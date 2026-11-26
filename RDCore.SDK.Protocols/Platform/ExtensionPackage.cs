using System.IO.Compression;
using System.Security.Cryptography;

namespace RDCore.SDK.Platform;

/// <summary>
/// The platform an extension is installed into: its <c>Extensions</c> folder, the RDCore.SDK it runs with, the system it runs on, and the
/// extension it bundles.
/// </summary>
/// <param name="ExtensionsFolder">The platform's <c>Extensions</c> folder, which holds one folder per extension, named by its title.</param>
/// <param name="Sdk">The RDCore.SDK version the platform runs with: an extension built against an incompatible one is not installed.</param>
/// <param name="RuntimeIdentifier">The platform's runtime identifier (<see cref="PlatformManifest.RuntimeIdentifier"/>); empty when its
/// manifest does not name one, and then any system's package is installed.</param>
/// <param name="CoreExtension">The extension the platform bundles (<see cref="PlatformManifest.CoreExtension"/>), which is never installed,
/// replaced, removed or carried.</param>
public sealed record ExtensionTarget(string ExtensionsFolder, PlatformVersion Sdk, string RuntimeIdentifier, string CoreExtension);

/// <summary>
/// What installing an extension package did, or why it did not install it.
/// </summary>
public enum ExtensionInstallOutcome
{
    /// <summary>
    /// The extension is installed, where none of its title was.
    /// </summary>
    Installed,
    /// <summary>
    /// The extension is installed in place of the one of its title.
    /// </summary>
    Replaced,
    /// <summary>
    /// The zip holds no <see cref="ExtensionPackageManifest.FileName"/> at its root.
    /// </summary>
    NotAPackage,
    /// <summary>
    /// An entry of the zip would be written outside the extension's folder.
    /// </summary>
    UnsafeEntry,
    /// <summary>
    /// The manifest's title is not the name of a folder: it is empty, or holds a path separator or a character no file name can hold.
    /// </summary>
    NotAFolderName,
    /// <summary>
    /// The manifest's title is the extension the platform bundles.
    /// </summary>
    CoreExtension,
    /// <summary>
    /// The zip holds no executable of the manifest's name, at its root.
    /// </summary>
    ExecutableMissing,
    /// <summary>
    /// The executable is not the one the manifest's file hash describes.
    /// </summary>
    FileHashMismatch,
    /// <summary>
    /// The package was published for another system than the platform's, or its manifest does not say which.
    /// </summary>
    OtherRuntime,
    /// <summary>
    /// The extension was built against an RDCore.SDK that is not compatible with the platform's (<see cref="PlatformVersion.IsCompatibleWith"/>),
    /// or its manifest does not say which.
    /// </summary>
    IncompatibleSdk,
    /// <summary>
    /// The installed extension of the title is a later version, and the install was not forced.
    /// </summary>
    Older,
    /// <summary>
    /// The installed extension of the title could not be moved out of the way: on Windows, a process runs from it.
    /// </summary>
    InUse,
}

/// <summary>
/// What packing an extension's folder did, or why it did not pack it.
/// </summary>
public enum ExtensionPackOutcome
{
    /// <summary>
    /// The package is written.
    /// </summary>
    Packed,
    /// <summary>
    /// The folder holds no <see cref="ExtensionPackageManifest.FileName"/>.
    /// </summary>
    NoManifest,
    /// <summary>
    /// The folder holds no executable of the manifest's name.
    /// </summary>
    ExecutableMissing,
    /// <summary>
    /// The executable is not the one the manifest's file hash describes: it changed after the extension was described.
    /// </summary>
    FileHashMismatch,
}

/// <summary>
/// What removing an installed extension did, or why it did not remove it.
/// </summary>
public enum ExtensionRemoveOutcome
{
    /// <summary>
    /// The extension's folder is removed.
    /// </summary>
    Removed,
    /// <summary>
    /// No extension of the title is installed.
    /// </summary>
    NotInstalled,
    /// <summary>
    /// The title is the extension the platform bundles, which is not removed.
    /// </summary>
    CoreExtension,
    /// <summary>
    /// The extension's folder could not be moved out of the way: on Windows, a process runs from it.
    /// </summary>
    InUse,
}

/// <summary>
/// The extensions a newly deployed version of the platform was given from the current one, and those it was not.
/// </summary>
/// <param name="Carried">The extensions copied into the new version.</param>
/// <param name="LeftBehind">The extensions the new version cannot run: built against an incompatible RDCore.SDK, or for another system.
/// They stay in the version they were installed in.</param>
public sealed record CarriedExtensions(IReadOnlyList<ExtensionPackageManifest> Carried, IReadOnlyList<ExtensionPackageManifest> LeftBehind);

/// <summary>
/// Packs an extension's folder into a package, installs packages into a deployed platform, removes and lists its extensions, and carries
/// them into the next version that is deployed.
/// </summary>
/// <remarks>
/// <para>
/// An extension package is the zip an extension's release attaches for one system: the files of the extension's folder, its
/// <see cref="ExtensionPackageManifest.FileName"/> among them, at the zip's root (<see cref="Pack"/>). It is installed into the platform's
/// <c>Extensions</c> folder, in a folder named by its title, under the platform root: no other folder holds extensions. Installing runs none of
/// the extension's code.
/// </para>
/// <para>
/// A folder is written whole into a <c>.partial</c> folder beside it, then moved into place. A folder that is replaced or removed is first
/// moved aside, to a <c>.removing</c> folder, then deleted; one that could not be deleted is deleted by the next install or removal
/// (<see cref="SweepLeftovers"/>).
/// </para>
/// <para>
/// A deployed platform's own files never change: only the folders of the extensions it does not bundle are added and removed. The caller
/// holds the shared root's lock (<see cref="SharedPlatformRoot.LockAsync"/>) for each call.
/// </para>
/// </remarks>
public static class ExtensionPackage
{
    private const string PartialSuffix = ".partial";
    private const string RemovingSuffix = ".removing";

    /// <summary>
    /// Packs an extension's folder, as <c>rdc describe-ext</c> left it, into the package <see cref="Install"/> takes: the folder's files at
    /// the zip's root, the executable marked executable for Linux and macOS.
    /// </summary>
    /// <param name="extensionFolder">The extension's folder: its published files and its manifest.</param>
    /// <param name="zip">The package's path. A file there is replaced once the package is whole.</param>
    /// <returns>What packing did, or why it did not pack: nothing is written but for <see cref="ExtensionPackOutcome.Packed"/>.</returns>
    /// <exception cref="InvalidDataException">The manifest is longer than <see cref="ExtensionPackageManifest.MaxBytes"/>, or not an
    /// extension manifest.</exception>
    /// <exception cref="IOException">The folder cannot be read, or the package cannot be written.</exception>
    public static ExtensionPackOutcome Pack(string extensionFolder, string zip)
    {
        if (ExtensionPackageManifest.Read(extensionFolder) is not { } manifest)
        {
            return ExtensionPackOutcome.NoManifest;
        }

        var executable = Path.Combine(extensionFolder, manifest.Name);
        if (!IsFolderName(manifest.Name) || !File.Exists(executable))
        {
            return ExtensionPackOutcome.ExecutableMissing;
        }

        using (var stream = File.OpenRead(executable))
        {
            if (!string.Equals(Convert.ToBase64String(SHA512.HashData(stream)), manifest.FileHash, StringComparison.Ordinal))
            {
                return ExtensionPackOutcome.FileHashMismatch;
            }
        }

        var partial = zip + PartialSuffix;
        try
        {
            using (var archive = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                // a package written into the folder it packs is not packed into itself.
                var written = new[] { Path.GetFullPath(partial), Path.GetFullPath(zip) };
                foreach (var file in Directory.EnumerateFiles(extensionFolder, "*", SearchOption.AllDirectories)
                    .Where(file => !written.Contains(Path.GetFullPath(file), StringComparer.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToList())
                {
                    var name = Path.GetRelativePath(extensionFolder, file).Replace('\\', '/');
                    var entry = archive.CreateEntryFromFile(file, name);
                    // a regular file's Unix mode, in the upper half: rwxr-xr-x for the executable, rw-r--r-- for the others.
                    entry.ExternalAttributes = (string.Equals(name, manifest.Name, StringComparison.Ordinal) ? 0x81ED : 0x81A4) << 16;
                }
            }

            File.Move(partial, zip, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }

        return ExtensionPackOutcome.Packed;
    }

    /// <summary>
    /// Reads the manifest of an extension package, without installing it.
    /// </summary>
    /// <param name="zip">The package's path.</param>
    /// <returns>The manifest; null when the zip holds none at its root.</returns>
    /// <exception cref="InvalidDataException">The zip is not one, or its manifest is longer than <see cref="ExtensionPackageManifest.MaxBytes"/>
    /// or not an extension manifest.</exception>
    /// <exception cref="IOException">The zip cannot be read.</exception>
    public static ExtensionPackageManifest? ReadManifest(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        return ManifestOf(archive, zip);
    }

    /// <summary>
    /// Installs an extension package into a platform, in place of the installed extension of its title, if there is one.
    /// </summary>
    /// <param name="zip">The package's path.</param>
    /// <param name="target">The platform it is installed into.</param>
    /// <param name="force">Whether a version earlier than the installed one replaces it.</param>
    /// <returns>What the install did, or why it did not install: nothing is written but for <see cref="ExtensionInstallOutcome.Installed"/>
    /// and <see cref="ExtensionInstallOutcome.Replaced"/>.</returns>
    /// <exception cref="InvalidDataException">The zip is not one, or its manifest is longer than <see cref="ExtensionPackageManifest.MaxBytes"/>
    /// or not an extension manifest.</exception>
    /// <exception cref="IOException">The zip cannot be read, or the extension's folder cannot be written.</exception>
    public static ExtensionInstallOutcome Install(string zip, ExtensionTarget target, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var archive = ZipFile.OpenRead(zip);
        if (ManifestOf(archive, zip) is not { } manifest)
        {
            return ExtensionInstallOutcome.NotAPackage;
        }

        var refused = Refusal(manifest, target);
        if (refused is { } outcome)
        {
            return outcome;
        }

        var folder = Path.Combine(target.ExtensionsFolder, manifest.Title);
        var partial = folder + PartialSuffix;
        if (archive.Entries.Any(entry => !IsUnder(partial, entry.FullName)))
        {
            return ExtensionInstallOutcome.UnsafeEntry;
        }

        if (archive.GetEntry(manifest.Name) is not { } executable)
        {
            return ExtensionInstallOutcome.ExecutableMissing;
        }

        if (!string.Equals(HashOf(executable), manifest.FileHash, StringComparison.Ordinal))
        {
            return ExtensionInstallOutcome.FileHashMismatch;
        }

        var installed = Directory.Exists(folder) ? ReadInstalled(folder) : null;
        if (!force && installed?.Version is { } current && manifest.Version is { } version && version < current)
        {
            return ExtensionInstallOutcome.Older;
        }

        SweepLeftovers(target.ExtensionsFolder);
        Directory.CreateDirectory(partial);
        try
        {
            archive.ExtractToDirectory(partial, overwriteFiles: true);
            MakeExecutable(Path.Combine(partial, manifest.Name));
        }
        catch
        {
            Directory.Delete(partial, recursive: true);
            throw;
        }

        var replacing = Directory.Exists(folder);
        if (replacing && !TryMoveAside(folder))
        {
            Directory.Delete(partial, recursive: true);
            return ExtensionInstallOutcome.InUse;
        }

        Directory.Move(partial, folder);
        SweepLeftovers(target.ExtensionsFolder);
        return replacing ? ExtensionInstallOutcome.Replaced : ExtensionInstallOutcome.Installed;
    }

    /// <summary>
    /// Removes an installed extension from a platform.
    /// </summary>
    /// <param name="target">The platform.</param>
    /// <param name="title">The extension's title, the name of its folder, in any case.</param>
    /// <returns>What the removal did, or why it did not remove the extension.</returns>
    /// <exception cref="IOException">The folder cannot be moved aside for another reason than a process that runs from it.</exception>
    public static ExtensionRemoveOutcome Remove(ExtensionTarget target, string title)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.Equals(title, target.CoreExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ExtensionRemoveOutcome.CoreExtension;
        }

        if (!IsFolderName(title) || FolderOf(target.ExtensionsFolder, title) is not { } folder)
        {
            return ExtensionRemoveOutcome.NotInstalled;
        }

        SweepLeftovers(target.ExtensionsFolder);
        if (!TryMoveAside(folder))
        {
            return ExtensionRemoveOutcome.InUse;
        }

        SweepLeftovers(target.ExtensionsFolder);
        return ExtensionRemoveOutcome.Removed;
    }

    /// <summary>
    /// The extensions installed in a platform's <c>Extensions</c> folder, the one it bundles among them: each folder that holds a manifest.
    /// </summary>
    /// <param name="extensionsFolder">The platform's <c>Extensions</c> folder.</param>
    /// <returns>Each extension's folder and its manifest, by title; a manifest that cannot be read is null. None when the folder does not
    /// exist.</returns>
    public static IReadOnlyList<(string Folder, ExtensionPackageManifest? Manifest)> List(string extensionsFolder)
    {
        if (!Directory.Exists(extensionsFolder))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(extensionsFolder)
            .Where(folder => !IsLeftover(folder) && File.Exists(Path.Combine(folder, ExtensionPackageManifest.FileName)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(folder => (folder, ReadInstalled(folder)))];
    }

    /// <summary>
    /// Copies the extensions of the current version into a version that is being deployed, before it is moved into place: each one the new
    /// version can run, which is neither the one it bundles nor one it holds already.
    /// </summary>
    /// <param name="fromExtensionsFolder">The current version's <c>Extensions</c> folder.</param>
    /// <param name="into">The version being deployed: its <c>Extensions</c> folder, under its <see cref="SharedPlatformRoot.PartialRoot"/>.</param>
    /// <returns>The extensions copied, and those left behind: built against an RDCore.SDK incompatible with the new version's, or for
    /// another system. An extension whose manifest cannot be read is neither.</returns>
    /// <exception cref="IOException">An extension cannot be copied.</exception>
    public static CarriedExtensions CarryForward(string fromExtensionsFolder, ExtensionTarget into)
    {
        ArgumentNullException.ThrowIfNull(into);
        List<ExtensionPackageManifest> carried = [];
        List<ExtensionPackageManifest> leftBehind = [];
        foreach (var (folder, manifest) in List(fromExtensionsFolder))
        {
            if (manifest is null || string.Equals(manifest.Title, into.CoreExtension, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(folder), manifest.Title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsCompatible(manifest, into))
            {
                leftBehind.Add(manifest);
                continue;
            }

            var copy = Path.Combine(into.ExtensionsFolder, manifest.Title);
            if (!Directory.Exists(copy))
            {
                CopyFolder(folder, copy);
                carried.Add(manifest);
            }
        }

        return new CarriedExtensions(carried, leftBehind);
    }

    /// <summary>
    /// Deletes the folders an install or a removal left in a platform's <c>Extensions</c> folder: a <c>.partial</c> folder an interrupted
    /// install left, and a <c>.removing</c> folder that could not be deleted. One that still cannot be deleted is left.
    /// </summary>
    /// <param name="extensionsFolder">The platform's <c>Extensions</c> folder.</param>
    public static void SweepLeftovers(string extensionsFolder)
    {
        if (!Directory.Exists(extensionsFolder))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(extensionsFolder).Where(IsLeftover))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // a process still runs from it: the next sweep deletes it.
            }
        }
    }

    private static ExtensionInstallOutcome? Refusal(ExtensionPackageManifest manifest, ExtensionTarget target)
    {
        if (!IsFolderName(manifest.Title))
        {
            return ExtensionInstallOutcome.NotAFolderName;
        }

        if (string.Equals(manifest.Title, target.CoreExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ExtensionInstallOutcome.CoreExtension;
        }

        if (!IsFolderName(manifest.Name))
        {
            return ExtensionInstallOutcome.ExecutableMissing;
        }

        if (!IsForTheSystem(manifest, target))
        {
            return ExtensionInstallOutcome.OtherRuntime;
        }

        return manifest.SdkVersion is { } sdk && sdk.IsCompatibleWith(target.Sdk) ? null : ExtensionInstallOutcome.IncompatibleSdk;
    }

    private static bool IsCompatible(ExtensionPackageManifest manifest, ExtensionTarget target) =>
        IsForTheSystem(manifest, target) && manifest.SdkVersion is { } sdk && sdk.IsCompatibleWith(target.Sdk);

    // a platform that does not name its system runs any package: a development build's manifest may name none.
    private static bool IsForTheSystem(ExtensionPackageManifest manifest, ExtensionTarget target) =>
        target.RuntimeIdentifier.Length == 0
        || string.Equals(manifest.RuntimeIdentifier, target.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase);

    // a title, or an executable's name, is one file name: no separator, nothing a file name cannot hold, and no folder of its own suffix.
    private static bool IsFolderName(string name) =>
        name.Length > 0 && name is not ("." or "..") && name.Trim() == name
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.IndexOfAny(['/', '\\', ':']) < 0
        && !name.EndsWith(PartialSuffix, StringComparison.OrdinalIgnoreCase) && !name.Contains(RemovingSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsLeftover(string folder)
    {
        var name = Path.GetFileName(folder);
        return name.EndsWith(PartialSuffix, StringComparison.OrdinalIgnoreCase) || name.Contains(RemovingSuffix, StringComparison.OrdinalIgnoreCase);
    }

    // the folder of a title, in the case it is written on disk.
    private static string? FolderOf(string extensionsFolder, string title) =>
        Directory.Exists(extensionsFolder)
            ? Directory.EnumerateDirectories(extensionsFolder)
                .FirstOrDefault(folder => !IsLeftover(folder) && string.Equals(Path.GetFileName(folder), title, StringComparison.OrdinalIgnoreCase))
            : null;

    private static ExtensionPackageManifest? ManifestOf(ZipArchive archive, string zip)
    {
        if (archive.GetEntry(ExtensionPackageManifest.FileName) is not { } entry)
        {
            return null;
        }

        var path = $"{zip}!{ExtensionPackageManifest.FileName}";
        if (entry.Length > ExtensionPackageManifest.MaxBytes)
        {
            throw ExtensionPackageManifest.TooLarge(path, entry.Length);
        }

        using var stream = entry.Open();
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return ExtensionPackageManifest.Parse(content.ToArray(), path);
    }

    private static ExtensionPackageManifest? ReadInstalled(string folder)
    {
        try
        {
            return ExtensionPackageManifest.Read(folder);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsUnder(string folder, string entryName)
    {
        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(folder, entryName));
        return !Path.IsPathRooted(entryName)
            && path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string HashOf(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return Convert.ToBase64String(SHA512.HashData(stream));
    }

    // a zip made on Windows records no Unix mode: the executable the platform starts is made executable by its owner, its group and others.
    private static void MakeExecutable(string executable)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, File.GetUnixFileMode(executable)
                | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    // moved aside whole, then deleted: false when it cannot be moved, which on Windows is while a process runs from it.
    private static bool TryMoveAside(string folder)
    {
        var aside = $"{folder}{RemovingSuffix}-{Guid.NewGuid():N}";
        try
        {
            Directory.Move(folder, aside);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void CopyFolder(string from, string to)
    {
        var partial = to + PartialSuffix;
        Directory.CreateDirectory(partial);
        try
        {
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var copy = Path.Combine(partial, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy);
            }
        }
        catch
        {
            Directory.Delete(partial, recursive: true);
            throw;
        }

        Directory.Move(partial, to);
    }
}

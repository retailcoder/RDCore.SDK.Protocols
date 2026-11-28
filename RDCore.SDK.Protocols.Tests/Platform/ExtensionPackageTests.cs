using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// An extension package, the zip an extension's release attaches for one system, is installed into a platform's <c>Extensions</c> folder,
/// in a folder named by its title, when it was built for the platform's system and an RDCore.SDK compatible with the platform's. Installed
/// extensions are removed, listed, and carried into the next version deployed.
/// </summary>
[TestClass]
public sealed class ExtensionPackageTests
{
    private const string Title = "RDCore.Web";
    private const string Executable = "RDCore.Web.Host";
    private const string Rid = "linux-x64";
    private static readonly PlatformVersion Sdk = PlatformVersion.Parse("0.3.1-alpha");

    private string _folder = null!;
    private string _extensions = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "RDCore", "extension-package", Guid.NewGuid().ToString("N"));
        _extensions = Path.Combine(_folder, "versions", "0.3.1", "Extensions");
        Directory.CreateDirectory(Path.Combine(_extensions, PlatformManifest.DefaultCoreExtension));
        File.WriteAllText(Path.Combine(_extensions, PlatformManifest.DefaultCoreExtension, ExtensionPackageManifest.FileName), "{}");
    }

    [TestCleanup]
    public void DeleteFolder()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ExtensionTarget Target(string? extensions = null, string rid = Rid) =>
        new(extensions ?? _extensions, Sdk, rid, PlatformManifest.DefaultCoreExtension);

    /// <summary>
    /// An extension package to write: its manifest's members, its executable and one more file, and the hashes its manifest lists of them,
    /// which a test may leave out, make one of them different, list a file it does not hold, or hold a file it does not list or one twice.
    /// </summary>
    public sealed record Package(string Title = Title, string Version = "1.0.0.0", string? SdkVersion = "0.3.0", string? Rid = Rid,
        string Name = Executable, string Content = "host", string? Hash = null, bool WithExecutable = true, bool WithManifest = true,
        string? ExtraEntry = "lib/RDCore.Web.dll", bool WithFileHashes = true, bool ExtraChanged = false, string? Missing = null,
        string? Unlisted = null, bool ExtraTwice = false);

    private static string HashOf(string content) => Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(content)));

    // the manifest describe-ext writes, PascalCase, with the members an installer does not read.
    private static string ManifestJson(Package package)
    {
        var manifest = new Dictionary<string, object?>
        {
            ["Name"] = package.Name,
            ["Title"] = package.Title,
            ["Version"] = package.Version,
            ["Publisher"] = "Rubberduck VBA",
            ["FileHash"] = package.Hash ?? HashOf(package.Content),
            ["Capabilities"] = Array.Empty<object>(),
            ["SdkVersion"] = package.SdkVersion,
            ["RuntimeIdentifier"] = package.Rid,
        };
        if (package.WithFileHashes)
        {
            var hashes = new Dictionary<string, string>();
            if (package.WithExecutable)
            {
                hashes[package.Name] = HashOf(package.Content);
            }

            if (package.ExtraEntry is { } extra)
            {
                hashes[extra] = HashOf(package.ExtraChanged ? "library, changed" : "library");
            }

            if (package.Missing is { } missing)
            {
                hashes[missing] = HashOf("missing");
            }

            manifest["FileHashes"] = hashes;
        }

        return JsonSerializer.Serialize(manifest);
    }

    private string Zip(Package package)
    {
        var path = Path.Combine(_folder, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }

        if (package.WithManifest)
        {
            Add(ExtensionPackageManifest.FileName, ManifestJson(package));
        }

        if (package.WithExecutable)
        {
            Add(package.Name, package.Content);
        }

        if (package.ExtraEntry is { } extra)
        {
            Add(extra, "library");
            if (package.ExtraTwice)
            {
                Add(extra, "library");
            }
        }

        if (package.Unlisted is { } unlisted)
        {
            Add(unlisted, "unlisted");
        }

        return path;
    }

    private string Installed(params string[] path) => Path.Combine([_extensions, Title, .. path]);

    private string[] ExtensionsFolders() =>
        [.. Directory.EnumerateDirectories(_extensions).Select(folder => Path.GetFileName(folder)).Order(StringComparer.Ordinal)];

    [TestMethod]
    public void APackageForThePlatform_IsInstalledInAFolderNamedByItsTitle()
    {
        var outcome = ExtensionPackage.Install(Zip(new()), Target());

        Assert.AreEqual(ExtensionInstallOutcome.Installed, outcome);
        Assert.AreEqual("host", File.ReadAllText(Installed(Executable)));
        Assert.AreEqual("library", File.ReadAllText(Installed("lib", "RDCore.Web.dll")));
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension, Title }, ExtensionsFolders(), "nothing is left beside it");
    }

    [TestMethod]
    public void APackageOfAnInstalledTitle_ReplacesIt_Whole()
    {
        ExtensionPackage.Install(Zip(new()), Target());

        var outcome = ExtensionPackage.Install(Zip(new(Version: "1.1.0.0", Content: "host 1.1", ExtraEntry: null)), Target());

        Assert.AreEqual(ExtensionInstallOutcome.Replaced, outcome);
        Assert.AreEqual("host 1.1", File.ReadAllText(Installed(Executable)));
        Assert.IsFalse(File.Exists(Installed("lib", "RDCore.Web.dll")), "the replaced version's files are gone");
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension, Title }, ExtensionsFolders(), "nothing is left beside it");
    }

    [TestMethod]
    public void AnEarlierVersion_IsRefused_UnlessForced()
    {
        ExtensionPackage.Install(Zip(new(Version: "1.1.0.0", Content: "host 1.1")), Target());
        var earlier = Zip(new(Version: "1.0.0.0"));

        Assert.AreEqual(ExtensionInstallOutcome.Older, ExtensionPackage.Install(earlier, Target()));
        Assert.AreEqual("host 1.1", File.ReadAllText(Installed(Executable)));
        Assert.AreEqual(ExtensionInstallOutcome.Replaced, ExtensionPackage.Install(earlier, Target(), force: true));
        Assert.AreEqual("host", File.ReadAllText(Installed(Executable)));
    }

    public static IEnumerable<object[]> Refusals =>
    [
        [new Package(WithManifest: false), ExtensionInstallOutcome.NotAPackage],
        [new Package(Title: "a/b"), ExtensionInstallOutcome.NotAFolderName],
        [new Package(Title: ".."), ExtensionInstallOutcome.NotAFolderName],
        [new Package(Title: "rdcore.diagnostics"), ExtensionInstallOutcome.CoreExtension],
        [new Package(WithExecutable: false), ExtensionInstallOutcome.ExecutableMissing],
        [new Package(Hash: "not the hash"), ExtensionInstallOutcome.FileHashMismatch],
        [new Package(Rid: "win-x64"), ExtensionInstallOutcome.OtherRuntime],
        [new Package(Rid: null), ExtensionInstallOutcome.OtherRuntime],
        [new Package(SdkVersion: "0.2.1"), ExtensionInstallOutcome.IncompatibleSdk],
        [new Package(SdkVersion: null), ExtensionInstallOutcome.IncompatibleSdk],
        [new Package(ExtraEntry: "../escaped.txt"), ExtensionInstallOutcome.UnsafeEntry],
        [new Package(WithFileHashes: false), ExtensionInstallOutcome.ContentMismatch],
        [new Package(ExtraChanged: true), ExtensionInstallOutcome.ContentMismatch],
        [new Package(Missing: "lib/RDCore.Web.Core.dll"), ExtensionInstallOutcome.ContentMismatch],
        [new Package(Unlisted: "lib/RDCore.Web.Core.dll"), ExtensionInstallOutcome.ContentMismatch],
        [new Package(ExtraTwice: true), ExtensionInstallOutcome.ContentMismatch],
    ];

    [TestMethod]
    [DynamicData(nameof(Refusals))]
    public void APackageThePlatformCannotRun_IsRefused_AndNothingIsWritten(Package package, ExtensionInstallOutcome refused)
    {
        var outcome = ExtensionPackage.Install(Zip(package), Target());

        Assert.AreEqual(refused, outcome);
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension }, ExtensionsFolders());
        Assert.IsFalse(File.Exists(Path.Combine(_extensions, "escaped.txt")), "no entry is written outside the extension's folder");
    }

    // a development build's platform manifest may name no system.
    [TestMethod]
    public void APlatformThatNamesNoSystem_InstallsAnySystemsPackage()
        => Assert.AreEqual(ExtensionInstallOutcome.Installed, ExtensionPackage.Install(Zip(new(Rid: "osx-arm64")), Target(rid: "")));

    [TestMethod]
    public void APackagesManifest_IsReadWithoutInstallingIt()
    {
        var manifest = ExtensionPackage.ReadManifest(Zip(new()));

        Assert.IsNotNull(manifest);
        Assert.AreEqual(Title, manifest.Title);
        Assert.AreEqual(new Version(1, 0, 0, 0), manifest.Version);
        Assert.AreEqual("0.3.0", manifest.SdkVersion?.Text);
        Assert.AreEqual(Rid, manifest.RuntimeIdentifier);
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension }, ExtensionsFolders());
    }

    [TestMethod]
    public void AnInstalledExtension_IsRemoved_AndTheCoreExtensionIsNot()
    {
        ExtensionPackage.Install(Zip(new()), Target());

        Assert.AreEqual(ExtensionRemoveOutcome.Removed, ExtensionPackage.Remove(Target(), "rdcore.web"));
        Assert.AreEqual(ExtensionRemoveOutcome.NotInstalled, ExtensionPackage.Remove(Target(), Title));
        Assert.AreEqual(ExtensionRemoveOutcome.CoreExtension, ExtensionPackage.Remove(Target(), PlatformManifest.DefaultCoreExtension));
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension }, ExtensionsFolders());
    }

    // on Windows, a folder a process holds a file of cannot be moved: the extension stays whole, and installed.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AnExtensionInUse_IsNeitherReplacedNorRemoved()
    {
        ExtensionPackage.Install(Zip(new()), Target());
        using var held = new FileStream(Installed(Executable), FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.AreEqual(ExtensionInstallOutcome.InUse, ExtensionPackage.Install(Zip(new(Version: "1.1.0.0", Content: "host 1.1")), Target()));
        Assert.AreEqual(ExtensionRemoveOutcome.InUse, ExtensionPackage.Remove(Target(), Title));
        Assert.AreEqual("host", File.ReadAllText(Installed(Executable)));
        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension, Title }, ExtensionsFolders());
    }

    // a zip made on Windows records no Unix mode.
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void AnInstalledExtensionsExecutable_IsExecutable()
    {
        ExtensionPackage.Install(Zip(new()), Target());

        Assert.IsTrue(File.GetUnixFileMode(Installed(Executable)).HasFlag(UnixFileMode.UserExecute));
    }

    [TestMethod]
    public void TheInstalledExtensions_AreListed_WithoutTheFoldersAnInstallLeft()
    {
        ExtensionPackage.Install(Zip(new()), Target());
        Directory.CreateDirectory(Path.Combine(_extensions, "RDCore.Other.partial"));
        File.WriteAllText(Path.Combine(_extensions, "RDCore.Other.partial", ExtensionPackageManifest.FileName), "{}");

        var listed = ExtensionPackage.List(_extensions);

        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension, Title }, listed.Select(e => Path.GetFileName(e.Folder)).ToArray());
        Assert.AreEqual(Title, listed[1].Manifest?.Title);
    }

    [TestMethod]
    public void TheFoldersAnInstallLeft_AreSwept()
    {
        Directory.CreateDirectory(Path.Combine(_extensions, $"{Title}.partial"));
        Directory.CreateDirectory(Path.Combine(_extensions, $"{Title}.removing-0123"));

        ExtensionPackage.SweepLeftovers(_extensions);

        CollectionAssert.AreEqual(new[] { PlatformManifest.DefaultCoreExtension }, ExtensionsFolders());
    }

    // the next version deployed runs on RDCore.SDK 0.3.x: an extension built on 0.2.x stays in the version it was installed in.
    [TestMethod]
    public void TheExtensionsTheNextVersionCanRun_AreCarriedIntoIt_AndTheOthersAreLeftBehind()
    {
        var current = Path.Combine(_folder, "versions", "0.2.0", "Extensions");
        var currentTarget = new ExtensionTarget(current, PlatformVersion.Parse("0.3.0"), "", PlatformManifest.DefaultCoreExtension);
        Directory.CreateDirectory(Path.Combine(current, PlatformManifest.DefaultCoreExtension));
        File.WriteAllText(Path.Combine(current, PlatformManifest.DefaultCoreExtension, ExtensionPackageManifest.FileName),
            "{\"Title\":\"RDCore.Diagnostics\",\"SdkVersion\":\"0.3.0\"}");
        ExtensionPackage.Install(Zip(new()), currentTarget);
        ExtensionPackage.Install(Zip(new(Title: "RDCore.Old", SdkVersion: "0.2.0")), currentTarget with { Sdk = PlatformVersion.Parse("0.2.0") });
        ExtensionPackage.Install(Zip(new(Title: "RDCore.Mac", Rid: "osx-arm64")), currentTarget);
        var next = Path.Combine(_folder, "versions", "0.3.1.partial", "Extensions");

        var carried = ExtensionPackage.CarryForward(current, Target(next));

        CollectionAssert.AreEqual(new[] { Title }, carried.Carried.Select(m => m.Title).ToArray());
        CollectionAssert.AreEquivalent(new[] { "RDCore.Old", "RDCore.Mac" }, carried.LeftBehind.Select(m => m.Title).ToArray());
        Assert.AreEqual("host", File.ReadAllText(Path.Combine(next, Title, Executable)));
        CollectionAssert.AreEqual(new[] { Title }, Directory.EnumerateDirectories(next).Select(Path.GetFileName).ToArray(),
            "neither the core extension nor the extensions left behind are copied");
        Assert.IsTrue(File.Exists(Path.Combine(current, "RDCore.Old", Executable)), "an extension left behind stays where it is");
    }

    // the version being deployed holds its own copy of an extension, as a release that bundles it: that copy is the one kept.
    [TestMethod]
    public void AnExtensionTheNextVersionHolds_IsNotCarried()
    {
        ExtensionPackage.Install(Zip(new()), Target());
        var next = Path.Combine(_folder, "versions", "0.3.2.partial", "Extensions");
        Directory.CreateDirectory(Path.Combine(next, Title));
        File.WriteAllText(Path.Combine(next, Title, Executable), "its own");

        var carried = ExtensionPackage.CarryForward(_extensions, Target(next));

        Assert.IsEmpty(carried.Carried);
        Assert.AreEqual("its own", File.ReadAllText(Path.Combine(next, Title, Executable)));
    }

    // an extension's folder as describe-ext leaves it: its published files and its manifest.
    private string DescribedFolder(Package package)
    {
        var folder = Path.Combine(_folder, "publish", package.Title);
        Directory.CreateDirectory(Path.Combine(folder, "lib"));
        File.WriteAllText(Path.Combine(folder, ExtensionPackageManifest.FileName), ManifestJson(package));
        if (package.WithExecutable)
        {
            File.WriteAllText(Path.Combine(folder, package.Name), package.Content);
        }

        File.WriteAllText(Path.Combine(folder, "lib", "RDCore.Web.dll"), "library");
        if (package.Unlisted is { } unlisted)
        {
            File.WriteAllText(Path.Combine(folder, unlisted), "unlisted");
        }

        return folder;
    }

    [TestMethod]
    public void AnExtensionsFolder_IsPacked_IntoAPackageThatInstalls()
    {
        var zip = Path.Combine(_folder, "RDCore.Web-1.0.0-linux-x64.zip");

        Assert.AreEqual(ExtensionPackOutcome.Packed, ExtensionPackage.Pack(DescribedFolder(new()), zip));

        Assert.AreEqual(ExtensionInstallOutcome.Installed, ExtensionPackage.Install(zip, Target()));
        Assert.AreEqual("host", File.ReadAllText(Installed(Executable)));
        Assert.AreEqual("library", File.ReadAllText(Installed("lib", "RDCore.Web.dll")));
        Assert.IsFalse(File.Exists(zip + ".partial"));
    }

    // Unix reads a file's mode from the upper half of the entry's external attributes, whichever system wrote the zip.
    [TestMethod]
    public void APackagesExecutable_IsMarkedExecutable_AndItsOtherFilesAreNot()
    {
        var zip = Path.Combine(_folder, "package.zip");
        ExtensionPackage.Pack(DescribedFolder(new()), zip);

        using var archive = ZipFile.OpenRead(zip);
        Assert.AreEqual(0x1ED, (archive.GetEntry(Executable)!.ExternalAttributes >> 16) & 0x1FF, "rwxr-xr-x");
        Assert.AreEqual(0x1A4, (archive.GetEntry("lib/RDCore.Web.dll")!.ExternalAttributes >> 16) & 0x1FF, "rw-r--r--");
    }

    // a package written into the folder it packs is not packed into itself.
    [TestMethod]
    public void APackageWrittenInItsOwnFolder_DoesNotHoldItself()
    {
        var folder = DescribedFolder(new());
        var zip = Path.Combine(folder, "package.zip");

        ExtensionPackage.Pack(folder, zip);

        using var archive = ZipFile.OpenRead(zip);
        CollectionAssert.AreEquivalent(new[] { ExtensionPackageManifest.FileName, Executable, "lib/RDCore.Web.dll" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    public static IEnumerable<object[]> PackRefusals =>
    [
        [new Package(WithManifest: false), ExtensionPackOutcome.NoManifest],
        [new Package(WithExecutable: false), ExtensionPackOutcome.ExecutableMissing],
        [new Package(Hash: "not the hash"), ExtensionPackOutcome.FileHashMismatch],
        [new Package(WithFileHashes: false), ExtensionPackOutcome.ContentMismatch],
        [new Package(ExtraChanged: true), ExtensionPackOutcome.ContentMismatch],
        [new Package(Missing: "lib/RDCore.Web.Core.dll"), ExtensionPackOutcome.ContentMismatch],
        [new Package(Unlisted: "lib/RDCore.Web.Core.dll"), ExtensionPackOutcome.ContentMismatch],
    ];

    [TestMethod]
    [DynamicData(nameof(PackRefusals))]
    public void AFolderThatIsNotADescribedExtension_IsNotPacked(Package package, ExtensionPackOutcome refused)
    {
        var folder = DescribedFolder(package);
        if (!package.WithManifest)
        {
            File.Delete(Path.Combine(folder, ExtensionPackageManifest.FileName));
        }

        var zip = Path.Combine(_folder, "package.zip");

        Assert.AreEqual(refused, ExtensionPackage.Pack(folder, zip));
        Assert.IsFalse(File.Exists(zip));
    }

    // the manifest holds the hashes, so it is not among them; a file in a subfolder is named with a slash on every system.
    [TestMethod]
    public void TheHashesOfAFolder_AreThoseOfEveryFileButItsManifest_ByItsPathInTheFolder()
    {
        var hashes = ExtensionFileHashes.Of(DescribedFolder(new()));

        CollectionAssert.AreEquivalent(
            new[] { $"{Executable}={HashOf("host")}", $"lib/RDCore.Web.dll={HashOf("library")}" },
            hashes.Select(file => $"{file.Key}={file.Value}").ToArray());
    }

    [TestMethod]
    public void AManifestsFileHashes_AreRead()
    {
        var manifest = ExtensionPackage.ReadManifest(Zip(new()));

        Assert.AreEqual(HashOf("library"), manifest?.FileHashes?["lib/RDCore.Web.dll"]);
    }

    [TestMethod]
    [DataRow("\"0.2.1-alpha\"", "0.2.1-alpha", DisplayName = "a platform version")]
    [DataRow("\"0.2.1.0\"", "0.2.1", DisplayName = "the four-part version an assembly reference writes")]
    [DataRow("\"0.2\"", "0.2.0", DisplayName = "a two-part version")]
    [DataRow("\"not a version\"", null, DisplayName = "not a version")]
    [DataRow("12", null, DisplayName = "a number")]
    [DataRow("null", null, DisplayName = "null")]
    public void AManifestsSdkVersion_IsReadAsAPlatformVersion(string json, string? expected)
        => Assert.AreEqual(expected, ExtensionPackageManifest.Parse(Encoding.UTF8.GetBytes($"{{\"sdkVersion\":{json}}}")).SdkVersion?.Text);

    [TestMethod]
    public void AVersion_IsWrittenAsItsText()
        => Assert.AreEqual("\"0.2.1-alpha\"", JsonSerializer.Serialize(PlatformVersion.Parse("0.2.1-alpha"),
            new JsonSerializerOptions { Converters = { new PlatformVersionJsonConverter() } }));

    [TestMethod]
    public void AFileThatIsNotAManifest_IsInvalidData()
        => Assert.ThrowsExactly<InvalidDataException>(() => ExtensionPackageManifest.Parse("[1, 2"u8));

    [TestMethod]
    public void AManifestLongerThanTheLargestRead_IsInvalidData()
        => Assert.ThrowsExactly<InvalidDataException>(() => ExtensionPackageManifest.Parse(new byte[ExtensionPackageManifest.MaxBytes + 1]));
}

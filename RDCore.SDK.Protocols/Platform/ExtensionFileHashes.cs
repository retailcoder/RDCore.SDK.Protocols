using System.Security.Cryptography;

namespace RDCore.SDK.Platform;

/// <summary>
/// The hashes of the files of an extension's folder, which its manifest lists (<see cref="ExtensionPackageManifest.FileHashes"/>): each file
/// under the folder, by its path relative to the folder, to its base64 SHA-512.
/// </summary>
/// <remarks>
/// <para>
/// Every file is listed, in every subfolder, the executable among them; only the folder's <see cref="ExtensionPackageManifest.FileName"/>,
/// which holds the hashes, is not. A path is written with <c>/</c> between its folders, on every system.
/// </para>
/// <para>
/// A folder matches its manifest when it holds the files the manifest lists, each with its hash, and no other: a listed file that is
/// missing or different, a file that is not listed, and a manifest that lists none are each a mismatch. <c>rdc describe-ext</c> lists the
/// files when it writes the manifest; <see cref="ExtensionPackage.Pack"/>, <see cref="ExtensionPackage.Install"/> and the platform, as it
/// loads an extension, each check them.
/// </para>
/// </remarks>
public static class ExtensionFileHashes
{
    /// <summary>
    /// The hashes of the files of an extension's folder.
    /// </summary>
    /// <param name="extensionFolder">The extension's folder.</param>
    /// <returns>Each file but the folder's manifest, by its path relative to the folder, to its base64 SHA-512.</returns>
    /// <exception cref="IOException">The folder or one of its files cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or one of its files cannot be read.</exception>
    public static IReadOnlyDictionary<string, string> Of(string extensionFolder)
        => OfFiles(extensionFolder, Directory.EnumerateFiles(extensionFolder, "*", SearchOption.AllDirectories));

    /// <summary>
    /// Whether the hashes of an extension's files are the ones its manifest lists: the same files, each with the same hash.
    /// </summary>
    /// <param name="listed">The hashes the manifest lists (<see cref="ExtensionPackageManifest.FileHashes"/>); null when it lists none, which
    /// matches no folder.</param>
    /// <param name="found">The hashes of the files the extension holds (<see cref="Of(string)"/>).</param>
    /// <returns>True when every file listed is found with its hash, and every file found is listed.</returns>
    public static bool Match(IReadOnlyDictionary<string, string>? listed, IReadOnlyDictionary<string, string> found)
    {
        ArgumentNullException.ThrowIfNull(found);
        return listed is not null && listed.Count == found.Count
            && listed.All(file => found.TryGetValue(file.Key, out var hash) && string.Equals(hash, file.Value, StringComparison.Ordinal));
    }

    // the hashes of some of a folder's files: those a package is written of.
    internal static IReadOnlyDictionary<string, string> OfFiles(string extensionFolder, IEnumerable<string> files)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var name = PathOf(Path.GetRelativePath(extensionFolder, file));
            if (!IsManifest(name))
            {
                using var stream = File.OpenRead(file);
                hashes[name] = HashOf(stream);
            }
        }

        return hashes;
    }

    // a file's path relative to the extension's folder, or a zip entry's name, as the manifest writes it.
    internal static string PathOf(string relativePath) => relativePath.Replace('\\', '/');

    internal static bool IsManifest(string path) => string.Equals(path, ExtensionPackageManifest.FileName, StringComparison.Ordinal);

    internal static string HashOf(Stream content) => Convert.ToBase64String(SHA512.HashData(content));
}

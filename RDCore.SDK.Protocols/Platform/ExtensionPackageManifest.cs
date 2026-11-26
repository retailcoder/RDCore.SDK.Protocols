using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Platform;

/// <summary>
/// What an installer reads of an extension manifest, <c>extension.manifest.json</c>: which extension it is, the executable the platform
/// starts, and the SDK and system it was built for.
/// </summary>
/// <remarks>
/// <c>rdc describe-ext</c> writes the manifest into the extension's folder, which is named by its <see cref="Title"/>, under the platform's
/// <c>Extensions</c> folder. An extension package, the zip its release attaches for one system, holds the folder's files, the manifest
/// among them, at its root (<see cref="ExtensionPackage"/>). The manifest is read regardless of case; its other members, such as its
/// capabilities and settings, are not read here.
/// </remarks>
public sealed record class ExtensionPackageManifest
{
    /// <summary>
    /// The name of the extension manifest, in the extension's folder and at the root of its package.
    /// </summary>
    public const string FileName = "extension.manifest.json";

    /// <summary>
    /// The largest extension manifest that is read, in bytes: 4 MiB.
    /// </summary>
    public const int MaxBytes = 4 << 20;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The extension's title, such as <c>RDCore.Web</c>: the name of its folder under the platform's <c>Extensions</c> folder.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The file name of the executable the platform starts, in the extension's folder: <c>RDCore.Web.Host.exe</c> on Windows,
    /// <c>RDCore.Web.Host</c> elsewhere.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The extension's version; null when the manifest does not say.
    /// </summary>
    public Version? Version { get; init; }

    /// <summary>
    /// The base64 SHA-512 of the executable (<see cref="Name"/>).
    /// </summary>
    public string FileHash { get; init; } = string.Empty;

    /// <summary>
    /// The RDCore.SDK version the extension was built against; null when the manifest does not say (<see cref="PlatformVersionJsonConverter"/>).
    /// </summary>
    [JsonConverter(typeof(PlatformVersionJsonConverter))]
    public PlatformVersion? SdkVersion { get; init; }

    /// <summary>
    /// The runtime identifier the extension was published for, such as <c>win-x64</c>, in the form
    /// <see cref="PlatformManifest.RuntimeIdentifier"/> takes; null when the manifest does not say.
    /// </summary>
    public string? RuntimeIdentifier { get; init; }

    /// <summary>
    /// Reads an extension manifest from its content.
    /// </summary>
    /// <param name="utf8Json">The manifest's content, in UTF-8.</param>
    /// <exception cref="InvalidDataException">The content is longer than <see cref="MaxBytes"/>, or not an extension manifest.</exception>
    public static ExtensionPackageManifest Parse(ReadOnlySpan<byte> utf8Json) => Parse(utf8Json, FileName);

    /// <summary>
    /// Reads the extension manifest of an extension's folder; null when it has none.
    /// </summary>
    /// <param name="extensionFolder">The extension's folder, such as <c>Extensions/RDCore.Web</c> under a platform root.</param>
    /// <exception cref="InvalidDataException">The manifest is longer than <see cref="MaxBytes"/>, or not an extension manifest.</exception>
    /// <exception cref="IOException">The manifest cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The manifest cannot be read.</exception>
    public static ExtensionPackageManifest? Read(string extensionFolder)
    {
        var file = new FileInfo(Path.Combine(extensionFolder, FileName));
        if (!file.Exists)
        {
            return null;
        }

        // the size first, so that no file is read whole for its name.
        return file.Length > MaxBytes
            ? throw TooLarge(file.FullName, file.Length)
            : Parse(File.ReadAllBytes(file.FullName), file.FullName);
    }

    internal static ExtensionPackageManifest Parse(ReadOnlySpan<byte> utf8Json, string path)
    {
        if (utf8Json.Length > MaxBytes)
        {
            throw TooLarge(path, utf8Json.Length);
        }

        try
        {
            return JsonSerializer.Deserialize<ExtensionPackageManifest>(utf8Json, Options)
                ?? throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, PlatformMessages.ExtensionManifestIsNotOne, path, "null"));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                string.Format(CultureInfo.CurrentCulture, PlatformMessages.ExtensionManifestIsNotOne, path, exception.Message), exception);
        }
    }

    internal static InvalidDataException TooLarge(string path, long size) =>
        new(string.Format(CultureInfo.CurrentCulture, PlatformMessages.ExtensionManifestIsTooLarge, path, size, MaxBytes));
}

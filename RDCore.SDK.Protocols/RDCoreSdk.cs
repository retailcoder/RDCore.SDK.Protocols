using System.Reflection;
using RDCore.SDK.Platform;

namespace RDCore.SDK;

/// <summary>
/// The RDCore.SDK packages: the SDK's package id, and the version a client or a platform part was built with.
/// </summary>
public static class RDCoreSdk
{
    /// <summary>
    /// The package's id: <c>RDCore.SDK</c>, as a release manifest names it (<see cref="PlatformReleasePackage.Package"/>).
    /// </summary>
    public const string PackageId = "RDCore.SDK";

    /// <summary>
    /// The version of the RDCore.SDK.Protocols package this program runs with, such as <c>0.2.0-alpha</c>: the package's version, without build
    /// metadata (<see cref="VersionOf"/>).
    /// </summary>
    /// <remarks>
    /// RDCore.SDK.Protocols and RDCore.SDK are versioned apart, with the same major and minor version: RDCore.SDK 0.2.1 runs on Protocols 0.2.0.
    /// A comparison by <see cref="PlatformVersion.IsCompatibleWith"/>, which reads only those two numbers, answers the same for either package's
    /// version (<see cref="PlatformReleaseManifest.IsBuiltWith"/>). RDCore.SDK's own version is <see cref="VersionOf"/> one of its assemblies.
    /// </remarks>
    public static PlatformVersion Version { get; } = VersionOf(typeof(RDCoreSdk).Assembly);

    /// <summary>
    /// The version an assembly of an RDCore package was packed as, without build metadata.
    /// </summary>
    /// <remarks>
    /// The package's build stamps that version into its assemblies' <see cref="AssemblyInformationalVersionAttribute"/>. An assembly without a
    /// stamped platform version reports the major, minor and patch numbers of its numeric <see cref="AssemblyName.Version"/>
    /// (<see cref="PlatformVersionJsonConverter.Of"/>).
    /// </remarks>
    /// <param name="assembly">The assembly, such as RDCore.SDK's.</param>
    /// <returns>Its version; <c>0.0.0</c> for an assembly with none at all.</returns>
    public static PlatformVersion VersionOf(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Of(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, assembly.GetName().Version);
    }

    internal static PlatformVersion Of(string? informationalVersion, Version? version) =>
        PlatformVersion.TryParse(informationalVersion, out var stamped) ? stamped : PlatformVersionJsonConverter.Of(version ?? new Version(0, 0, 0));
}

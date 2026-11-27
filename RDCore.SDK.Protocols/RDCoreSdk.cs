using System.Reflection;
using RDCore.SDK.Platform;

namespace RDCore.SDK;

/// <summary>
/// The RDCore.SDK package, as a client knows it: its id, and the version of RDCore.SDK.Protocols, the package of its protocol, that a client or a
/// platform part runs with.
/// </summary>
public static class RDCoreSdk
{
    /// <summary>
    /// The package's id: <c>RDCore.SDK</c>, as a release manifest names it (<see cref="PlatformReleasePackage.Package"/>).
    /// </summary>
    public const string PackageId = "RDCore.SDK";

    /// <summary>
    /// The version of the RDCore.SDK.Protocols this program runs with, such as <c>0.2.1-alpha</c>: the package's version, without build metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RDCore.SDK.Protocols and RDCore.SDK are versioned apart, and an RDCore.SDK depends on an RDCore.SDK.Protocols of its own major and minor
    /// version: the two are compatible (<see cref="PlatformVersion.IsCompatibleWith"/>). A client that references RDCore.SDK.Protocols alone, as an
    /// IDE's add-in does, compares this version with the RDCore.SDK a platform was built with (<see cref="PlatformReleaseManifest.IsBuiltWith"/>).
    /// </para>
    /// <para>
    /// The build stamps it into the assembly's <see cref="AssemblyInformationalVersionAttribute"/>, from the version the package is packed as. An
    /// assembly built without it reports its numeric <see cref="AssemblyName.Version"/>.
    /// </para>
    /// </remarks>
    public static PlatformVersion Version { get; } = Of(typeof(RDCoreSdk).Assembly);

    internal static PlatformVersion Of(Assembly assembly) =>
        Of(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, assembly.GetName().Version);

    internal static PlatformVersion Of(string? informationalVersion, Version? version) =>
        PlatformVersion.TryParse(informationalVersion, out var stamped) ? stamped : PlatformVersion.Parse(Numeric(version));

    private static string Numeric(Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}

using System.Reflection;
using RDCore.SDK;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The version of an RDCore package a program runs with, as the package's build stamps it.
/// </summary>
[TestClass]
public sealed class RDCoreSdkTests
{
    [TestMethod]
    public void TheSdksVersion_IsTheOneItsBuildStamps()
    {
        var built = typeof(RDCoreSdkTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "BuiltVersion").Value;

        Assert.AreEqual(PlatformVersion.Parse(built!), RDCoreSdk.Version);
    }

    // an assembly made here, as a package's build makes one: numbered 0.2.1.0, and stamped with the prerelease it was packed as, or not stamped.
    private static Assembly Assembly(string? stamped)
        => System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Stamped{Guid.NewGuid():N}") { Version = new Version(0, 2, 1, 0) },
            System.Reflection.Emit.AssemblyBuilderAccess.Run,
            stamped is null
                ? []
                : [new System.Reflection.Emit.CustomAttributeBuilder(
                    typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!, [stamped])]);

    [TestMethod]
    public void AnAssemblysVersion_IsTheOneItsPackageStamps()
        => Assert.AreEqual("0.2.1-local.abc1234", RDCoreSdk.VersionOf(Assembly("0.2.1-local.abc1234+2062f9857560619aec056b37ac0181c84c0c6e6a")).Text);

    [TestMethod]
    public void AnAssemblyNotStamped_HasItsNumericVersion()
        => Assert.AreEqual("0.2.1", RDCoreSdk.VersionOf(Assembly(null)).Text);

    [TestMethod]
    public void TheStampedVersion_IsReadWithoutItsBuildMetadata()
        => Assert.AreEqual("0.1.2-local.abc1234", RDCoreSdk.Of("0.1.2-local.abc1234+2062f9857560619aec056b37ac0181c84c0c6e6a", new Version(0, 1, 2, 0)).Text);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a version")]
    public void AnAssemblyWithNoStampedVersion_ReportsItsNumericOne(string? informationalVersion)
        => Assert.AreEqual("0.1.2", RDCoreSdk.Of(informationalVersion, new Version(0, 1, 2, 0)).Text);

    [TestMethod]
    public void AnAssemblyWithNoVersionAtAll_Reports000()
        => Assert.AreEqual("0.0.0", RDCoreSdk.Of(null, null).Text);
}

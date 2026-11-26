using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Workspace.Forms;

[TestClass]
public sealed class FormControlClassTests
{
    [TestMethod]
    [DataRow("VB.CommandButton", "VB", "CommandButton", null, DisplayName = "a library and a class")]
    [DataRow("MSComctlLib.ListView", "MSComctlLib", "ListView", null, DisplayName = "an ActiveX library and a class")]
    [DataRow("Label", null, "Label", null, DisplayName = "a bare name: no library")]
    [DataRow("{8BD21D10-EC42-11CE-9E0D-00AA006002F3}", null, null, "8BD21D10-EC42-11CE-9E0D-00AA006002F3", DisplayName = "a class identifier alone")]
    public void Parse_IsTheClassABeginLineNames(string token, string? library, string? name, string? classId)
        => Assert.AreEqual(new FormControlClass(library, name, classId is null ? null : Guid.Parse(classId)), FormControlClass.Parse(token));

    [TestMethod]
    [DataRow("VB.CommandButton", DisplayName = "a library and a class")]
    [DataRow("Label", DisplayName = "a bare name")]
    [DataRow("{8BD21D10-EC42-11CE-9E0D-00AA006002F3}", DisplayName = "a class identifier, in upper case")]
    public void ToString_WritesTheClassAsABeginLineDoes(string token)
        => Assert.AreEqual(token, FormControlClass.Parse(token).ToString());
}

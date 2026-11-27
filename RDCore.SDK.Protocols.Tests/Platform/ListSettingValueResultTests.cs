using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;
using System.Text.Json;

namespace RDCore.Tests.Platform;

/// <summary>
/// What a client answers a change of a list with travels whole: why it did not apply the change, when it says why.
/// </summary>
[TestClass]
public sealed class ListSettingValueResultTests
{
    [TestMethod]
    [DataRow(true, null, DisplayName = "applied")]
    [DataRow(false, null, DisplayName = "not applied, no reason")]
    [DataRow(false, "The item is not yours to remove.", DisplayName = "not applied, with its reason")]
    public void AResult_IsReadBackAsItWasWritten(bool applied, string? reason)
    {
        var json = JsonSerializer.Serialize(new ListSettingValueResult { Applied = applied, Reason = reason }, RDCorePlatformJson.Options);

        var read = JsonSerializer.Deserialize<ListSettingValueResult>(json, RDCorePlatformJson.Options)!;

        Assert.AreEqual(applied, read.Applied);
        Assert.AreEqual(reason, read.Reason);
    }
}

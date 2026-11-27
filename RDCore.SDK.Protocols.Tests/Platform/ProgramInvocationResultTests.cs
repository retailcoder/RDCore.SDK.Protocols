using RDCore.SDK.Platform.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RDCore.Tests.Platform;

/// <summary>
/// How an invocation of one procedure ended, as its factories make it and as <c>System.Text.Json</c> writes it.
/// </summary>
[TestClass]
public sealed class ProgramInvocationResultTests
{
    [TestMethod]
    public void AReturnedValue_IsACompletedResultWithThatValue_AndASubsIsOneWithNone()
    {
        var value = new JsonObject { ["total"] = 42 };

        var returned = ProgramInvocationResult.Returned(value);
        var completed = ProgramInvocationResult.Completed();

        Assert.AreEqual((ExecutionOutcome.Completed, true), (returned.Outcome, returned.HasValue));
        Assert.AreSame(value, returned.Value);
        Assert.AreEqual((ExecutionOutcome.Completed, false), (completed.Outcome, completed.HasValue));
        Assert.IsNull(completed.Value);
    }

    [TestMethod]
    public void ARunTimeError_CarriesItsNumberDescriptionAndCode()
    {
        var result = ProgramInvocationResult.RuntimeError(13, "Type mismatch", "VBR00013");

        Assert.AreEqual((ExecutionOutcome.RuntimeError, 13, "Type mismatch", "VBR00013"),
            (result.Outcome, result.ErrorNumber, result.ErrorMessage, result.ErrorCode));
        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    [DataRow(ExecutionOutcome.Completed)]
    [DataRow(ExecutionOutcome.RuntimeError)]
    public void AnOtherwiseEndedInvocation_IsNeverCompletedNorARunTimeError(ExecutionOutcome outcome)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ProgramInvocationResult.Ended(outcome, "no"));

    [TestMethod]
    public void AnInterruptedInvocation_CarriesItsExplanation()
    {
        var result = ProgramInvocationResult.Ended(ExecutionOutcome.Interrupted, "the request was cancelled");

        Assert.AreEqual((ExecutionOutcome.Interrupted, "the request was cancelled", false), (result.Outcome, result.ErrorMessage, result.HasValue));
    }

    [TestMethod]
    public void TheValue_IsWrittenAsTheJsonItIs_AndReadBackSo()
    {
        var json = JsonSerializer.Serialize(ProgramInvocationResult.Returned(new JsonObject { ["items"] = new JsonArray(1, "two") }));

        var back = JsonSerializer.Deserialize<ProgramInvocationResult>(json)!;

        Assert.AreEqual("""{"items":[1,"two"]}""", JsonNode.Parse(json)!["Value"]!.ToJsonString(), "the value is JSON in the payload, not a string of it");
        Assert.IsTrue(JsonNode.DeepEquals(new JsonObject { ["items"] = new JsonArray(1, "two") }, back.Value));
        Assert.IsTrue(back.HasValue);
    }
}

using System.Text.Json.Nodes;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// How an invocation of one procedure of a program ended: the value the procedure returned, as JSON, or why it did not complete.
/// </summary>
/// <remarks>
/// A run-time error of the program is a result (<see cref="ExecutionOutcome.RuntimeError"/>), never an exception. So is an argument that does not convert to
/// its parameter's type: run-time error 13 (Type mismatch).
/// <para>
/// RDCore.SDK derives its reply on the JSON-RPC wire from this type, and that reply carries the converter that writes <see cref="Value"/> as JSON.
/// </para>
/// </remarks>
public record class ProgramInvocationResult
{
    /// <summary>
    /// How the invocation ended.
    /// </summary>
    public ExecutionOutcome Outcome { get; init; }

    /// <summary>
    /// Whether the procedure returned a value: a <c>Function</c> or a <c>Property Get</c> that completed. A <c>Sub</c> returns none.
    /// </summary>
    public bool HasValue { get; init; }

    /// <summary>
    /// The value the procedure returned, as JSON: <see langword="null"/>, written as JSON <c>null</c>, for <c>Empty</c>, <c>Null</c> and <c>Nothing</c>,
    /// and when <see cref="HasValue"/> is <see langword="false"/>.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>
    /// The number of the run-time error, as <c>Err.Number</c> reports it, when <see cref="Outcome"/> is <see cref="ExecutionOutcome.RuntimeError"/>; 0
    /// otherwise.
    /// </summary>
    public int ErrorNumber { get; init; }

    /// <summary>
    /// The description of the run-time error, as <c>Err.Description</c> reports it, or a short explanation of any other outcome that is not
    /// <see cref="ExecutionOutcome.Completed"/>.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// The <strong>RD-VBAL §2.6.3</strong> diagnostic code of the run-time error (<c>VBR</c> for one the runtime raised, <c>VBA</c> for one the program
    /// raised); empty for any other outcome.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// A procedure that completed and returned a value.
    /// </summary>
    /// <param name="value">The value, as JSON; <see langword="null"/> for <c>Empty</c>, <c>Null</c> and <c>Nothing</c>.</param>
    /// <returns>A <see cref="ExecutionOutcome.Completed"/> result with a value.</returns>
    public static ProgramInvocationResult Returned(JsonNode? value) => new() { Outcome = ExecutionOutcome.Completed, HasValue = true, Value = value };

    /// <summary>
    /// A procedure that completed and returned no value: a <c>Sub</c>.
    /// </summary>
    /// <returns>A <see cref="ExecutionOutcome.Completed"/> result with no value.</returns>
    public static ProgramInvocationResult Completed() => new() { Outcome = ExecutionOutcome.Completed };

    /// <summary>
    /// A procedure that raised a run-time error that nothing handled.
    /// </summary>
    /// <param name="number">The error's number, as <c>Err.Number</c> reports it.</param>
    /// <param name="description">The error's description, as <c>Err.Description</c> reports it.</param>
    /// <param name="code">The error's <strong>RD-VBAL §2.6.3</strong> diagnostic code.</param>
    /// <returns>A <see cref="ExecutionOutcome.RuntimeError"/> result.</returns>
    public static ProgramInvocationResult RuntimeError(int number, string description, string code)
        => new() { Outcome = ExecutionOutcome.RuntimeError, ErrorNumber = number, ErrorMessage = description, ErrorCode = code };

    /// <summary>
    /// An invocation that ended otherwise, such as a procedure that was not found, a program that an <c>End</c> statement halted or that was interrupted,
    /// or an invocation the machine refused.
    /// </summary>
    /// <param name="outcome">How it ended: neither <see cref="ExecutionOutcome.Completed"/> nor <see cref="ExecutionOutcome.RuntimeError"/>.</param>
    /// <param name="explanation">A short explanation of the outcome.</param>
    /// <returns>A result of that outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is <see cref="ExecutionOutcome.Completed"/> or <see cref="ExecutionOutcome.RuntimeError"/>, which
    /// <see cref="Returned"/>, <see cref="Completed"/> and <see cref="RuntimeError"/> make.
    /// </exception>
    public static ProgramInvocationResult Ended(ExecutionOutcome outcome, string explanation)
        => outcome is ExecutionOutcome.Completed or ExecutionOutcome.RuntimeError
            ? throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
            : new() { Outcome = outcome, ErrorMessage = explanation };
}

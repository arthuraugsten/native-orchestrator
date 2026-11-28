namespace NativeOrchestrator.App.ApiManagement;

/// <summary>An operation's status, as <see cref="IApiManagementService.GetStatusAsync"/> reads it.</summary>
/// <param name="State">Whether it is running, succeeded or failed.</param>
/// <param name="ResourceId">What the operation produced, when it succeeded.</param>
/// <param name="Error">Why it failed, when it failed.</param>
public sealed record ApiOperationStatus(ApiOperationState State, string? ResourceId = null, string? Error = null)
{
  /// <summary>
  /// The polling step's answer: keep polling, succeed, or fail permanently. A successful operation that produced no
  /// resource fails, since the next step needs it.
  /// </summary>
  public PollResult ToPollResult() => State switch
  {
    ApiOperationState.Succeeded when string.IsNullOrWhiteSpace(ResourceId) => PollResult.Failed("The operation succeeded but returned no resource ID."),
    ApiOperationState.Succeeded => PollResult.Succeeded(),
    ApiOperationState.Failed => PollResult.Failed(Error ?? "The operation failed."),
    _ => PollResult.Pending(),
  };
}

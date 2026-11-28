using NativeOrchestrator.App.ApiManagement;

namespace NativeOrchestrator.App.Sagas.Client;

/// <summary>
/// Starts provisioning, polls until it finishes, and keeps the provisioned resource's ID. The operation ID is the step
/// state, saved between checks. Compensation reverts the provisioning.
/// </summary>
/// <remarks>
/// The compensation runs when a later step fails or times out, or the saga is cancelled or exceeds its maximum duration,
/// after this step succeeded.
/// </remarks>
public sealed class ProvisionClientStep : PollingSagaStep<ClientSagaData, string>
{
  private readonly IApiManagementService _apiManagement;

  public ProvisionClientStep(IApiManagementService apiManagement)
  {
    _apiManagement = apiManagement;
  }

  protected override Task<string> StartAsync(StepContext<ClientSagaData> context, CancellationToken cancellationToken) =>
    _apiManagement.StartProvisioningAsync(context.Data.RunName, context.IdempotencyKey, cancellationToken);

  protected override async Task<PollResult> CheckAsync(StepContext<ClientSagaData> context, string operation, CancellationToken cancellationToken)
  {
    var status = await _apiManagement.GetStatusAsync(operation, cancellationToken);

    if (status.State == ApiOperationState.Succeeded)
      context.Data.ProvisioningId = status.ResourceId;

    return status.ToPollResult();
  }

  public override async Task CompensateAsync(StepContext<ClientSagaData> context, CancellationToken cancellationToken)
  {
    // The polling step saved the operation ID as its state.
    if (!context.TryGetState<string>(out var operation))
      return;

    await _apiManagement.RevertAsync(operation, cancellationToken);
  }
}

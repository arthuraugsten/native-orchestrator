using NativeOrchestrator.App.ApiManagement;

namespace NativeOrchestrator.App.Sagas.Client;

/// <summary>
/// Starts synchronizing the provisioned client, polls until it finishes, and keeps the synchronization's ID. The
/// operation ID is the step state. Compensation reverts the synchronization.
/// </summary>
/// <remarks>
/// The engine compensates only steps that succeeded, so the compensation runs when a step declared after this one
/// fails. When this step itself fails or times out, nothing is reverted.
/// </remarks>
public sealed class SynchronizeClientStep : PollingSagaStep<ClientSagaData, string>
{
  private readonly IApiManagementService _apiManagement;

  public SynchronizeClientStep(IApiManagementService apiManagement)
  {
    _apiManagement = apiManagement;
  }

  protected override Task<string> StartAsync(StepContext<ClientSagaData> context, CancellationToken cancellationToken)
  {
    var provisioningId = context.Data.ProvisioningId
      ?? throw new StepFailedException("The client has no provisioning ID: ProvisionClient must run first.");

    return _apiManagement.StartSynchronizationAsync(context.Data.RunName, provisioningId, context.IdempotencyKey, cancellationToken);
  }

  protected override async Task<PollResult> CheckAsync(StepContext<ClientSagaData> context, string operation, CancellationToken cancellationToken)
  {
    var status = await _apiManagement.GetStatusAsync(operation, cancellationToken);

    if (status.State == ApiOperationState.Succeeded)
      context.Data.SynchronizationId = status.ResourceId;

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

namespace NativeOrchestrator.App.Sagas.Client;

/// <summary>
/// Provisions the client, then synchronizes it, both through API Management. Each is a long-running operation (up to
/// about 5 hours) that is polled until it finishes. Started by the <see cref="ScheduleName"/> schedule; one run at a time.
/// </summary>
/// <remarks>
/// If <see cref="SynchronizeClientStep"/> fails, <see cref="ProvisionClientStep"/> is reverted, and the saga ends
/// <see cref="SagaStatus.Compensated"/> with the failure recorded (or <see cref="SagaStatus.Failed"/> if the revert
/// fails after its retries). Step timeouts and polling are in configuration (<c>SagaOrchestrator:Sagas:client</c>).
/// </remarks>
public sealed class ClientSaga : Saga<ClientSagaData>
{
  public const string Name = "client";

  public const string ScheduleName = "client-every-5-minutes";

  protected override void Configure(ISagaBuilder<ClientSagaData> saga) => saga
    .Step<ProvisionClientStep>()
    .Step<SynchronizeClientStep>();

  protected override string? Validate(ClientSagaData data) =>
    string.IsNullOrWhiteSpace(data.RunName) ? "runName is required." : null;
}

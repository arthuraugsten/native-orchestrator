namespace NativeOrchestrator.App.ApiManagement;

/// <summary>
/// The long-running operations behind API Management. A start returns an operation ID right away; the operation then
/// runs in the external system (hours in real life) until <see cref="GetStatusAsync"/> reports it finished.
/// </summary>
public interface IApiManagementService
{
  /// <summary>Starts provisioning. A repeat with the same <paramref name="idempotencyKey"/> returns the same operation.</summary>
  /// <param name="runName">The run the operation belongs to.</param>
  /// <param name="idempotencyKey">The step's <see cref="StepContext{TData}.IdempotencyKey"/>.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  Task<string> StartProvisioningAsync(string runName, string idempotencyKey, CancellationToken cancellationToken);

  /// <summary>Starts synchronization. A repeat with the same <paramref name="idempotencyKey"/> returns the same operation.</summary>
  /// <param name="runName">The run the operation belongs to.</param>
  /// <param name="provisioningId">The resource the provisioning operation created.</param>
  /// <param name="idempotencyKey">The step's <see cref="StepContext{TData}.IdempotencyKey"/>.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  Task<string> StartSynchronizationAsync(string runName, string provisioningId, string idempotencyKey, CancellationToken cancellationToken);

  /// <summary>Reads an operation's status. Safe to call any number of times.</summary>
  /// <param name="operationId">The ID a start returned.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  Task<ApiOperationStatus> GetStatusAsync(string operationId, CancellationToken cancellationToken);

  /// <summary>Reverts an operation's effect (compensation). Idempotent: an operation already gone counts as reverted.</summary>
  /// <param name="operationId">The ID a start returned.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  Task RevertAsync(string operationId, CancellationToken cancellationToken);
}

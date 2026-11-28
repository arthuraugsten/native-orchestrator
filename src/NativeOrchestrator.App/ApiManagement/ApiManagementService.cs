using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.App.ApiManagement;

/// <summary>
/// A simulated <see cref="IApiManagementService"/> for the lab: each operation is kept in memory with a random delay up
/// to <see cref="ApiManagementOptions.MaxDuration"/>, and its status is running until that delay has passed. Replace it
/// with an implementation that calls the real gateway.
/// </summary>
/// <remarks>
/// Operations are lost when the app restarts, so a saga that was polling then fails its step (unknown operation). A run
/// name containing <see cref="FailProvisioning"/> or <see cref="FailSynchronization"/> makes that operation fail.
/// </remarks>
public sealed class ApiManagementService : IApiManagementService
{
  /// <summary>A run name containing this fails provisioning.</summary>
  public const string FailProvisioning = "FAIL-PROVISION";

  /// <summary>A run name containing this fails synchronization.</summary>
  public const string FailSynchronization = "FAIL-SYNC";

  // Keyed by idempotency key, which is also the operation ID, so a repeated start returns the same operation.
  private readonly ConcurrentDictionary<string, Operation> _operations = new();
  private readonly TimeProvider _timeProvider;
  private readonly ApiManagementOptions _options;

  public ApiManagementService(TimeProvider timeProvider, IOptions<ApiManagementOptions> options)
  {
    _timeProvider = timeProvider;
    _options = options.Value;
  }

  public Task<string> StartProvisioningAsync(string runName, string idempotencyKey, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(runName);

    return Start("prov", runName.Contains(FailProvisioning, StringComparison.OrdinalIgnoreCase), idempotencyKey);
  }

  public Task<string> StartSynchronizationAsync(string runName, string provisioningId, string idempotencyKey, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(runName);
    ArgumentException.ThrowIfNullOrWhiteSpace(provisioningId);

    return Start("sync", runName.Contains(FailSynchronization, StringComparison.OrdinalIgnoreCase), idempotencyKey);
  }

  public Task<ApiOperationStatus> GetStatusAsync(string operationId, CancellationToken cancellationToken)
  {
    if (!_operations.TryGetValue(operationId, out var operation))
      throw new StepFailedException($"The operation '{operationId}' doesn't exist.");

    var status = _timeProvider.GetUtcNow() < operation.DueAtUtc
      ? new ApiOperationStatus(ApiOperationState.Running)
      : operation.Fails
        ? new ApiOperationStatus(ApiOperationState.Failed, Error: $"The operation '{operationId}' failed in the external system.")
        : new ApiOperationStatus(ApiOperationState.Succeeded, ResourceId: operation.ResourceId);

    return Task.FromResult(status);
  }

  public Task RevertAsync(string operationId, CancellationToken cancellationToken)
  {
    _operations.TryRemove(operationId, out _);

    return Task.CompletedTask;
  }

  private Task<string> Start(string prefix, bool fails, string idempotencyKey)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

    _operations.GetOrAdd(idempotencyKey, _ =>
    {
      var delay = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * _options.MaxDuration.TotalMilliseconds);

      return new Operation(_timeProvider.GetUtcNow() + delay, fails, $"{prefix}-{Guid.NewGuid():N}"[..13]);
    });

    return Task.FromResult(idempotencyKey);
  }

  private sealed record Operation(DateTimeOffset DueAtUtc, bool Fails, string ResourceId);
}

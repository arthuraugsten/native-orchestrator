namespace NativeOrchestrator;

/// <summary>
/// Prepares a store before the worker starts claiming sagas (for example, applies database migrations). The worker
/// runs every registered initializer first.
/// </summary>
public interface ISagaStoreInitializer
{
  /// <summary>Prepares the store.</summary>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task InitializeAsync(CancellationToken cancellationToken);
}

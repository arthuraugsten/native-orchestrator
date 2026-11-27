using NativeOrchestrator.Data.InMemory;

namespace NativeOrchestrator;

/// <summary>Adds the in-memory saga store.</summary>
public static class InMemoryNativeOrchestratorBuilderExtensions
{
  /// <summary>
  /// Stores sagas in memory with <see cref="InMemorySagaStore"/>: for tests and local experiments only, since it works
  /// within one process and nothing survives a restart.
  /// </summary>
  /// <param name="builder">The orchestrator builder.</param>
  public static NativeOrchestratorBuilder UseInMemoryStore(this NativeOrchestratorBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    return builder.UseStore<InMemorySagaStore>();
  }
}

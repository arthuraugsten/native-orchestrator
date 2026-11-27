namespace NativeOrchestrator;

/// <summary>
/// Thrown by <see cref="ISagaWorkQueue.SaveAsync"/> when the lease was lost or the saga changed since it was read
/// (for example, cancellation was requested). The engine drops its changes; the saga runs again from its stored state.
/// </summary>
public sealed class SagaConcurrencyException : Exception
{
  /// <summary>Creates the exception.</summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="message">What happened.</param>
  /// <param name="innerException">The store's exception, if any.</param>
  public SagaConcurrencyException(Guid sagaId, string message, Exception? innerException = null)
    : base(message, innerException)
  {
    SagaId = sagaId;
  }

  /// <summary>The saga's identifier.</summary>
  public Guid SagaId { get; }
}

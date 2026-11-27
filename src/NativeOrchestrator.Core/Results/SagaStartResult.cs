namespace NativeOrchestrator;

/// <summary>What <see cref="ISagaClient.StartAsync(string, System.Text.Json.JsonElement, string?, CancellationToken)"/> returns.</summary>
/// <param name="SagaId">
/// The saga's identifier: the new saga's, or, if none was created, the identifier of the saga that already existed.
/// </param>
/// <param name="Outcome">Whether a saga was created, and if not, why.</param>
public sealed record SagaStartResult(Guid SagaId, SagaStartOutcome Outcome)
{
  /// <summary>Whether a new saga was created.</summary>
  public bool Created => Outcome == SagaStartOutcome.Created;
}

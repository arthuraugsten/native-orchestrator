namespace NativeOrchestrator;

/// <summary>What a saga's hooks see: the saga after it was saved.</summary>
/// <typeparam name="TData">The saga's data.</typeparam>
public sealed class SagaContext<TData>
  where TData : class
{
  private readonly SagaRecord _saga;

  internal SagaContext(SagaRecord saga, TData data, IServiceProvider services)
  {
    _saga = saga;
    Data = data;
    Services = services;
  }

  /// <summary>The saga's identifier.</summary>
  public Guid SagaId => _saga.Id;

  /// <summary>The saga type name.</summary>
  public string SagaType => _saga.SagaType;

  /// <summary>The caller's correlation identifier, if one was given.</summary>
  public string? CorrelationId => _saga.CorrelationId;

  /// <summary>The saga's status.</summary>
  public SagaStatus Status => _saga.Status;

  /// <summary>When the saga was created.</summary>
  public DateTimeOffset CreatedAtUtc => _saga.CreatedAtUtc;

  /// <summary>When the saga finished.</summary>
  public DateTimeOffset? CompletedAtUtc => _saga.CompletedAtUtc;

  /// <summary>The saga's data.</summary>
  public TData Data { get; }

  /// <summary>The DI scope the hook runs in.</summary>
  public IServiceProvider Services { get; }
}

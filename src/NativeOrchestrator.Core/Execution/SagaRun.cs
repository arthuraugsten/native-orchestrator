using Microsoft.Extensions.Logging;

namespace NativeOrchestrator;

/// <summary>One run of a claimed saga: the record being changed, its saga type, and the history recorded so far.</summary>
internal sealed class SagaRun
{
  private readonly TimeProvider _timeProvider;
  private readonly int _maxErrorLength;
  private readonly ILogger _logger;

  public SagaRun(
    SagaRecord saga,
    ISagaRuntime runtime,
    SagaOptions options,
    IServiceProvider services,
    TimeProvider timeProvider,
    int maxErrorLength,
    ILogger logger)
  {
    Saga = saga;
    Runtime = runtime;
    Options = options;
    Services = services;
    _timeProvider = timeProvider;
    _maxErrorLength = maxErrorLength;
    _logger = logger;
  }

  public SagaRecord Saga { get; }

  public ISagaRuntime Runtime { get; }

  public SagaOptions Options { get; }

  public IServiceProvider Services { get; }

  /// <summary>History recorded since the last save.</summary>
  public List<SagaHistoryEntry> PendingHistory { get; } = [];

  public DateTimeOffset Now => _timeProvider.GetUtcNow();

  public void Record(SagaEventType eventType, string? stepName = null, string? details = null) =>
    PendingHistory.Add(new SagaHistoryEntry(Saga.Id, Saga.SagaType, Now, eventType, stepName, details));

  public string Truncate(string message) =>
    message.Length <= _maxErrorLength ? message : string.Concat(message.AsSpan(0, _maxErrorLength - 3), "...");

  /// <summary>Ends the run: the saga is saved as due at <paramref name="dueAtUtc"/>.</summary>
  public void WaitUntil(DateTimeOffset dueAtUtc)
  {
    if (Saga.Status != SagaStatus.Compensating)
      Saga.Status = SagaStatus.Waiting;
    Saga.NextRunAtUtc = dueAtUtc;
  }

  public void BeginCompensation(SagaFailureKind kind, string? stepName, string error)
  {
    error = Truncate(error);
    foreach (var step in Saga.Steps)
    {
      if (step.Status != StepStatus.InProgress)
        continue;
      step.Status = StepStatus.Failed;
      step.CompletedAtUtc = Now;
      step.Error = error;
    }

    Saga.Status = SagaStatus.Compensating;
    Saga.FailureKind = kind;
    Saga.FailureStep = stepName;
    Saga.Error = error;
    Saga.NextRunAtUtc = Now;
    Record(SagaEventType.CompensationStarted, stepName, $"{kind}: {error}");
    SagaLog.SagaCompensating(_logger, Saga.Id, Saga.SagaType, kind, error);
  }

  public void Finish(SagaStatus status)
  {
    Saga.Status = status;
    Saga.CompletedAtUtc = Now;
    Saga.NextRunAtUtc = null;
    // A failed saga needs manual action, so by default it keeps blocking new starts of a single-instance saga until it
    // is resolved.
    if (status != SagaStatus.Failed || !Options.FailedBlocksNewStarts)
      Saga.ActiveInstanceKey = null;
    var eventType = status switch
    {
      SagaStatus.Completed => SagaEventType.Completed,
      SagaStatus.Compensated => SagaEventType.Compensated,
      _ => SagaEventType.Failed,
    };
    Record(eventType);
  }
}

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

internal sealed class SagaClient : ISagaClient
{
  private const int MaxCorrelationIdLength = 200;

  private readonly SagaRegistry _registry;
  private readonly IOptionsMonitor<SagaOptions> _sagaOptions;
  private readonly ISagaRepository _repository;
  private readonly IServiceScopeFactory _scopes;
  private readonly SagaWorkSignal _signal;
  private readonly TimeProvider _timeProvider;

  public SagaClient(
    SagaRegistry registry,
    IOptionsMonitor<SagaOptions> sagaOptions,
    ISagaRepository repository,
    IServiceScopeFactory scopes,
    SagaWorkSignal signal,
    TimeProvider timeProvider)
  {
    _registry = registry;
    _sagaOptions = sagaOptions;
    _repository = repository;
    _scopes = scopes;
    _signal = signal;
    _timeProvider = timeProvider;
  }

  public Task<SagaStartResult> StartAsync<TData>(string sagaType, TData data, string? correlationId = null, CancellationToken cancellationToken = default)
    where TData : class
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
    ArgumentNullException.ThrowIfNull(data);

    var runtime = _registry.Get(sagaType);

    using var scope = _scopes.CreateScope();

    return CreateAsync(sagaType, runtime.PrepareData(data, scope.ServiceProvider), correlationId, cancellationToken);
  }

  public Task<SagaStartResult> StartAsync(string sagaType, JsonElement data, string? correlationId = null, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);

    var runtime = _registry.Get(sagaType);

    using var scope = _scopes.CreateScope();

    return CreateAsync(sagaType, runtime.PrepareData(data, scope.ServiceProvider), correlationId, cancellationToken);
  }

  public async Task<SagaSnapshot?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
  {
    var saga = await _repository.GetAsync(sagaId, cancellationToken).ConfigureAwait(false);

    return saga is null ? null : ToSnapshot(saga);
  }

  public Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken = default) =>
    _repository.GetHistoryAsync(sagaId, cancellationToken);

  public async Task<CancellationRequestResult> CancelAsync(Guid sagaId, CancellationToken cancellationToken = default)
  {
    var result = await _repository.RequestCancellationAsync(sagaId, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

    if (result == CancellationRequestResult.Requested)
      _signal.Notify();

    return result;
  }

  private async Task<SagaStartResult> CreateAsync(string sagaType, string dataJson, string? correlationId, CancellationToken cancellationToken)
  {
    if (correlationId is not null && (correlationId.Length == 0 || correlationId.Length > MaxCorrelationIdLength))
      throw new ArgumentException($"The correlation ID must be 1 to {MaxCorrelationIdLength} characters.", nameof(correlationId));

    var now = _timeProvider.GetUtcNow();
    var activeInstanceKey = _sagaOptions.Get(sagaType).SingleInstance ? sagaType : null;
    var saga = SagaRecord.Create(Guid.CreateVersion7(now), sagaType, correlationId, activeInstanceKey, dataJson, now);
    var created = new SagaHistoryEntry(saga.Id, sagaType, now, SagaEventType.Created, null, correlationId is null ? null : $"Correlation ID: {correlationId}");

    try
    {
      await _repository.CreateAsync(saga, created, cancellationToken).ConfigureAwait(false);
    }
    catch (DuplicateSagaException ex)
    {
      var outcome = ex.Reason == DuplicateSagaReason.ActiveInstance ? SagaStartOutcome.AlreadyRunning : SagaStartOutcome.Duplicate;

      return new SagaStartResult(ex.ExistingSagaId, outcome);
    }

    _signal.Notify();

    return new SagaStartResult(saga.Id, SagaStartOutcome.Created);
  }

  private SagaSnapshot ToSnapshot(SagaRecord saga)
  {
    using var data = JsonDocument.Parse(saga.DataJson);
    var steps = new List<StepSnapshot>(saga.Steps.Count);

    foreach (var step in saga.Steps.OrderBy(s => s.StepIndex))
      steps.Add(new StepSnapshot(step.Name, step.Status, step.Attempt, step.CheckCount, step.StartedAtUtc, step.CompletedAtUtc, step.Error));

    var failure = saga.FailureKind is { } kind ? new SagaFailure(kind, saga.FailureStep, saga.Error ?? "") : null;

    return new SagaSnapshot(
      saga.Id,
      saga.SagaType,
      saga.CorrelationId,
      saga.Status,
      CurrentStep(saga),
      data.RootElement.Clone(),
      saga.CreatedAtUtc,
      saga.StartedAtUtc,
      saga.CompletedAtUtc,
      saga.NextRunAtUtc,
      saga.CancellationRequested,
      failure,
      steps);
  }

  private string? CurrentStep(SagaRecord saga)
  {
    if (saga.Status.IsFinished())
      return null;

    if (saga.Status == SagaStatus.Compensating)
      return saga.FindLatestSucceededStep()?.Name;

    if (_registry.TryGet(saga.SagaType, out var runtime) && saga.CurrentStepIndex < runtime.Steps.Count)
      return runtime.Steps[saga.CurrentStepIndex].Name;

    return saga.FindStep(saga.CurrentStepIndex)?.Name;
  }
}

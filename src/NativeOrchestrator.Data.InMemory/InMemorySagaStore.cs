namespace NativeOrchestrator.Data.InMemory;

/// <summary>
/// A store that keeps sagas in memory, for tests and local experiments. It follows the same contract as a database
/// store (leases, versions, correlation IDs) but works within one process only, and nothing survives a restart.
/// </summary>
public sealed class InMemorySagaStore : ISagaRepository, ISagaWorkQueue
{
  private readonly Lock _lock = new();
  private readonly Dictionary<Guid, Entry> _sagas = [];
  private readonly Dictionary<(string SagaType, string CorrelationId), Guid> _correlations = [];
  private readonly Dictionary<string, Guid> _activeInstances = [];
  private readonly List<SagaHistoryEntry> _history = [];

  /// <inheritdoc />
  public Task CreateAsync(SagaRecord saga, SagaHistoryEntry created, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(saga);
    ArgumentNullException.ThrowIfNull(created);
    lock (_lock)
    {
      if (_sagas.ContainsKey(saga.Id))
        throw new InvalidOperationException($"A saga with ID {saga.Id} already exists.");
      if (saga.CorrelationId is not null)
      {
        if (_correlations.TryGetValue((saga.SagaType, saga.CorrelationId), out var existing))
          throw new DuplicateSagaException(existing);
      }

      if (saga.ActiveInstanceKey is not null && _activeInstances.TryGetValue(saga.ActiveInstanceKey, out var active))
        throw new DuplicateSagaException(active, DuplicateSagaReason.ActiveInstance);

      if (saga.CorrelationId is not null)
        _correlations.Add((saga.SagaType, saga.CorrelationId), saga.Id);
      if (saga.ActiveInstanceKey is not null)
        _activeInstances.Add(saga.ActiveInstanceKey, saga.Id);
      _sagas.Add(saga.Id, new Entry(saga.Clone()));
      _history.Add(created);
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task<SagaRecord?> GetAsync(Guid sagaId, CancellationToken cancellationToken)
  {
    lock (_lock)
      return Task.FromResult(_sagas.TryGetValue(sagaId, out var entry) ? entry.Saga.Clone() : null);
  }

  /// <inheritdoc />
  public Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken)
  {
    lock (_lock)
      return Task.FromResult<IReadOnlyList<SagaHistoryEntry>>([.. _history.Where(h => h.SagaId == sagaId)]);
  }

  /// <inheritdoc />
  public Task<CancellationRequestResult> RequestCancellationAsync(Guid sagaId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
  {
    lock (_lock)
    {
      if (!_sagas.TryGetValue(sagaId, out var entry))
        return Task.FromResult(CancellationRequestResult.NotFound);
      return Task.FromResult(entry.Saga.RequestCancellation(nowUtc));
    }
  }

  /// <inheritdoc />
  public Task<IReadOnlyList<ClaimedSaga>> ClaimDueAsync(SagaClaimRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    lock (_lock)
    {
      var due = _sagas.Values
        .Where(e => SagaStatuses.Active.Contains(e.Saga.Status)
          && e.Saga.NextRunAtUtc <= request.NowUtc
          && (e.LeaseExpiresAtUtc is null || e.LeaseExpiresAtUtc < request.NowUtc))
        .OrderBy(e => e.Saga.NextRunAtUtc)
        .Take(request.MaxCount)
        .ToArray();

      var claimed = new List<ClaimedSaga>(due.Length);
      foreach (var entry in due)
      {
        entry.LeaseToken = Guid.NewGuid();
        entry.LeaseExpiresAtUtc = request.LeaseExpiresAtUtc;
        claimed.Add(new ClaimedSaga(entry.Saga.Clone(), new SagaLease(entry.Saga.Id, entry.LeaseToken.Value, request.Owner)));
      }

      return Task.FromResult<IReadOnlyList<ClaimedSaga>>(claimed);
    }
  }

  /// <inheritdoc />
  public Task<bool> RenewLeaseAsync(SagaLease lease, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    lock (_lock)
    {
      if (!_sagas.TryGetValue(lease.SagaId, out var entry) || entry.LeaseToken != lease.Token)
        return Task.FromResult(false);
      entry.LeaseExpiresAtUtc = expiresAtUtc;
      return Task.FromResult(true);
    }
  }

  /// <inheritdoc />
  public Task SaveAsync(SagaLease lease, SagaRecord saga, IReadOnlyList<SagaHistoryEntry> history, bool releaseLease, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    ArgumentNullException.ThrowIfNull(saga);
    ArgumentNullException.ThrowIfNull(history);
    lock (_lock)
    {
      if (!_sagas.TryGetValue(saga.Id, out var entry) || entry.LeaseToken != lease.Token)
        throw new SagaConcurrencyException(saga.Id, $"The lease on saga {saga.Id} was lost.");
      if (entry.Saga.Version != saga.Version)
        throw new SagaConcurrencyException(saga.Id, $"Saga {saga.Id} changed since it was read (version {saga.Version}, now {entry.Saga.Version}).");

      var newKey = saga.ActiveInstanceKey;
      if (newKey is not null && _activeInstances.TryGetValue(newKey, out var holder) && holder != saga.Id)
        throw new InvalidOperationException($"Another saga ({holder}) is active with the key '{newKey}'.");
      if (entry.Saga.ActiveInstanceKey is { } oldKey && oldKey != newKey)
        _activeInstances.Remove(oldKey);
      if (newKey is not null)
        _activeInstances[newKey] = saga.Id;

      saga.IncrementVersion();
      entry.Saga = saga.Clone();
      _history.AddRange(history);
      if (releaseLease)
      {
        entry.LeaseToken = null;
        entry.LeaseExpiresAtUtc = null;
      }
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task ReleaseLeaseAsync(SagaLease lease, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    lock (_lock)
    {
      if (_sagas.TryGetValue(lease.SagaId, out var entry) && entry.LeaseToken == lease.Token)
      {
        entry.LeaseToken = null;
        entry.LeaseExpiresAtUtc = null;
      }
    }

    return Task.CompletedTask;
  }

  private sealed class Entry
  {
    public Entry(SagaRecord saga)
    {
      Saga = saga;
    }

    public SagaRecord Saga { get; set; }

    public Guid? LeaseToken { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
  }
}

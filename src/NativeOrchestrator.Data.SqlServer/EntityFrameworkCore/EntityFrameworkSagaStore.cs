using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// The saga store on EF Core, for any relational provider. Each operation uses its own short-lived context, so no
/// connection or transaction stays open while a saga waits.
/// </summary>
internal sealed class EntityFrameworkSagaStore : ISagaRepository, ISagaWorkQueue
{
  private static readonly SagaStatus[] Cancellable = [.. SagaStatuses.Cancellable];

  private readonly IDbContextFactory<SagaDbContext> _contexts;
  private readonly ISagaClaimStrategy _claimStrategy;

  /// <summary>Creates the store.</summary>
  /// <param name="contexts">Creates contexts.</param>
  /// <param name="claimStrategy">Leases due sagas.</param>
  public EntityFrameworkSagaStore(IDbContextFactory<SagaDbContext> contexts, ISagaClaimStrategy claimStrategy)
  {
    ArgumentNullException.ThrowIfNull(contexts);
    ArgumentNullException.ThrowIfNull(claimStrategy);
    _contexts = contexts;
    _claimStrategy = claimStrategy;
  }

  /// <inheritdoc />
  public async Task CreateAsync(SagaRecord saga, SagaHistoryEntry created, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(saga);
    ArgumentNullException.ThrowIfNull(created);
    // Usual case for a repeated start: the saga exists already, so don't attempt (and log) a failing insert.
    if (await FindDuplicateAsync(saga, cancellationToken).ConfigureAwait(false) is { } existing)
      throw existing;

    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    context.Sagas.Add(saga);
    context.History.Add(created);
    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException ex) when (saga.CorrelationId is not null || saga.ActiveInstanceKey is not null)
    {
      // Two starts raced: a unique index ((SagaType, CorrelationId) or ActiveInstanceKey) rejected this one. Detected
      // without provider-specific error codes, by looking for the saga that won.
      if (await FindDuplicateAsync(saga, cancellationToken).ConfigureAwait(false) is { } winner)
        throw new DuplicateSagaException(winner.ExistingSagaId, winner.Reason, ex);
      throw;
    }
  }

  private async Task<DuplicateSagaException?> FindDuplicateAsync(SagaRecord saga, CancellationToken cancellationToken)
  {
    if (saga.CorrelationId is null && saga.ActiveInstanceKey is null)
      return null;

    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    if (saga.CorrelationId is not null)
    {
      var sameCorrelation = await context.Sagas
        .Where(s => s.SagaType == saga.SagaType && s.CorrelationId == saga.CorrelationId)
        .Select(s => (Guid?)s.Id)
        .FirstOrDefaultAsync(cancellationToken)
        .ConfigureAwait(false);
      if (sameCorrelation is { } id)
        return new DuplicateSagaException(id, DuplicateSagaReason.CorrelationId);
    }

    if (saga.ActiveInstanceKey is not null)
    {
      var active = await context.Sagas
        .Where(s => s.ActiveInstanceKey == saga.ActiveInstanceKey)
        .Select(s => (Guid?)s.Id)
        .FirstOrDefaultAsync(cancellationToken)
        .ConfigureAwait(false);
      if (active is { } id)
        return new DuplicateSagaException(id, DuplicateSagaReason.ActiveInstance);
    }

    return null;
  }

  /// <inheritdoc />
  public async Task<SagaRecord?> GetAsync(Guid sagaId, CancellationToken cancellationToken)
  {
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    return await context.Sagas
      .AsNoTracking()
      .Include(s => s.Steps.OrderBy(step => step.StepIndex))
      .SingleOrDefaultAsync(s => s.Id == sagaId, cancellationToken)
      .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken)
  {
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    return await context.History
      .AsNoTracking()
      .Where(h => h.SagaId == sagaId)
      .OrderBy(h => EF.Property<long>(h, SagaModel.HistoryId))
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<CancellationRequestResult> RequestCancellationAsync(Guid sagaId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
  {
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    var updated = await context.Sagas
      .Where(s => s.Id == sagaId && Cancellable.Contains(s.Status) && !s.CancellationRequested)
      .ExecuteUpdateAsync(
        set => set
          .SetProperty(s => s.CancellationRequested, true)
          .SetProperty(s => s.NextRunAtUtc, nowUtc)
          .SetProperty(s => s.UpdatedAtUtc, nowUtc)
          .SetProperty(s => s.Version, s => s.Version + 1),
        cancellationToken)
      .ConfigureAwait(false);
    if (updated == 1)
      return CancellationRequestResult.Requested;

    var current = await context.Sagas
      .Where(s => s.Id == sagaId)
      .Select(s => new { s.Status, s.CancellationRequested })
      .SingleOrDefaultAsync(cancellationToken)
      .ConfigureAwait(false);
    if (current is null)
      return CancellationRequestResult.NotFound;
    return current.CancellationRequested && Cancellable.Contains(current.Status)
      ? CancellationRequestResult.AlreadyRequested
      : CancellationRequestResult.NotCancellable;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ClaimedSaga>> ClaimDueAsync(SagaClaimRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    var token = Guid.NewGuid();
    var leased = await _claimStrategy.ClaimAsync(context, request, token, cancellationToken).ConfigureAwait(false);
    if (leased == 0)
      return [];

    var sagas = await context.Sagas
      .AsNoTracking()
      .Include(s => s.Steps.OrderBy(step => step.StepIndex))
      .Where(s => EF.Property<Guid?>(s, SagaModel.LeaseToken) == token)
      .OrderBy(s => s.NextRunAtUtc)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    var claimed = new ClaimedSaga[sagas.Count];
    for (var i = 0; i < sagas.Count; i++)
      claimed[i] = new ClaimedSaga(sagas[i], new SagaLease(sagas[i].Id, token, request.Owner));
    return claimed;
  }

  /// <inheritdoc />
  public async Task<bool> RenewLeaseAsync(SagaLease lease, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    var updated = await context.Sagas
      .Where(s => s.Id == lease.SagaId && EF.Property<Guid?>(s, SagaModel.LeaseToken) == lease.Token)
      .ExecuteUpdateAsync(set => set.SetProperty(s => EF.Property<DateTimeOffset?>(s, SagaModel.LeaseExpiresAtUtc), expiresAtUtc), cancellationToken)
      .ConfigureAwait(false);
    return updated == 1;
  }

  /// <inheritdoc />
  public async Task SaveAsync(SagaLease lease, SagaRecord saga, IReadOnlyList<SagaHistoryEntry> history, bool releaseLease, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    ArgumentNullException.ThrowIfNull(saga);
    ArgumentNullException.ThrowIfNull(history);
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    var stored = await context.Sagas
      .Include(s => s.Steps)
      .SingleOrDefaultAsync(s => s.Id == saga.Id && EF.Property<Guid?>(s, SagaModel.LeaseToken) == lease.Token, cancellationToken)
      .ConfigureAwait(false)
      ?? throw new SagaConcurrencyException(saga.Id, $"The lease on saga {saga.Id} was lost.");
    if (stored.Version != saga.Version)
      throw new SagaConcurrencyException(saga.Id, $"Saga {saga.Id} changed since it was read (version {saga.Version}, now {stored.Version}).");

    var entry = context.Entry(stored);
    entry.CurrentValues.SetValues(saga);
    entry.Property(s => s.Version).OriginalValue = saga.Version;
    stored.IncrementVersion();
    if (releaseLease)
    {
      entry.Property(SagaModel.LeaseToken).CurrentValue = null;
      entry.Property(SagaModel.LeaseOwner).CurrentValue = null;
      entry.Property(SagaModel.LeaseExpiresAtUtc).CurrentValue = null;
    }

    foreach (var step in saga.Steps)
    {
      var storedStep = stored.FindStep(step.StepIndex);
      if (storedStep is null)
        stored.AddStep(step.Clone());
      else
        context.Entry(storedStep).CurrentValues.SetValues(step);
    }

    context.History.AddRange(history);
    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateConcurrencyException ex)
    {
      throw new SagaConcurrencyException(saga.Id, $"Saga {saga.Id} changed while it was being saved.", ex);
    }

    saga.IncrementVersion();
  }

  /// <inheritdoc />
  public async Task ReleaseLeaseAsync(SagaLease lease, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lease);
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    await context.Sagas
      .Where(s => s.Id == lease.SagaId && EF.Property<Guid?>(s, SagaModel.LeaseToken) == lease.Token)
      .ExecuteUpdateAsync(
        set => set
          .SetProperty(s => EF.Property<Guid?>(s, SagaModel.LeaseToken), (Guid?)null)
          .SetProperty(s => EF.Property<string?>(s, SagaModel.LeaseOwner), (string?)null)
          .SetProperty(s => EF.Property<DateTimeOffset?>(s, SagaModel.LeaseExpiresAtUtc), (DateTimeOffset?)null),
        cancellationToken)
      .ConfigureAwait(false);
  }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.App.Simulated;

/// <summary>The in-memory state of the simulated external systems. Requests with the same idempotency key get the same result.</summary>
public sealed class SimulatedExternalSystems
{
  private readonly ConcurrentDictionary<string, Account> _accountsByKey = new();
  private readonly ConcurrentDictionary<string, Account> _accounts = new();
  private readonly ConcurrentDictionary<string, int> _flakyAttempts = new();
  private readonly ConcurrentDictionary<string, Verification> _verificationsByKey = new();
  private readonly ConcurrentDictionary<string, Verification> _verifications = new();
  private readonly ConcurrentDictionary<string, Report> _reportsByKey = new();
  private readonly ConcurrentDictionary<string, Report> _reports = new();
  private readonly ConcurrentQueue<SentEmail> _emails = new();
  private readonly TimeProvider _timeProvider;
  private readonly SimulatedOptions _options;

  public SimulatedExternalSystems(TimeProvider timeProvider, IOptions<SimulatedOptions> options)
  {
    _timeProvider = timeProvider;
    _options = options.Value;
  }

  public TimeSpan RetryAfter => _options.RetryAfter;

  public IReadOnlyCollection<SentEmail> Emails => _emails;

  /// <summary>Returns <see langword="false"/> while a flaky request should still fail.</summary>
  public bool PassFlakyCheck(string email, string idempotencyKey) =>
    !email.StartsWith(SimulatedTriggers.FlakyEmailPrefix, StringComparison.OrdinalIgnoreCase)
      || _flakyAttempts.AddOrUpdate(idempotencyKey, 1, (_, attempts) => attempts + 1) > 2;

  public Account CreateAccount(CreateAccountRequest request, string idempotencyKey) =>
    _accountsByKey.GetOrAdd(idempotencyKey, _ =>
    {
      var account = new Account($"acc-{Guid.NewGuid():N}"[..12], request.CustomerName, request.Email);
      _accounts[account.Id] = account;
      return account;
    });

  public Account? FindAccount(string id) => _accounts.GetValueOrDefault(id);

  public bool DeleteAccount(string id) => _accounts.TryRemove(id, out _);

  public Verification StartVerification(VerificationRequest request, string idempotencyKey) =>
    _verificationsByKey.GetOrAdd(idempotencyKey, _ =>
    {
      var verification = new Verification($"ver-{Guid.NewGuid():N}"[..12], request.CustomerName, _timeProvider.GetUtcNow());
      _verifications[verification.Id] = verification;
      return verification;
    });

  /// <summary>The verification's status: <c>pending</c>, <c>approved</c> or <c>rejected</c>, or <see langword="null"/> if unknown.</summary>
  public VerificationResponse? GetVerification(string id)
  {
    if (!_verifications.TryGetValue(id, out var verification))
      return null;
    var name = verification.CustomerName;
    if (name.Contains(SimulatedTriggers.SlowCustomer, StringComparison.OrdinalIgnoreCase)
      || _timeProvider.GetUtcNow() - verification.StartedAtUtc < _options.IdentityCheckDuration)
      return new VerificationResponse(id, "pending", null);
    return name.Contains(SimulatedTriggers.RejectedCustomer, StringComparison.OrdinalIgnoreCase)
      ? new VerificationResponse(id, "rejected", "The identity document doesn't match the customer.")
      : new VerificationResponse(id, "approved", null);
  }

  public Report StartReport(ReportRequest request, string idempotencyKey) =>
    _reportsByKey.GetOrAdd(idempotencyKey, _ =>
    {
      var report = new Report($"rep-{Guid.NewGuid():N}"[..12], request.Name, TimeSpan.FromSeconds(request.DurationSeconds), _timeProvider.GetUtcNow());
      _reports[report.Id] = report;
      return report;
    });

  /// <summary>The report's status: <c>running</c>, <c>succeeded</c> or <c>failed</c>, or <see langword="null"/> if unknown or discarded.</summary>
  public ReportResponse? GetReport(string id, string baseUrl)
  {
    if (!_reports.TryGetValue(id, out var report))
      return null;
    if (_timeProvider.GetUtcNow() - report.StartedAtUtc < report.Duration)
      return new ReportResponse(id, "running", null, null);
    return report.Name.StartsWith(SimulatedTriggers.FailingReportPrefix, StringComparison.OrdinalIgnoreCase)
      ? new ReportResponse(id, "failed", null, "The report query timed out on the warehouse.")
      : new ReportResponse(id, "succeeded", $"{baseUrl}/simulated/reports/{id}/file", null);
  }

  public bool DiscardReport(string id) => _reports.TryRemove(id, out _);

  public void SendEmail(EmailRequest request) =>
    _emails.Enqueue(new SentEmail(request.To, request.Subject, _timeProvider.GetUtcNow()));

  public sealed record Account(string Id, string CustomerName, string Email)
  {
    public bool Active { get; set; }
  }

  public sealed record Verification(string Id, string CustomerName, DateTimeOffset StartedAtUtc);

  public sealed record Report(string Id, string Name, TimeSpan Duration, DateTimeOffset StartedAtUtc);
}

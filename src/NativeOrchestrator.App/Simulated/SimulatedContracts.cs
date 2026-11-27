namespace NativeOrchestrator.App.Simulated;

// Request and response bodies of the simulated external systems. The sagas' steps use the same records.

public sealed record CreateAccountRequest(string CustomerName, string Email);

public sealed record AccountResponse(string AccountId, string Status);

public sealed record VerificationRequest(string CustomerName);

public sealed record VerificationResponse(string VerificationId, string Status, string? Reason);

public sealed record EmailRequest(string To, string Subject, string Body);

public sealed record ReportRequest(string Name, int DurationSeconds);

public sealed record ReportResponse(string ReportId, string Status, string? DownloadUrl, string? Error);

public sealed record SentEmail(string To, string Subject, DateTimeOffset SentAtUtc);

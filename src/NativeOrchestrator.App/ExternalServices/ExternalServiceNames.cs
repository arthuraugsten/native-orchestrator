namespace NativeOrchestrator.App.ExternalServices;

/// <summary>Names of the <see cref="HttpClient"/>s for the external systems the sagas call.</summary>
public static class ExternalServiceNames
{
  public const string Identity = "identity";
  public const string Accounts = "accounts";
  public const string Notifications = "notifications";
  public const string Reports = "reports";
}

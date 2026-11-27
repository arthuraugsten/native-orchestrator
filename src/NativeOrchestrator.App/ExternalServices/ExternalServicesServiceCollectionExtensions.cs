using Microsoft.Extensions.Options;

namespace NativeOrchestrator.App.ExternalServices;

public static class ExternalServicesServiceCollectionExtensions
{
  /// <summary>
  /// Registers a named <see cref="HttpClient"/> per external system, each with Microsoft's standard resilience
  /// pipeline (attempt timeout, short retries, circuit breaker, total timeout), configured from <paramref name="resilience"/>.
  /// </summary>
  /// <remarks>
  /// These retries cover seconds within one step call. Longer outages are handled by the saga's durable retries
  /// (<c>RetryOptions</c>), which survive restarts. Unsafe methods (POST) are retried too: every request carries the
  /// step's <c>Idempotency-Key</c>, so the external system can ignore repeats.
  /// </remarks>
  public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration, IConfigurationSection resilience)
  {
    services.AddOptions<ExternalServicesOptions>().Bind(configuration).ValidateOnStart();
    services.AddSingleton<IValidateOptions<ExternalServicesOptions>, ExternalServicesOptionsValidator>();

    AddClient(services, ExternalServiceNames.Identity, options => options.Identity, resilience);
    AddClient(services, ExternalServiceNames.Accounts, options => options.Accounts, resilience);
    AddClient(services, ExternalServiceNames.Notifications, options => options.Notifications, resilience);
    AddClient(services, ExternalServiceNames.Reports, options => options.Reports, resilience);
    return services;
  }

  private static void AddClient(IServiceCollection services, string name, Func<ExternalServicesOptions, Uri?> baseUrl, IConfigurationSection resilience)
  {
    services.AddHttpClient(name, (provider, client) =>
        client.BaseAddress = baseUrl(provider.GetRequiredService<IOptions<ExternalServicesOptions>>().Value))
      .AddStandardResilienceHandler(resilience);
  }
}

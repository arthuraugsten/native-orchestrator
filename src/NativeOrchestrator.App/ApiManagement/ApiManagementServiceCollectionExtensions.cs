namespace NativeOrchestrator.App.ApiManagement;

public static class ApiManagementServiceCollectionExtensions
{
  /// <summary>
  /// Registers the simulated <see cref="IApiManagementService"/> as a singleton (it holds the operations in memory), with
  /// <see cref="ApiManagementOptions"/> from <paramref name="configuration"/>, validated at startup.
  /// </summary>
  public static IServiceCollection AddApiManagement(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<ApiManagementOptions>()
      .Bind(configuration)
      .Validate(options => options.MaxDuration >= TimeSpan.Zero, "ApiManagement:MaxDuration must not be negative. Set it to a time span such as 00:00:20.")
      .ValidateOnStart();

    services.AddSingleton<IApiManagementService, ApiManagementService>();

    return services;
  }
}

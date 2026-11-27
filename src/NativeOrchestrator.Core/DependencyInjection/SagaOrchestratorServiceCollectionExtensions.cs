using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NativeOrchestrator;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Adds the orchestrator to a service collection.</summary>
public static class NativeOrchestratorServiceCollectionExtensions
{
  /// <summary>
  /// Adds the orchestrator: the worker, the engine and <see cref="ISagaClient"/>. Then add sagas and a store with the
  /// returned builder.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configure">Sets <see cref="NativeOrchestratorOptions"/> in code.</param>
  public static NativeOrchestratorBuilder AddNativeOrchestrator(this IServiceCollection services, Action<NativeOrchestratorOptions>? configure = null)
  {
    ArgumentNullException.ThrowIfNull(services);

    AddCore(services);

    if (configure is not null)
      services.Configure(configure);

    return new NativeOrchestratorBuilder(services, null);
  }

  /// <summary>
  /// Adds the orchestrator with options bound from configuration: <see cref="NativeOrchestratorOptions"/> from the
  /// section itself, every saga's <see cref="SagaOptions"/> from <c>SagaDefaults</c>, each saga's own from
  /// <c>Sagas:{name}</c>, and each schedule's <see cref="SagaScheduleOptions"/> from <c>Schedules:{name}</c>.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The configuration section, for example <c>builder.Configuration.GetSection("NativeOrchestrator")</c>.</param>
  public static NativeOrchestratorBuilder AddNativeOrchestrator(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    AddCore(services);

    services.AddOptions<NativeOrchestratorOptions>().Bind(configuration);

    var defaults = configuration.GetSection("SagaDefaults");

    services.ConfigureAll<SagaOptions>(options => defaults.Bind(options));

    return new NativeOrchestratorBuilder(services, configuration);
  }

  private static void AddCore(IServiceCollection services)
  {
    services.AddOptions<NativeOrchestratorOptions>().ValidateOnStart();
    services.AddOptions<SagaSerializerOptions>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<NativeOrchestratorOptions>, NativeOrchestratorOptionsValidator>());
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SagaOptions>, SagaOptionsValidator>());
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SagaScheduleOptions>, SagaScheduleOptionsValidator>());

    services.TryAddSingleton(TimeProvider.System);
    services.TryAddSingleton<ISagaSerializer, SystemTextJsonSagaSerializer>();
    services.TryAddSingleton<IBackoffStrategy, ExponentialBackoffStrategy>();
    services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();

    services.TryAddSingleton<SagaRegistry>();
    services.TryAddSingleton<SagaDiagnostics>();
    services.TryAddSingleton<SagaWorkSignal>();
    services.TryAddSingleton<SagaStepExecutor>();
    services.TryAddSingleton<CompensationRunner>();
    services.TryAddSingleton<SagaEngine>();
    services.TryAddSingleton<ISagaClient, SagaClient>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<Hosting.IHostedService, SagaWorker>());
    services.TryAddSingleton<SagaScheduleRegistry>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<Hosting.IHostedService, SagaScheduler>());
  }
}

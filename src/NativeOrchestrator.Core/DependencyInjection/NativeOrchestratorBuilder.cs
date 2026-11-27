using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NativeOrchestrator;

/// <summary>Adds sagas, a store and customisations to the orchestrator. Returned by <c>AddNativeOrchestrator</c>.</summary>
public sealed class NativeOrchestratorBuilder
{
  private const int MaxSagaNameLength = 100;

  private readonly IConfiguration? _configuration;
  private readonly HashSet<string> _sagaNames = new(StringComparer.Ordinal);

  internal NativeOrchestratorBuilder(IServiceCollection services, IConfiguration? configuration)
  {
    Services = services;
    _configuration = configuration;
  }

  /// <summary>The service collection, for store packages that extend the builder.</summary>
  public IServiceCollection Services { get; }

  /// <summary>Registers a saga type.</summary>
  /// <typeparam name="TSaga">The saga class.</typeparam>
  /// <typeparam name="TData">The saga's data.</typeparam>
  /// <param name="name">
  /// The saga type name: stored with every saga, used in the API and as the configuration key (<c>Sagas:{name}</c>).
  /// Keep it stable once sagas are running. Defaults to the class name without a <c>Saga</c> suffix.
  /// </param>
  /// <param name="configure">Sets the saga's options in code. Configuration overrides them.</param>
  public NativeOrchestratorBuilder AddSaga<TSaga, TData>(string? name = null, Action<SagaOptions>? configure = null)
    where TSaga : Saga<TData>
    where TData : class
  {
    name ??= DefaultName(typeof(TSaga));
    if (string.IsNullOrWhiteSpace(name) || name.Length > MaxSagaNameLength)
      throw new ArgumentException($"Saga names must be 1 to {MaxSagaNameLength} characters ('{name}').", nameof(name));
    if (!_sagaNames.Add(name))
      throw new InvalidOperationException($"A saga named '{name}' is already registered. Pass a different name to AddSaga.");

    var options = Services.AddOptions<SagaOptions>(name);
    if (configure is not null)
      options.Configure(configure);
    if (_configuration is not null)
      options.Bind(_configuration.GetSection("Sagas").GetSection(name));
    options.ValidateOnStart();

    Services.AddSingleton(new SagaRegistration(name, typeof(TSaga), services => new SagaRuntime<TSaga, TData>(name, services)));
    return this;
  }

  /// <summary>Uses a store that implements both <see cref="ISagaRepository"/> and <see cref="ISagaWorkQueue"/>, as a singleton.</summary>
  /// <typeparam name="TStore">The store.</typeparam>
  public NativeOrchestratorBuilder UseStore<TStore>()
    where TStore : class, ISagaRepository, ISagaWorkQueue
  {
    Services.RemoveAll<ISagaRepository>();
    Services.RemoveAll<ISagaWorkQueue>();
    Services.TryAddSingleton<TStore>();
    Services.AddSingleton<ISagaRepository>(services => services.GetRequiredService<TStore>());
    Services.AddSingleton<ISagaWorkQueue>(services => services.GetRequiredService<TStore>());
    return this;
  }

  /// <summary>Uses <see cref="InMemorySagaStore"/>: for tests and local experiments only, since nothing survives a restart.</summary>
  public NativeOrchestratorBuilder UseInMemoryStore() => UseStore<InMemorySagaStore>();

  /// <summary>Adds a lifecycle observer, as a singleton.</summary>
  /// <typeparam name="TObserver">The observer.</typeparam>
  public NativeOrchestratorBuilder AddObserver<TObserver>()
    where TObserver : class, ISagaLifecycleObserver
  {
    Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISagaLifecycleObserver, TObserver>());
    return this;
  }

  /// <summary>Replaces the retry policy, as a singleton.</summary>
  /// <typeparam name="TPolicy">The policy.</typeparam>
  public NativeOrchestratorBuilder UseRetryPolicy<TPolicy>()
    where TPolicy : class, IRetryPolicy
  {
    Services.RemoveAll<IRetryPolicy>();
    Services.AddSingleton<IRetryPolicy, TPolicy>();
    return this;
  }

  /// <summary>Replaces the backoff strategy, as a singleton.</summary>
  /// <typeparam name="TStrategy">The strategy.</typeparam>
  public NativeOrchestratorBuilder UseBackoffStrategy<TStrategy>()
    where TStrategy : class, IBackoffStrategy
  {
    Services.RemoveAll<IBackoffStrategy>();
    Services.AddSingleton<IBackoffStrategy, TStrategy>();
    return this;
  }

  /// <summary>Replaces the serializer, as a singleton.</summary>
  /// <typeparam name="TSerializer">The serializer.</typeparam>
  public NativeOrchestratorBuilder UseSerializer<TSerializer>()
    where TSerializer : class, ISagaSerializer
  {
    Services.RemoveAll<ISagaSerializer>();
    Services.AddSingleton<ISagaSerializer, TSerializer>();
    return this;
  }

  /// <summary>
  /// Configures the default serializer's options, for example to add a source-generated context:
  /// <c>o => o.TypeInfoResolverChain.Insert(0, MyJsonContext.Default)</c>.
  /// </summary>
  /// <param name="configure">Changes the options.</param>
  public NativeOrchestratorBuilder ConfigureSerializer(Action<JsonSerializerOptions> configure)
  {
    ArgumentNullException.ThrowIfNull(configure);
    Services.Configure<SagaSerializerOptions>(options => configure(options.SerializerOptions));
    return this;
  }

  private static string DefaultName(Type sagaType)
  {
    const string suffix = "Saga";
    var name = sagaType.Name;
    return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
  }
}

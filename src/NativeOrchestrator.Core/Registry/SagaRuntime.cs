using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator;

internal sealed class SagaRuntime<TSaga, TData> : ISagaRuntime
  where TSaga : Saga<TData>
  where TData : class
{
  private readonly ISagaSerializer _serializer;
  private readonly TimeProvider _timeProvider;
  private readonly Dictionary<string, StepDefinition> _stepsByName;

  public SagaRuntime(string name, IServiceProvider services)
  {
    Name = name;
    _serializer = services.GetRequiredService<ISagaSerializer>();
    _timeProvider = services.GetRequiredService<TimeProvider>();

    using var scope = services.CreateScope();

    var saga = CreateSaga(scope.ServiceProvider);
    var builder = new SagaBuilder<TData>(name);

    saga.InvokeConfigure(builder);
    Steps = builder.Build();

    _stepsByName = [with(StringComparer.OrdinalIgnoreCase)];

    foreach (var step in Steps)
      _stepsByName.Add(step.Name, step);
  }

  public string Name { get; }

  public Type DataType => typeof(TData);

  public IReadOnlyList<StepDefinition> Steps { get; }

  public StepDefinition? FindStep(string name) => _stepsByName.GetValueOrDefault(name);

  public string PrepareData(JsonElement data, IServiceProvider services)
  {
    TData value;
    try
    {
      value = _serializer.Deserialize<TData>(data.GetRawText());
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
    {
      throw new ArgumentException($"The data isn't a valid {typeof(TData).Name}: {ex.Message}", ex);
    }

    return Prepare(value, services);
  }

  public string PrepareData<T>(T data, IServiceProvider services)
  {
    if (data is not TData value)
      throw new ArgumentException($"Saga '{Name}' takes {typeof(TData).Name} data, not {typeof(T).Name}.", nameof(data));
    return Prepare(value, services);
  }

  public async Task<StepResult> ExecuteStepAsync(
    SagaRecord saga,
    StepRecord step,
    StepDefinition definition,
    IServiceProvider services,
    CancellationToken cancellationToken)
  {
    var data = _serializer.Deserialize<TData>(saga.DataJson);
    var context = new StepContext<TData>(saga, step, data, false, _serializer, services, _timeProvider);
    var instance = (SagaStep<TData>)definition.Factory(services, null);
    try
    {
      var result = await instance.ExecuteAsync(context, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Step '{definition.Name}' returned null. Return a StepResult.");
      if (result.Kind != StepResultKind.Failure)
      {
        saga.UpdateData(_serializer.Serialize(data));
        step.UpdateState(context.StateJson);
      }

      return result;
    }
    finally
    {
      await DisposeAsync(instance).ConfigureAwait(false);
    }
  }

  public async Task CompensateStepAsync(
    SagaRecord saga,
    StepRecord step,
    StepDefinition definition,
    IServiceProvider services,
    CancellationToken cancellationToken)
  {
    var data = _serializer.Deserialize<TData>(saga.DataJson);
    var context = new StepContext<TData>(saga, step, data, true, _serializer, services, _timeProvider);
    var instance = (SagaStep<TData>)definition.Factory(services, null);
    try
    {
      await instance.CompensateAsync(context, cancellationToken).ConfigureAwait(false);
      saga.UpdateData(_serializer.Serialize(data));
      step.UpdateState(context.StateJson);
    }
    finally
    {
      await DisposeAsync(instance).ConfigureAwait(false);
    }
  }

  public async ValueTask OnFinishedAsync(SagaRecord saga, IServiceProvider services, CancellationToken cancellationToken)
  {
    var data = _serializer.Deserialize<TData>(saga.DataJson);
    var context = new SagaContext<TData>(saga, data, services);
    var instance = CreateSaga(services);
    if (saga.Status == SagaStatus.Completed)
    {
      await instance.InvokeOnCompletedAsync(context, cancellationToken).ConfigureAwait(false);
      return;
    }

    var failure = new SagaFailure(saga.FailureKind ?? SagaFailureKind.StepFailed, saga.FailureStep, saga.Error ?? "");
    await instance.InvokeOnFailedAsync(context, failure, cancellationToken).ConfigureAwait(false);
  }

  private string Prepare(TData data, IServiceProvider services)
  {
    var error = CreateSaga(services).InvokeValidate(data);
    if (error is not null)
      throw new ArgumentException(error);
    return _serializer.Serialize(data);
  }

  private static TSaga CreateSaga(IServiceProvider services) =>
    ActivatorUtilities.GetServiceOrCreateInstance<TSaga>(services);

  private static async ValueTask DisposeAsync(object instance)
  {
    if (instance is IAsyncDisposable asyncDisposable)
      await asyncDisposable.DisposeAsync().ConfigureAwait(false);
    else if (instance is IDisposable disposable)
      disposable.Dispose();
  }
}

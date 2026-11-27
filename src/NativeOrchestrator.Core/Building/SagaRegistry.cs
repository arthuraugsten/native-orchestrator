using System.Diagnostics.CodeAnalysis;

namespace NativeOrchestrator;

/// <summary>
/// Every registered saga type. Building it calls each saga's <c>Configure</c> and validates the steps, so a bad
/// declaration fails at startup (the worker depends on the registry).
/// </summary>
internal sealed class SagaRegistry
{
  private readonly Dictionary<string, ISagaRuntime> _runtimes = new(StringComparer.Ordinal);

  public SagaRegistry(IEnumerable<SagaRegistration> registrations, IServiceProvider services)
  {
    foreach (var registration in registrations)
    {
      if (_runtimes.ContainsKey(registration.Name))
        throw new InvalidOperationException($"Two sagas are registered as '{registration.Name}'. Give each AddSaga call a different name.");
      _runtimes.Add(registration.Name, registration.CreateRuntime(services));
    }
  }

  public IEnumerable<string> Names => _runtimes.Keys;

  public bool TryGet(string sagaType, [NotNullWhen(true)] out ISagaRuntime? runtime) =>
    _runtimes.TryGetValue(sagaType, out runtime);

  public ISagaRuntime Get(string sagaType) =>
    _runtimes.TryGetValue(sagaType, out var runtime) ? runtime : throw new UnknownSagaTypeException(sagaType);
}

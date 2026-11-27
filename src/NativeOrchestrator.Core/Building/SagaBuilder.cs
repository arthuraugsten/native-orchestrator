using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator;

internal sealed class SagaBuilder<TData> : ISagaBuilder<TData>
  where TData : class
{
  private const int MaxStepNameLength = 100;

  private readonly string _sagaName;
  private readonly List<StepDefinition> _steps = [];
  private readonly HashSet<string> _names = [with(StringComparer.OrdinalIgnoreCase)];

  public SagaBuilder(string sagaName)
  {
    _sagaName = sagaName;
  }

  public ISagaBuilder<TData> Step<TStep>(string? name = null, Action<StepOptions>? configure = null)
    where TStep : SagaStep<TData>
  {
    if (typeof(TStep).IsAbstract)
      throw new InvalidOperationException($"Saga '{_sagaName}': step class {typeof(TStep).Name} is abstract. Add a concrete step class.");

    name ??= DefaultName(typeof(TStep));

    if (string.IsNullOrWhiteSpace(name) || name.Length > MaxStepNameLength)
      throw new InvalidOperationException($"Saga '{_sagaName}': step names must be 1 to {MaxStepNameLength} characters ('{name}').");

    if (!_names.Add(name))
      throw new InvalidOperationException($"Saga '{_sagaName}' has two steps named '{name}'. Give one of them a different name: Step<{typeof(TStep).Name}>(\"another-name\").");

    var options = new StepOptions();
    configure?.Invoke(options);

    List<string> errors = [];

    SagaOptionsValidator.ValidateStep(errors, $"Saga '{_sagaName}', step '{name}'", options);

    if (errors.Count > 0)
      throw new InvalidOperationException(string.Join(" ", errors));

    var factory = ActivatorUtilities.CreateFactory(typeof(TStep), Type.EmptyTypes);

    _steps.Add(new StepDefinition(_steps.Count, name, typeof(TStep), factory, options));

    return this;
  }

  public IReadOnlyList<StepDefinition> Build()
  {
    if (_steps.Count == 0)
      throw new InvalidOperationException($"Saga '{_sagaName}' has no steps. Add at least one with saga.Step<TStep>() in Configure.");

    return [.. _steps];
  }

  private static string DefaultName(Type stepType)
  {
    const string suffix = "Step";

    var name = stepType.Name;

    return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
  }
}

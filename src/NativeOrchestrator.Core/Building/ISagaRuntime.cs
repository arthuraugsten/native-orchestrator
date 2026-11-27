using System.Text.Json;

namespace NativeOrchestrator;

/// <summary>
/// One registered saga type, without its generic parameters, so the engine can run any saga. Implemented by
/// <see cref="SagaRuntime{TSaga, TData}"/>.
/// </summary>
internal interface ISagaRuntime
{
  string Name { get; }

  IReadOnlyList<StepDefinition> Steps { get; }

  StepDefinition? FindStep(string name);

  /// <summary>Validates start data given as JSON and returns it serialized. Throws <see cref="ArgumentException"/> if it is invalid.</summary>
  string PrepareData(JsonElement data, IServiceProvider services);

  /// <summary>Validates typed start data and returns it serialized. Throws <see cref="ArgumentException"/> if it is invalid.</summary>
  string PrepareData<T>(T data, IServiceProvider services);

  /// <summary>Runs a step. Saves the data and step state into the records unless the step fails.</summary>
  Task<StepResult> ExecuteStepAsync(SagaRecord saga, StepRecord step, StepDefinition definition, IServiceProvider services, CancellationToken cancellationToken);

  /// <summary>Compensates a step. Saves the data into the record if it succeeds.</summary>
  Task CompensateStepAsync(SagaRecord saga, StepRecord step, StepDefinition definition, IServiceProvider services, CancellationToken cancellationToken);

  /// <summary>Calls the saga's completion or failure hook.</summary>
  ValueTask OnFinishedAsync(SagaRecord saga, IServiceProvider services, CancellationToken cancellationToken);
}

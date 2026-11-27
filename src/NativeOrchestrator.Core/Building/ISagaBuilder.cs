namespace NativeOrchestrator;

/// <summary>Declares a saga's steps, in <see cref="Saga{TData}.Configure"/>.</summary>
/// <typeparam name="TData">The saga's data.</typeparam>
public interface ISagaBuilder<TData>
  where TData : class
{
  /// <summary>Adds a step. Steps run in the order they are added.</summary>
  /// <typeparam name="TStep">The step class. It is created from DI for each run.</typeparam>
  /// <param name="name">
  /// The step's name, unique within the saga. It is stored with the saga and used as the configuration key, so keep it
  /// stable once sagas are running. Defaults to the class name without a <c>Step</c> suffix.
  /// </param>
  /// <param name="configure">Sets step options in code. Configuration (<c>Sagas:{saga}:Steps:{step}</c>) overrides them.</param>
  ISagaBuilder<TData> Step<TStep>(string? name = null, Action<StepOptions>? configure = null)
    where TStep : SagaStep<TData>;
}

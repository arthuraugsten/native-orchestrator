namespace NativeOrchestrator;

/// <summary>A saga type added with <c>AddSaga</c>. The registry turns each into an <see cref="ISagaRuntime"/> at startup.</summary>
internal sealed record SagaRegistration(string Name, Type SagaClass, Func<IServiceProvider, ISagaRuntime> CreateRuntime);

namespace NativeOrchestrator;

/// <summary>Why a saga failed.</summary>
/// <param name="Kind">The kind of failure.</param>
/// <param name="StepName">The step that failed, or <see langword="null"/> for a saga-level failure (timeout, cancellation).</param>
/// <param name="Error">A description of the failure.</param>
public sealed record SagaFailure(SagaFailureKind Kind, string? StepName, string Error);

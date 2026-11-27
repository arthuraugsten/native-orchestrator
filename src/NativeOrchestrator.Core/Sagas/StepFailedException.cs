namespace NativeOrchestrator;

/// <summary>
/// Thrown by a step to fail permanently, without retries: the same as returning <see cref="StepResult.Failure"/>.
/// Useful where a step can't return a result, such as a polling step's <c>StartAsync</c>.
/// </summary>
public sealed class StepFailedException : Exception
{
  /// <summary>Creates the exception.</summary>
  /// <param name="message">Why the step failed.</param>
  /// <param name="innerException">The cause, if any.</param>
  public StepFailedException(string message, Exception? innerException = null)
    : base(message, innerException)
  {
  }
}

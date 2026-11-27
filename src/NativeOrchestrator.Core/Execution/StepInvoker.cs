namespace NativeOrchestrator;

/// <summary>Calls step code with its execution timeout, and turns what it throws into a result.</summary>
internal static class StepInvoker
{
  /// <summary>
  /// Runs <paramref name="call"/>. Returns <see langword="null"/> on success, or the exception it threw (a timeout
  /// becomes a <see cref="TimeoutException"/>). Rethrows only when <paramref name="runToken"/> is cancelled, which
  /// means the lease was lost or the host is stopping.
  /// </summary>
  public static async Task<Exception?> InvokeAsync(
    Func<CancellationToken, Task> call,
    TimeSpan executionTimeout,
    TimeProvider timeProvider,
    CancellationToken runToken)
  {
    using var timeout = new CancellationTokenSource(executionTimeout, timeProvider);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(runToken, timeout.Token);
    try
    {
      await call(linked.Token).ConfigureAwait(false);
      return null;
    }
    catch (OperationCanceledException) when (runToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
    {
      return new TimeoutException($"The call took longer than its execution timeout of {executionTimeout}.", ex);
    }
    catch (Exception ex)
    {
      // Step code is user code calling external systems: anything it throws is a step failure, not an engine bug.
      return ex;
    }
  }
}

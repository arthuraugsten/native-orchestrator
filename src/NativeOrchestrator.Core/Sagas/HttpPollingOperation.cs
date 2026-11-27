namespace NativeOrchestrator;

/// <summary>An operation started over HTTP: the absolute URL to poll for its status.</summary>
/// <param name="StatusUri">The status URL.</param>
public sealed record HttpPollingOperation(Uri StatusUri);

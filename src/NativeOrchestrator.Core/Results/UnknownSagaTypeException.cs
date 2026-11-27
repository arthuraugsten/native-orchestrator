namespace NativeOrchestrator;

/// <summary>Thrown when a saga type name isn't registered with <c>AddSaga</c>.</summary>
public sealed class UnknownSagaTypeException : Exception
{
  /// <summary>Creates the exception.</summary>
  /// <param name="sagaType">The saga type name that isn't registered.</param>
  public UnknownSagaTypeException(string sagaType)
    : base($"No saga type named '{sagaType}' is registered. Register it with AddSaga under that name.")
  {
    SagaType = sagaType;
  }

  /// <summary>The saga type name that isn't registered.</summary>
  public string SagaType { get; }
}

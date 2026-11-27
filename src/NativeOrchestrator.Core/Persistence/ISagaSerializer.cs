namespace NativeOrchestrator;

/// <summary>Turns saga data and step state into stored text and back. The default uses System.Text.Json.</summary>
public interface ISagaSerializer
{
  /// <summary>Serializes a value.</summary>
  /// <typeparam name="T">The value's type.</typeparam>
  /// <param name="value">The value.</param>
  string Serialize<T>(T value);

  /// <summary>Deserializes a value.</summary>
  /// <typeparam name="T">The value's type.</typeparam>
  /// <param name="text">The serialized value.</param>
  /// <exception cref="InvalidOperationException">The text is <c>null</c> once deserialized.</exception>
  T Deserialize<T>(string text);
}

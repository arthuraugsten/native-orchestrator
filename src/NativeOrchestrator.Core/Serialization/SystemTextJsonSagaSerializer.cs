using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

/// <summary>The default <see cref="ISagaSerializer"/>: System.Text.Json, configured by <see cref="SagaSerializerOptions"/>.</summary>
public sealed class SystemTextJsonSagaSerializer : ISagaSerializer
{
  private readonly JsonSerializerOptions _options;

  /// <summary>Creates the serializer.</summary>
  /// <param name="options">The serializer options.</param>
  public SystemTextJsonSagaSerializer(IOptions<SagaSerializerOptions> options)
  {
    ArgumentNullException.ThrowIfNull(options);
    _options = options.Value.SerializerOptions;
    _options.MakeReadOnly();
  }

  /// <inheritdoc />
  public string Serialize<T>(T value) => JsonSerializer.Serialize(value, GetTypeInfo<T>());

  /// <inheritdoc />
  public T Deserialize<T>(string text)
  {
    ArgumentNullException.ThrowIfNull(text);
    return JsonSerializer.Deserialize(text, GetTypeInfo<T>())
      ?? throw new InvalidOperationException($"The stored {typeof(T).Name} is null.");
  }

  private JsonTypeInfo<T> GetTypeInfo<T>() => _options.GetTypeInfo<T>();
}

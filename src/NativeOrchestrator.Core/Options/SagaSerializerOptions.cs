using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NativeOrchestrator;

/// <summary>
/// Options for the default <see cref="ISagaSerializer"/>. Add a source-generated <c>JsonSerializerContext</c> for your
/// saga data and step state types with <see cref="SagaOrchestratorBuilder.ConfigureSerializer"/>.
/// </summary>
public sealed class SagaSerializerOptions
{
  /// <summary>The serializer options. Web defaults (camelCase), resolving source-generated contexts first, then reflection.</summary>
  public JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
  {
    TypeInfoResolver = JsonTypeInfoResolver.Combine(CoreJsonContext.Default, new DefaultJsonTypeInfoResolver()),
  };
}

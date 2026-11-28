using System.Text.Json;
using System.Text.Json.Serialization;

namespace NativeOrchestrator;

/// <summary>Source-generated JSON metadata for the step state types the library defines.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HttpPollingOperation))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;

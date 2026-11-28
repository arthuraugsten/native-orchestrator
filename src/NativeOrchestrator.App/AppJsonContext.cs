using System.Text.Json;
using System.Text.Json.Serialization;
using NativeOrchestrator.App.Api;
using NativeOrchestrator.App.Sagas.Client;

namespace NativeOrchestrator.App;

/// <summary>
/// Source-generated JSON for every type the app serializes: API bodies, saga data and step state (stored by the
/// orchestrator). Add new types here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StartSagaRequest))]
[JsonSerializable(typeof(StartSagaResponse))]
[JsonSerializable(typeof(CancelSagaResponse))]
[JsonSerializable(typeof(SagaSnapshot))]
[JsonSerializable(typeof(IReadOnlyList<SagaHistoryEntry>))]
[JsonSerializable(typeof(ClientSagaData))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

using System.Text.Json;
using System.Text.Json.Serialization;
using NativeOrchestrator.App.Api;
using NativeOrchestrator.App.Sagas.CustomerOnboarding;
using NativeOrchestrator.App.Sagas.ReportGeneration;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App;

/// <summary>
/// Source-generated JSON for every type the app serializes: API bodies, saga data (stored by the orchestrator) and the
/// external systems' bodies. Add new types here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StartSagaRequest))]
[JsonSerializable(typeof(StartSagaResponse))]
[JsonSerializable(typeof(CancelSagaResponse))]
[JsonSerializable(typeof(SagaSnapshot))]
[JsonSerializable(typeof(IReadOnlyList<SagaHistoryEntry>))]
[JsonSerializable(typeof(CustomerOnboardingData))]
[JsonSerializable(typeof(ReportGenerationData))]
[JsonSerializable(typeof(CreateAccountRequest))]
[JsonSerializable(typeof(AccountResponse))]
[JsonSerializable(typeof(VerificationRequest))]
[JsonSerializable(typeof(VerificationResponse))]
[JsonSerializable(typeof(EmailRequest))]
[JsonSerializable(typeof(ReportRequest))]
[JsonSerializable(typeof(ReportResponse))]
[JsonSerializable(typeof(SentEmail[]))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

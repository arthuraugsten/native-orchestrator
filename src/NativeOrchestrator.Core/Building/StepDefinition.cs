using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator;

/// <summary>A declared step: its position, name, class and code options.</summary>
internal sealed record StepDefinition(int Index, string Name, Type StepType, ObjectFactory Factory, StepOptions Options);

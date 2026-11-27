using Microsoft.Extensions.Options;

namespace NativeOrchestrator.App.ExternalServices;

/// <summary>Validates <see cref="ExternalServicesOptions"/>' data annotations, with code generated at compile time.</summary>
[OptionsValidator]
public sealed partial class ExternalServicesOptionsValidator : IValidateOptions<ExternalServicesOptions>;

using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

internal sealed class SagaScheduleOptionsValidator : IValidateOptions<SagaScheduleOptions>
{
  private readonly TimeProvider _timeProvider;

  public SagaScheduleOptionsValidator(TimeProvider timeProvider)
  {
    _timeProvider = timeProvider;
  }

  public ValidateOptionsResult Validate(string? name, SagaScheduleOptions options)
  {
    var errors = new List<string>();
    var prefix = $"Schedules:{name}";
    var zone = SagaSchedule.FindTimeZone(options.TimeZone);
    if (zone is null)
      errors.Add($"{prefix}:TimeZone '{options.TimeZone}' isn't a known time zone. Use an IANA (such as 'Europe/Lisbon') or Windows identifier, or 'UTC'.");
    if (!CronExpression.TryParse(options.Cron, out var cron, out var cronError))
      errors.Add($"{prefix}:Cron is invalid: {cronError}");
    else if (zone is not null && cron!.GetNextOccurrence(_timeProvider.GetUtcNow(), zone) is null)
      errors.Add($"{prefix}:Cron '{options.Cron}' never occurs (no occurrence in the next five years).");
    if (options.MisfireThreshold < TimeSpan.Zero)
      errors.Add($"{prefix}:MisfireThreshold must not be negative.");
    return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
  }
}

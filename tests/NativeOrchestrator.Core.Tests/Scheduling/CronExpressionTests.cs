namespace NativeOrchestrator.Tests;

public sealed class CronExpressionTests
{
  private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

  [Theory]
  [InlineData("*/15 * * * *", "2026-11-26T10:07:00Z", "2026-11-26T10:15:00Z")]
  [InlineData("*/15 * * * *", "2026-11-26T10:15:00Z", "2026-11-26T10:30:00Z")]
  [InlineData("0 2 * * *", "2026-11-26T02:00:00Z", "2026-11-27T02:00:00Z")]
  [InlineData("0 9 * * MON-FRI", "2026-11-27T10:00:00Z", "2026-11-30T09:00:00Z")]
  [InlineData("0 0 * JAN SUN", "2026-12-31T00:00:00Z", "2027-01-03T00:00:00Z")]
  [InlineData("0 0 * 1 7", "2026-12-31T00:00:00Z", "2027-01-03T00:00:00Z")]
  [InlineData("5/20 * * * *", "2026-11-26T10:06:00Z", "2026-11-26T10:25:00Z")]
  [InlineData("0-30/10 8,20 * * *", "2026-11-26T08:30:00Z", "2026-11-26T20:00:00Z")]
  [InlineData("0 0 29 2 *", "2026-03-01T00:00:00Z", "2028-02-29T00:00:00Z")]
  [InlineData("0 0 31 * *", "2026-11-01T00:00:00Z", "2026-12-31T00:00:00Z")]
  [InlineData("@daily", "2026-11-26T10:00:00Z", "2026-11-27T00:00:00Z")]
  [InlineData("@hourly", "2026-11-26T10:00:00Z", "2026-11-26T11:00:00Z")]
  [InlineData("@monthly", "2026-11-26T10:00:00Z", "2026-12-01T00:00:00Z")]
  [InlineData("@weekly", "2026-11-26T10:00:00Z", "2026-11-29T00:00:00Z")]
  [InlineData("@yearly", "2026-11-26T10:00:00Z", "2027-01-01T00:00:00Z")]
  public void Finds_the_next_occurrence_in_utc(string expression, string after, string expected)
  {
    var next = CronExpression.Parse(expression).GetNextOccurrence(DateTimeOffset.Parse(after), TimeZoneInfo.Utc);

    Assert.Equal(DateTimeOffset.Parse(expected), next);
  }

  [Fact]
  public void Six_fields_have_seconds_first_and_sub_second_times_round_up()
  {
    var cron = CronExpression.Parse("30 */10 * * * *");

    Assert.Equal(DateTimeOffset.Parse("2026-11-26T10:00:30Z"), cron.GetNextOccurrence(DateTimeOffset.Parse("2026-11-26T10:00:00Z"), TimeZoneInfo.Utc));
    Assert.Equal(DateTimeOffset.Parse("2026-11-26T10:10:30Z"), cron.GetNextOccurrence(DateTimeOffset.Parse("2026-11-26T10:00:30Z"), TimeZoneInfo.Utc));
    Assert.Equal(DateTimeOffset.Parse("2026-11-26T10:10:30Z"), cron.GetNextOccurrence(DateTimeOffset.Parse("2026-11-26T10:00:30.5Z"), TimeZoneInfo.Utc));
  }

  [Fact]
  public void When_both_day_fields_are_restricted_either_one_matches()
  {
    // The 1st of the month, or any Monday.
    var cron = CronExpression.Parse("0 0 1 * MON");
    var first = cron.GetNextOccurrence(DateTimeOffset.Parse("2026-11-26T00:00:00Z"), TimeZoneInfo.Utc)!.Value;
    var second = cron.GetNextOccurrence(first, TimeZoneInfo.Utc)!.Value;
    var third = cron.GetNextOccurrence(second, TimeZoneInfo.Utc)!.Value;

    Assert.Equal(DateTimeOffset.Parse("2026-11-30T00:00:00Z"), first);
    Assert.Equal(DateTimeOffset.Parse("2026-12-01T00:00:00Z"), second);
    Assert.Equal(DateTimeOffset.Parse("2026-12-07T00:00:00Z"), third);
  }

  [Fact]
  public void A_wildcard_day_of_month_with_a_step_leaves_only_the_day_of_week()
  {
    var next = CronExpression.Parse("0 0 */2 * MON").GetNextOccurrence(DateTimeOffset.Parse("2026-11-26T00:00:00Z"), TimeZoneInfo.Utc);

    Assert.Equal(DateTimeOffset.Parse("2026-11-30T00:00:00Z"), next);
  }

  [Fact]
  public void An_expression_that_never_occurs_has_no_next_occurrence()
  {
    Assert.Null(CronExpression.Parse("0 0 30 2 *").GetNextOccurrence(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc));
  }

  [Fact]
  public void Times_are_read_in_the_time_zone()
  {
    // Lisbon is UTC+1 in summer.
    var next = CronExpression.Parse("0 9 * * *").GetNextOccurrence(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), Lisbon);

    Assert.Equal(DateTimeOffset.Parse("2026-07-01T08:00:00Z"), next);
  }

  [Fact]
  public void A_time_skipped_by_daylight_saving_runs_when_the_clocks_change()
  {
    // On 2026-03-29 Lisbon's clocks go from 01:00 to 02:00 (at 01:00 UTC), so 01:30 doesn't exist that day.
    var cron = CronExpression.Parse("30 1 * * *");
    var onTheDay = cron.GetNextOccurrence(DateTimeOffset.Parse("2026-03-28T12:00:00Z"), Lisbon)!.Value;
    var dayAfter = cron.GetNextOccurrence(onTheDay, Lisbon);

    Assert.Equal(DateTimeOffset.Parse("2026-03-29T01:00:00Z"), onTheDay);
    Assert.Equal(DateTimeOffset.Parse("2026-03-30T00:30:00Z"), dayAfter);
  }

  [Fact]
  public void A_time_repeated_by_daylight_saving_runs_once()
  {
    // On 2026-10-25 Lisbon's clocks go from 02:00 back to 01:00 (at 01:00 UTC), so 01:30 happens twice.
    var cron = CronExpression.Parse("30 1 * * *");
    var onTheDay = cron.GetNextOccurrence(DateTimeOffset.Parse("2026-10-24T12:00:00Z"), Lisbon)!.Value;
    var next = cron.GetNextOccurrence(onTheDay, Lisbon);

    Assert.Equal(DateTimeOffset.Parse("2026-10-25T00:30:00Z"), onTheDay);
    Assert.Equal(DateTimeOffset.Parse("2026-10-26T01:30:00Z"), next);
  }

  [Theory]
  [InlineData("")]
  [InlineData("* * * *")]
  [InlineData("* * * * * * *")]
  [InlineData("60 * * * *")]
  [InlineData("* 24 * * *")]
  [InlineData("* * 0 * *")]
  [InlineData("* * * 13 *")]
  [InlineData("* * * * 8")]
  [InlineData("5-1 * * * *")]
  [InlineData("*/0 * * * *")]
  [InlineData("*/x * * * *")]
  [InlineData("1,,2 * * * *")]
  [InlineData("* * * FOO *")]
  [InlineData("? * * * *")]
  [InlineData("-1 * * * *")]
  public void Invalid_expressions_are_rejected_with_a_reason(string expression)
  {
    Assert.False(CronExpression.TryParse(expression, out var cron, out var error));
    Assert.Null(cron);
    Assert.False(string.IsNullOrWhiteSpace(error));
  }
}

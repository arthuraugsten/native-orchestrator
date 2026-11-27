using System.Globalization;
using System.Numerics;

namespace NativeOrchestrator;

/// <summary>
/// A parsed CRON expression: five fields (<c>minute hour day-of-month month day-of-week</c>) or six with seconds first.
/// Fields take <c>*</c>, values, lists (<c>1,15</c>), ranges (<c>1-5</c>) and steps (<c>*/10</c>, <c>5/15</c>,
/// <c>0-30/5</c>); months and days of the week also take names (<c>JAN</c>, <c>MON-FRI</c>), and day-of-week 7 is
/// Sunday. <c>?</c> means <c>*</c> in the day fields. Macros: <c>@yearly</c>, <c>@annually</c>, <c>@monthly</c>,
/// <c>@weekly</c>, <c>@daily</c>, <c>@midnight</c>, <c>@hourly</c>. When both day fields are restricted, a day matches
/// if either does (the standard cron rule).
/// </summary>
internal sealed class CronExpression
{
  // How far ahead to look for an occurrence before deciding there is none (such as "0 0 30 2 *").
  private const int MaxYearsAhead = 5;

  private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
  private static readonly string[] DayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

  private readonly ulong _seconds;
  private readonly ulong _minutes;
  private readonly ulong _hours;
  private readonly ulong _daysOfMonth;
  private readonly ulong _months;
  private readonly ulong _daysOfWeek;
  private readonly bool _daysOfMonthRestricted;
  private readonly bool _daysOfWeekRestricted;
  private readonly string _text;

  private CronExpression(string text, string[] fields)
  {
    _text = text;
    var offset = fields.Length == 6 ? 1 : 0;
    _seconds = offset == 1 ? ParseField(fields[0], "second", 0, 59, null) : 1UL;
    _minutes = ParseField(fields[offset], "minute", 0, 59, null);
    _hours = ParseField(fields[offset + 1], "hour", 0, 23, null);
    _daysOfMonth = ParseField(fields[offset + 2], "day-of-month", 1, 31, null);
    _months = ParseField(fields[offset + 3], "month", 1, 12, MonthNames);
    _daysOfWeek = ParseField(fields[offset + 4], "day-of-week", 0, 7, DayNames);
    if ((_daysOfWeek & (1UL << 7)) != 0)
      _daysOfWeek = (_daysOfWeek | 1UL) & ~(1UL << 7);
    _daysOfMonthRestricted = IsRestricted(fields[offset + 2]);
    _daysOfWeekRestricted = IsRestricted(fields[offset + 4]);
  }

  /// <summary>Parses an expression.</summary>
  /// <exception cref="FormatException">The expression isn't valid; the message says why.</exception>
  public static CronExpression Parse(string expression)
  {
    ArgumentNullException.ThrowIfNull(expression);
    var text = expression.Trim();
    var expanded = text.ToUpperInvariant() switch
    {
      "@YEARLY" or "@ANNUALLY" => "0 0 1 1 *",
      "@MONTHLY" => "0 0 1 * *",
      "@WEEKLY" => "0 0 * * 0",
      "@DAILY" or "@MIDNIGHT" => "0 0 * * *",
      "@HOURLY" => "0 * * * *",
      _ => text,
    };
    var fields = expanded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    if (fields.Length is not (5 or 6))
      throw new FormatException($"'{expression}' has {fields.Length} fields; a CRON expression has 5 (minute hour day-of-month month day-of-week) or 6 (with seconds first).");
    return new CronExpression(text, fields);
  }

  /// <summary>Parses an expression, or returns the reason it isn't valid.</summary>
  public static bool TryParse(string? expression, out CronExpression? cron, out string? error)
  {
    cron = null;
    error = null;
    if (string.IsNullOrWhiteSpace(expression))
    {
      error = "The CRON expression is empty.";
      return false;
    }

    try
    {
      cron = Parse(expression);
      return true;
    }
    catch (FormatException ex)
    {
      error = ex.Message;
      return false;
    }
  }

  /// <summary>
  /// The first occurrence strictly after <paramref name="after"/>, with the fields read as wall-clock time in
  /// <paramref name="zone"/>; <see langword="null"/> if there is none in the next five years. A time skipped by a
  /// daylight-saving change runs when the change happens; a time that occurs twice runs once, the first time.
  /// </summary>
  public DateTimeOffset? GetNextOccurrence(DateTimeOffset after, TimeZoneInfo zone)
  {
    ArgumentNullException.ThrowIfNull(zone);
    var afterUtc = after.ToUniversalTime();
    var local = TimeZoneInfo.ConvertTime(afterUtc, zone).DateTime;
    var start = new DateTime(local.Ticks - (local.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified).AddSeconds(1);

    var candidate = start;
    while (true)
    {
      var found = FindNextLocal(candidate);
      if (found is null)
        return null;

      var wall = found.Value;
      var utc = ToUtc(wall, zone);
      if (utc > afterUtc)
        return utc;
      // Only possible in a repeated hour whose first pass is already over: that time has run, so look further.
      candidate = wall.AddSeconds(1);
    }
  }

  /// <inheritdoc />
  public override string ToString() => _text;

  private DateTime? FindNextLocal(DateTime from)
  {
    var limit = from.Year + MaxYearsAhead;
    var t = from;
    while (t.Year <= limit)
    {
      if (!Has(_months, t.Month))
      {
        t = new DateTime(t.Year, t.Month, 1).AddMonths(1);
        continue;
      }

      if (!DayMatches(t))
      {
        t = t.Date.AddDays(1);
        continue;
      }

      if (!Has(_hours, t.Hour))
      {
        var hour = NextBit(_hours, t.Hour);
        t = hour < 0 ? t.Date.AddDays(1) : t.Date.AddHours(hour);
        continue;
      }

      if (!Has(_minutes, t.Minute))
      {
        var minute = NextBit(_minutes, t.Minute);
        var hourStart = t.Date.AddHours(t.Hour);
        t = minute < 0 ? hourStart.AddHours(1) : hourStart.AddMinutes(minute);
        continue;
      }

      if (!Has(_seconds, t.Second))
      {
        var second = NextBit(_seconds, t.Second);
        var minuteStart = t.Date.AddHours(t.Hour).AddMinutes(t.Minute);
        t = second < 0 ? minuteStart.AddMinutes(1) : minuteStart.AddSeconds(second);
        continue;
      }

      return t;
    }

    return null;
  }

  private bool DayMatches(DateTime t)
  {
    var dayOfMonth = Has(_daysOfMonth, t.Day);
    var dayOfWeek = Has(_daysOfWeek, (int)t.DayOfWeek);
    return (_daysOfMonthRestricted, _daysOfWeekRestricted) switch
    {
      (true, true) => dayOfMonth || dayOfWeek,
      (true, false) => dayOfMonth,
      (false, true) => dayOfWeek,
      _ => true,
    };
  }

  private static DateTimeOffset ToUtc(DateTime wall, TimeZoneInfo zone)
  {
    if (zone.IsAmbiguousTime(wall))
    {
      // Repeated hour: the first pass has the larger offset.
      var offset = zone.GetAmbiguousTimeOffsets(wall).Max();
      return new DateTimeOffset(wall, offset).ToUniversalTime();
    }

    if (zone.IsInvalidTime(wall))
    {
      // Skipped hour: run at the moment of the change. The offset before the change gives an instant after it, so
      // search back for the first instant with the new offset.
      var before = zone.GetUtcOffset(new DateTimeOffset(wall.AddDays(-1), TimeSpan.Zero));
      var high = new DateTimeOffset(wall, before).ToUniversalTime();
      var after = zone.GetUtcOffset(high);
      var low = high.AddDays(-1);
      while (high - low > TimeSpan.FromSeconds(1))
      {
        var middle = low + ((high - low) / 2);
        if (zone.GetUtcOffset(middle) == after)
          high = middle;
        else
          low = middle;
      }

      return new DateTimeOffset(high.Ticks - (high.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    return new DateTimeOffset(wall, zone.GetUtcOffset(wall)).ToUniversalTime();
  }

  // As in Vixie cron: a day field that starts with "*" (including "*/2") doesn't count as restricted.
  private static bool IsRestricted(string field) => !field.StartsWith('*') && !field.StartsWith('?');

  private static bool Has(ulong mask, int value) => (mask & (1UL << value)) != 0;

  private static int NextBit(ulong mask, int after)
  {
    var remaining = after >= 63 ? 0 : mask & ~((1UL << (after + 1)) - 1);
    return remaining == 0 ? -1 : BitOperations.TrailingZeroCount(remaining);
  }

  private static ulong ParseField(string field, string name, int min, int max, string[]? names)
  {
    var mask = 0UL;
    foreach (var part in field.Split(','))
    {
      if (part.Length == 0)
        throw new FormatException($"The {name} field '{field}' has an empty list item.");

      var slash = part.IndexOf('/', StringComparison.Ordinal);
      var range = slash < 0 ? part : part[..slash];
      var step = 1;
      if (slash >= 0 && (!int.TryParse(part[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1))
        throw new FormatException($"The {name} field '{field}' has an invalid step '{part[(slash + 1)..]}'; it must be a positive number.");

      int from, to;
      if (range is "*" || (range is "?" && name is "day-of-month" or "day-of-week"))
      {
        from = min;
        // Day-of-week 7 is another name for Sunday (0), so "*" stops at Saturday.
        to = name == "day-of-week" ? 6 : max;
      }
      else
      {
        var dash = range.IndexOf('-', StringComparison.Ordinal);
        from = ParseValue(dash < 0 ? range : range[..dash], field, name, min, max, names);
        // "5/15" means from 5 to the end, every 15.
        to = dash >= 0 ? ParseValue(range[(dash + 1)..], field, name, min, max, names) : slash >= 0 ? max : from;
        if (from > to)
          throw new FormatException($"The {name} field '{field}' has a range '{range}' that ends before it starts.");
      }

      for (var value = from; value <= to; value += step)
        mask |= 1UL << value;
    }

    return mask;
  }

  private static int ParseValue(string text, string field, string name, int min, int max, string[]? names)
  {
    if (names is not null)
    {
      var index = Array.IndexOf(names, text.ToUpperInvariant());
      if (index >= 0)
        return index + min;
    }

    if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
      throw new FormatException($"The {name} field '{field}' has '{text}', which isn't a value from {min} to {max}{(names is null ? "" : " or a name")}.");
    return value;
  }
}

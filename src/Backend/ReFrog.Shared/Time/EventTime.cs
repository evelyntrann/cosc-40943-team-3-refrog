using System.Globalization;

namespace ReFrog.Contracts.Time;

/// <summary>
/// ReFrog's event time zone and 12-hour display (architecture §8.2.2). Instants are stored in UTC; dates and
/// times people enter or read are America/Chicago local time. Shared by the server (conversion) and the client
/// (display) so neither has its own copy.
/// </summary>
public static class EventTime
{
    public const string TimeZoneId = "America/Chicago";

    private static readonly Lazy<TimeZoneInfo> LazyZone = new(() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));

    public static TimeZoneInfo Zone => LazyZone.Value;

    /// <summary>
    /// Converts a Chicago date and time to its UTC instant. False when the local time does not exist because the
    /// clock skips it on the daylight-saving change. A local time that occurs twice is read as standard time.
    /// </summary>
    public static bool TryToUtc(DateOnly date, TimeOnly time, out DateTimeOffset utc)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            utc = default;
            return false;
        }

        utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
        return true;
    }

    /// <summary>The same instant expressed in Chicago local time.</summary>
    public static DateTimeOffset ToEventLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    /// <summary>"2:00 p.m." style.</summary>
    public static string FormatTime(TimeOnly time)
    {
        var hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        var suffix = time.Hour < 12 ? "a.m." : "p.m.";
        return string.Create(CultureInfo.InvariantCulture, $"{hour}:{time.Minute:00} {suffix}");
    }

    /// <summary>"Monday, May 3, 2027" style.</summary>
    public static string FormatDate(DateOnly date) =>
        date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>An instant shown as its Chicago date and 12-hour time.</summary>
    public static string FormatInstant(DateTimeOffset instant)
    {
        var local = ToEventLocal(instant);
        return $"{FormatDate(DateOnly.FromDateTime(local.DateTime))}, {FormatTime(TimeOnly.FromDateTime(local.DateTime))}";
    }
}

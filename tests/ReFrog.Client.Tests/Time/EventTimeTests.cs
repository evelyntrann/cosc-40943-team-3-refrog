using ReFrog.Contracts.Time;

namespace ReFrog.Client.Tests.Time;

// Architecture §8.2.2 and ADM.md key decision 12: Chicago input, UTC storage, 12-hour display.
// ADM.md test row "Time-zone boundary".
[TestClass]
public sealed class EventTimeTests
{
    [TestMethod]
    public void ChicagoTimeConvertsToTheUtcInstantInDaylightAndStandardTime()
    {
        Assert.IsTrue(EventTime.TryToUtc(new DateOnly(2027, 5, 3), new TimeOnly(14, 0), out var may));
        Assert.AreEqual(new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero), may);
        Assert.AreEqual(TimeSpan.Zero, may.Offset);

        Assert.IsTrue(EventTime.TryToUtc(new DateOnly(2026, 12, 7), new TimeOnly(14, 0), out var december));
        Assert.AreEqual(new DateTimeOffset(2026, 12, 7, 20, 0, 0, TimeSpan.Zero), december);
    }

    [TestMethod]
    public void TimeSkippedBySpringForwardDoesNotExist()
    {
        Assert.IsFalse(EventTime.TryToUtc(new DateOnly(2027, 3, 14), new TimeOnly(2, 30), out _));
        Assert.IsTrue(EventTime.TryToUtc(new DateOnly(2027, 3, 14), new TimeOnly(3, 0), out var after));
        Assert.AreEqual(new DateTimeOffset(2027, 3, 14, 8, 0, 0, TimeSpan.Zero), after);
    }

    [TestMethod]
    public void TimeRepeatedByFallBackIsReadAsStandardTime()
    {
        Assert.IsTrue(EventTime.TryToUtc(new DateOnly(2026, 11, 1), new TimeOnly(1, 30), out var repeated));
        Assert.AreEqual(new DateTimeOffset(2026, 11, 1, 7, 30, 0, TimeSpan.Zero), repeated);
    }

    [TestMethod]
    public void TimesDisplayOnATwelveHourClockWithPeriods()
    {
        Assert.AreEqual("12:00 a.m.", EventTime.FormatTime(new TimeOnly(0, 0)));
        Assert.AreEqual("9:05 a.m.", EventTime.FormatTime(new TimeOnly(9, 5)));
        Assert.AreEqual("12:30 p.m.", EventTime.FormatTime(new TimeOnly(12, 30)));
        Assert.AreEqual("2:00 p.m.", EventTime.FormatTime(new TimeOnly(14, 0)));
    }

    [TestMethod]
    public void TheWindowsFallbackIdIsTheSameZone()
    {
        // The fallback is used only on a host without IANA data; here both ids resolve, so their rules can be compared.
        var windows = TimeZoneInfo.FindSystemTimeZoneById(EventTime.WindowsTimeZoneId);
        foreach (var local in new[] { new DateTime(2027, 5, 3, 14, 0, 0), new DateTime(2026, 12, 7, 14, 0, 0) })
        {
            Assert.AreEqual(EventTime.Zone.GetUtcOffset(local), windows.GetUtcOffset(local));
        }
    }

    [TestMethod]
    public void InstantDisplaysAsItsChicagoDateAndTime()
    {
        Assert.AreEqual("Monday, May 3, 2027, 2:00 p.m.",
            EventTime.FormatInstant(new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero)));
    }
}

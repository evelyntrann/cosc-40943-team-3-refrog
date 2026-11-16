using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Info;

// INF's EventConfigService and its tables, as UC-ADM-manage-shifts reads them (docs/design/INF.md, EventConfigService;
// ADM.md key decision 11). Sample data only.
[TestClass]
public sealed class EventConfigServiceTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task CurrentEventIsThePlannedOrActiveOne()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var planned = Event(ReFrogEventStatus.Planned, 2027);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.AddRange(Event(ReFrogEventStatus.Archived, 2025), Event(ReFrogEventStatus.Closed, 2026), planned);
            return db.SaveChangesAsync();
        });

        var current = await WithService(host, service => service.GetCurrentEventAsync(CancellationToken.None));

        Assert.AreEqual(planned.Id, current?.Id);
        Assert.AreEqual(ReFrogEventStatus.Planned, current?.Status);
        Assert.AreEqual(new DateOnly(2027, 5, 3), current?.StartDate);
    }

    [TestMethod]
    public async Task NoPlannedOrActiveEventMeansNoCurrentEvent()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(Event(ReFrogEventStatus.Closed, 2026));
            return db.SaveChangesAsync();
        });

        Assert.IsNull(await WithService(host, service => service.GetCurrentEventAsync(CancellationToken.None)));
    }

    [TestMethod]
    public async Task APlannedAndAnActiveEventCannotBothExist()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(Event(ReFrogEventStatus.Active, 2027));
            return db.SaveChangesAsync();
        });

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(Event(ReFrogEventStatus.Planned, 2028));
            return db.SaveChangesAsync();
        }));
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(Event(ReFrogEventStatus.Active, 2028));
            return db.SaveChangesAsync();
        }));
    }

    [TestMethod]
    public async Task LocationsComeWithTheirWeeklyHoursAndOnlyForTheirEvent()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var current = Event(ReFrogEventStatus.Planned, 2027);
        var old = Event(ReFrogEventStatus.Closed, 2026);
        var commons = Location(current.Id, "Sample Commons", DonationLocationStatus.Open);
        commons.Schedules.Add(Hours("TUESDAY", 14, 18));
        commons.Schedules.Add(Hours("MONDAY", 14, 20));
        var village = Location(current.Id, "Sample Village", DonationLocationStatus.Closed);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.AddRange(current, old);
            db.Locations.AddRange(commons, village, Location(old.Id, "Old Site", DonationLocationStatus.Open));
            return db.SaveChangesAsync();
        });

        var locations = await WithService(host, service => service.GetLocationsForEventAsync(current.Id, CancellationToken.None));

        Assert.AreEqual("Sample Commons|Sample Village", string.Join('|', locations.Select(x => x.Name)));
        CollectionAssert.AreEqual(
            new[]
            {
                new OperatingHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
                new OperatingHours(DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(18, 0)),
            },
            locations[0].Hours.ToArray());
        Assert.AreEqual(DonationLocationStatus.Closed, locations[1].Status);
        Assert.IsEmpty(locations[1].Hours);
    }

    [TestMethod]
    public async Task ScheduleRowsRejectSundayAndClosingBeforeOpening()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var current = Event(ReFrogEventStatus.Planned, 2027);
        var location = Location(current.Id, "Sample Commons", DonationLocationStatus.Open);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(current);
            db.Locations.Add(location);
            return db.SaveChangesAsync();
        });

        foreach (var bad in new[] { Hours("SUNDAY", 14, 18), Hours("MONDAY", 18, 14) })
        {
            bad.LocationId = location.Id;
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<InfoDbContext>(db =>
            {
                db.Schedules.Add(bad);
                return db.SaveChangesAsync();
            }));
        }
    }

    [TestMethod]
    public async Task FindEventReturnsNullForUnknownId()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);

        Assert.IsNull(await WithService(host, service => service.FindEventAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    internal static ReFrogEvent Event(string status, int year) => new()
    {
        Id = Guid.NewGuid(),
        EventYear = year,
        StartDate = new DateOnly(year, 5, 3),
        EndDate = new DateOnly(year, 5, 8),
        DailyOperatingHoursDescription = $"Sample hours {year}",
        CampusOverview = $"Sample overview {year}",
        Status = status,
    };

    internal static DonationLocation Location(Guid eventId, string name, string status) => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Name = name,
        Address = $"{name} sample address",
        Status = status,
    };

    internal static LocationSchedule Hours(string day, int opens, int closes) => new()
    {
        Id = Guid.NewGuid(),
        DayOfWeek = day,
        OpenTime = new TimeOnly(opens, 0),
        CloseTime = new TimeOnly(closes, 0),
    };

    private static async Task<T> WithService<T>(SqlTestHost host, Func<EventConfigService, Task<T>> action)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<EventConfigService>());
    }
}

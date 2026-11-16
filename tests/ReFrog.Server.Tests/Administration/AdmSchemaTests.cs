using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Administration;

// ADM.md data model and migration: the adm schema's constraints hold even if a caller skips validation.
[TestClass]
public sealed class AdmSchemaTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ShiftRowsRejectZeroCountEndBeforeStartAndRepeatedRequestId()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var requestId = Guid.NewGuid();
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(Shift(requestId, 2, 1));
            return db.SaveChangesAsync();
        });

        foreach (var bad in new[] { Shift(Guid.NewGuid(), 0, 1), Shift(Guid.NewGuid(), 2, -1), Shift(requestId, 2, 1) })
        {
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<AdmDbContext>(db =>
            {
                db.Shifts.Add(bad);
                return db.SaveChangesAsync();
            }));
        }
    }

    [TestMethod]
    public async Task LocationDefaultMustBePositiveAndIsOnePerEventAndLocation()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var eventId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.LocationStaffing.Add(new LocationStaffing { EventId = eventId, LocationId = locationId, DefaultRequiredCount = 3 });
            return db.SaveChangesAsync();
        });

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<AdmDbContext>(db =>
        {
            db.LocationStaffing.Add(new LocationStaffing { EventId = eventId, LocationId = locationId, DefaultRequiredCount = 2 });
            return db.SaveChangesAsync();
        }));
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<AdmDbContext>(db =>
        {
            db.LocationStaffing.Add(new LocationStaffing { EventId = eventId, LocationId = Guid.NewGuid(), DefaultRequiredCount = 0 });
            return db.SaveChangesAsync();
        }));
    }

    private static Shift Shift(Guid requestId, int count, int hours) => new()
    {
        Id = Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        StartsAt = Now.AddDays(60),
        EndsAt = Now.AddDays(60).AddHours(hours),
        RequiredCount = count,
        CreateRequestId = requestId,
        CreatedByAccountId = Guid.NewGuid(),
        CreatedAt = Now,
    };
}

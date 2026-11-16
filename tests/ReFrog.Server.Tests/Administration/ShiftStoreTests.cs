using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Administration;

// ADM's store under concurrency and failure (ADM.md "4b", "duplicate submission", "Location default count";
// §8.2.1, §8.2.8, §8.2.9). Sample data only.
[TestClass]
public sealed class ShiftStoreTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ConcurrentFirstTimeDefaultsForOneLocationAllSave()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var eventId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var outcomes = await Task.WhenAll(Enumerable.Range(1, 8).Select(count =>
            WithStore(host, (store, _) => store.SetLocationDefaultAsync(eventId, locationId, count, CancellationToken.None))));

        Assert.IsTrue(outcomes.All(x => x == SaveOutcome.Saved), string.Join(", ", outcomes));
        Assert.AreEqual(1, await host.WithAsync<AdmDbContext, int>(db => db.LocationStaffing.CountAsync()));
    }

    [TestMethod]
    public async Task ConcurrentRetriesOfOneRequestStoreOneShiftAndReturnItToTheOther()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var requestId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            WithStore(host, (store, _) => store.AddShiftAsync(Shift(requestId), Guid.NewGuid(), Now, CancellationToken.None))));

        var stored = await host.WithAsync<AdmDbContext, List<Shift>>(db => db.Shifts.AsNoTracking().ToListAsync());
        Assert.HasCount(1, stored);
        Assert.HasCount(1, results.Where(x => x.Outcome == SaveOutcome.Saved));
        var duplicate = results.Single(x => x.Outcome == SaveOutcome.DuplicateRequest);
        Assert.AreEqual(stored[0].Id, duplicate.Existing?.Id);
    }

    [TestMethod]
    public async Task ATransientFailureWrappedByEfCoreIsNotRecordedAndStoresNothing()
    {
        var failure = new WrappedTransientSaveFailure();
        await using var host = await SqlTestHost.CreateAsync(Now, services =>
            services.ConfigureDbContext<AdmDbContext>((_, options) => options.AddInterceptors(failure)));
        failure.Armed = true;

        var result = await WithStore(host, (store, audit) =>
        {
            audit.Record(Guid.NewGuid(), "SHIFT_CREATED", "Shift", "sample");
            return store.AddShiftAsync(Shift(Guid.NewGuid()), Guid.NewGuid(), Now, CancellationToken.None);
        });

        Assert.AreEqual(SaveOutcome.NotRecorded, result.Outcome);
        Assert.AreEqual(0, await host.WithAsync<AdmDbContext, int>(db => db.Shifts.CountAsync()));
        Assert.AreEqual(0, await host.WithAsync<AdmDbContext, int>(db => db.AuditEntries.CountAsync()));
    }

    [TestMethod]
    public async Task AFailedReadDiscardsTheStagedAuditEntry()
    {
        var failure = new FailStaffingRead();
        await using var host = await SqlTestHost.CreateAsync(Now, services =>
            services.ConfigureDbContext<AdmDbContext>((_, options) => options.AddInterceptors(failure)));
        failure.Armed = true;

        await WithStore(host, async (store, audit) =>
        {
            audit.Record(Guid.NewGuid(), "LOCATION_DEFAULT_SET", "LocationStaffing", "sample");
            Assert.AreEqual(SaveOutcome.NotRecorded,
                await store.SetLocationDefaultAsync(Guid.NewGuid(), Guid.NewGuid(), 3, CancellationToken.None));

            // A later change in the same scope commits only its own entry, not the one for the failed change.
            audit.Record(Guid.NewGuid(), "SHIFT_CREATED", "Shift", "sample");
            Assert.AreEqual(SaveOutcome.Saved,
                (await store.AddShiftAsync(Shift(Guid.NewGuid()), Guid.NewGuid(), Now, CancellationToken.None)).Outcome);
            return true;
        });

        var actions = await host.WithAsync<AdmDbContext, List<string>>(db => db.AuditEntries.Select(x => x.Action).ToListAsync());
        Assert.AreEqual("SHIFT_CREATED", actions.Single());
        Assert.AreEqual(0, await host.WithAsync<AdmDbContext, int>(db => db.LocationStaffing.CountAsync()));
    }

    private static StoredShift Shift(Guid requestId) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Now.AddDays(60), Now.AddDays(60).AddHours(4), 2, requestId);

    private static async Task<T> WithStore<T>(SqlTestHost host, Func<IShiftStore, IAdminAuditLog, Task<T>> action)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IShiftStore>(),
            scope.ServiceProvider.GetRequiredService<IAdminAuditLog>());
    }

    /// <summary>The shape EF Core's SQL Server strategy gives a transient error: wrapped in InvalidOperationException.</summary>
    private sealed class WrappedTransientSaveFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!Armed)
            {
                return ValueTask.FromResult(result);
            }

            Armed = false;
            throw new InvalidOperationException("An exception has been raised that is likely due to a transient failure.",
                new DbUpdateException("Simulated deadlock."));
        }
    }

    /// <summary>Fails the locked read of a location default, as a timeout would.</summary>
    private sealed class FailStaffingRead : DbCommandInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("[LocationStaffing]", StringComparison.Ordinal))
            {
                Armed = false;
                throw new TimeoutException("Simulated timeout.");
            }

            return ValueTask.FromResult(result);
        }
    }
}

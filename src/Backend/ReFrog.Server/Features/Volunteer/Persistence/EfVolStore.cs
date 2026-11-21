using Microsoft.EntityFrameworkCore;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Persistence;

namespace ReFrog.Server.Features.Volunteer.Persistence;

public sealed class EfVolStore(VolDbContext db, SqlUnitOfWork unitOfWork, ILogger<EfVolStore> logger) : IVolStore
{
    private static readonly Action<ILogger, string, Exception?> LogSaveFailure =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7301, "VolunteerAssignmentSaveFailure"),
            "A volunteer assignment could not be stored ({FailureType})");

    public async Task<int> CountHeldAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.ShiftAssignments
            .Where(x => x.ShiftId == shiftId && (x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Pending))
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountsHeldByShiftsAsync(
        IEnumerable<Guid> shiftIds, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var targetIds = shiftIds.ToList();
        if (targetIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await db.ShiftAssignments
            .Where(x => targetIds.Contains(x.ShiftId) && (x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Pending))
            .GroupBy(x => x.ShiftId)
            .Select(g => new { ShiftId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ShiftId, x => x.Count, cancellationToken);

        return counts;
    }

    public async Task<IReadOnlyDictionary<Guid, AssignmentCounts>> CountsByShiftsAsync(
        IEnumerable<Guid> shiftIds, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var targetIds = shiftIds.ToList();
        if (targetIds.Count == 0)
        {
            return new Dictionary<Guid, AssignmentCounts>();
        }

        return await db.ShiftAssignments
            .Where(x => targetIds.Contains(x.ShiftId) && (x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Pending))
            .GroupBy(x => x.ShiftId)
            .Select(g => new
            {
                ShiftId = g.Key,
                Active = g.Count(x => x.Status == AssignmentStatus.Active),
                Pending = g.Count(x => x.Status == AssignmentStatus.Pending),
            })
            .ToDictionaryAsync(x => x.ShiftId, x => new AssignmentCounts(x.Active, x.Pending), cancellationToken);
    }

    public async Task<ShiftAssignment?> FindByShiftAndVolunteerAsync(
        Guid shiftId, Guid volunteerId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.ShiftAssignments
            .SingleOrDefaultAsync(x => x.ShiftId == shiftId && x.VolunteerId == volunteerId, cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignment>> ListHeldByShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.ShiftAssignments.AsNoTracking()
            .Where(x => x.ShiftId == shiftId && (x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Pending))
            .OrderBy(x => x.AssignedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasAnyAssignmentAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.ShiftAssignments.AnyAsync(x => x.ShiftId == shiftId, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> ListHeldShiftIdsForVolunteerAsync(
        Guid volunteerId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var ids = await db.ShiftAssignments
            .Where(x => x.VolunteerId == volunteerId && (x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Pending))
            .Select(x => x.ShiftId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> ListDeniedShiftIdsForVolunteerAsync(
        Guid volunteerId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var ids = await db.ShiftAssignments
            .Where(x => x.VolunteerId == volunteerId && x.Status == AssignmentStatus.Denied)
            .Select(x => x.ShiftId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task AddAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await unitOfWork.EnlistAsync(db, cancellationToken);

        try
        {
            db.ShiftAssignments.Add(assignment);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            LogSaveFailure(logger, exception.GetType().Name, null);
            throw;
        }
    }

    public async Task UpdateAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await unitOfWork.EnlistAsync(db, cancellationToken);

        try
        {
            db.ShiftAssignments.Update(assignment);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            LogSaveFailure(logger, exception.GetType().Name, null);
            throw;
        }
    }
}

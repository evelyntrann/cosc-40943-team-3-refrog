using ReFrog.Server.Features.Info;

namespace ReFrog.Server.Features.Administration;

/// <summary>
/// A shift as VOL reads it: UTC start and end, the required count, and its event's id and status, so sign-up can
/// apply the same PLANNED/ACTIVE rule as the list (VOL.md, <c>ShiftService</c>).
/// </summary>
public sealed record ScheduledShift(
    Guid ShiftId,
    Guid EventId,
    string EventStatus,
    Guid LocationId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredCount);

/// <summary>
/// ADM's shift contract with Volunteer Scheduling (ADM.md "Integration boundary fixed by VOL"; VOL.md Components).
/// VOL compares the returned UTC instants only with <see cref="TimeProvider.GetUtcNow"/> (§8.2.2).
/// </summary>
public sealed class ShiftService(IShiftStore store, EventConfigService events)
{
    /// <summary>Shifts that have not started, in the current (PLANNED or ACTIVE) event; empty when there is none.</summary>
    public async Task<IReadOnlyList<ScheduledShift>> FindSignupCandidatesAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await events.GetCurrentEventAsync(cancellationToken);
        if (current is null)
        {
            return [];
        }

        var shifts = await store.ListStartingAfterAsync(current.Id, now, cancellationToken);
        return shifts.Select(shift => ToScheduled(shift, current.Status)).ToList();
    }

    public async Task<ScheduledShift?> FindByIdAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await store.FindByIdAsync(shiftId, cancellationToken);
        return shift is null ? null : await WithEventStatusAsync(shift, cancellationToken);
    }

    /// <summary>
    /// Reads the shift with its row locked until the caller's shared transaction (<c>SqlUnitOfWork.BeginAsync</c>)
    /// ends, so sign-ups, cancellations, and capacity edits on one shift run one after another.
    /// </summary>
    public async Task<ScheduledShift?> LockForAssignmentAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await store.LockAsync(shiftId, cancellationToken);
        return shift is null ? null : await WithEventStatusAsync(shift, cancellationToken);
    }

    private async Task<ScheduledShift> WithEventStatusAsync(StoredShift shift, CancellationToken cancellationToken)
    {
        var configured = await events.FindEventAsync(shift.EventId, cancellationToken)
            ?? throw new InvalidOperationException("A stored shift refers to an event INF does not have.");
        return ToScheduled(shift, configured.Status);
    }

    private static ScheduledShift ToScheduled(StoredShift shift, string eventStatus) => new(
        shift.Id, shift.EventId, eventStatus, shift.LocationId, shift.StartsAt, shift.EndsAt, shift.RequiredCount);
}

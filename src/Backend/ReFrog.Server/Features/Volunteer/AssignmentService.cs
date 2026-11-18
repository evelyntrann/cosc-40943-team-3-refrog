using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Volunteer.Persistence;
using ReFrog.Server.Persistence;

namespace ReFrog.Server.Features.Volunteer;

public sealed record SignUpOutcome(int StatusCode, SignUpResultDto? Result, ApiError? Refusal)
{
    public bool Succeeded => Refusal is null && Result is not null;
}

public sealed class AssignmentService(
    IVolStore volStore,
    ShiftService shiftService,
    EventConfigService eventConfig,
    VolDbContext volDb,
    SqlUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AssignmentService> logger)
{
    private static readonly Action<ILogger, Exception?> LogUnexpectedSignUpError =
        LoggerMessage.Define(LogLevel.Error, new EventId(7303, "VolunteerSignUpUnexpectedError"),
            "An unexpected error occurred during volunteer sign-up");

    public async Task<IReadOnlyList<OpenShiftDto>> ListOpenShiftsAsync(
        Guid volunteerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidateShifts = await shiftService.FindSignupCandidatesAsync(now, cancellationToken);
        if (candidateShifts.Count == 0)
        {
            return [];
        }

        var currentEvent = await eventConfig.GetCurrentEventAsync(cancellationToken);
        if (currentEvent is null)
        {
            return [];
        }

        var locations = await eventConfig.GetLocationsForEventAsync(currentEvent.Id, cancellationToken);
        var locationNames = locations.ToDictionary(l => l.Id, l => l.Name);

        var heldShiftIds = await volStore.ListHeldShiftIdsForVolunteerAsync(volunteerId, cancellationToken);
        var deniedShiftIds = await volStore.ListDeniedShiftIdsForVolunteerAsync(volunteerId, cancellationToken);

        // Filter out shifts previously denied to this volunteer (step 2, extension 4c)
        var eligibleShifts = candidateShifts.Where(s => !deniedShiftIds.Contains(s.ShiftId)).ToList();
        if (eligibleShifts.Count == 0)
        {
            return [];
        }

        var heldCounts = await volStore.CountsHeldByShiftsAsync(
            eligibleShifts.Select(s => s.ShiftId), cancellationToken);

        var result = new List<OpenShiftDto>();
        foreach (var shift in eligibleShifts)
        {
            var held = heldCounts.GetValueOrDefault(shift.ShiftId, 0);
            var openSlots = shift.RequiredCount - held;
            if (openSlots > 0)
            {
                var locName = locationNames.GetValueOrDefault(shift.LocationId, "Donation Location");
                var assignedToMe = heldShiftIds.Contains(shift.ShiftId);
                result.Add(new OpenShiftDto(
                    shift.ShiftId,
                    shift.EventId,
                    shift.LocationId,
                    locName,
                    shift.StartsAt,
                    shift.EndsAt,
                    shift.RequiredCount,
                    openSlots,
                    assignedToMe));
            }
        }

        return result.OrderBy(s => s.StartsAt).ThenBy(s => s.ShiftId).ToList();
    }

    public async Task<SignUpOutcome> SignUpAsync(
        Guid volunteerId, bool holdsVolunteerRole, Guid shiftId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            // Initial read outside lock for existence and timing checks
            var initialShift = await shiftService.FindByIdAsync(shiftId, cancellationToken);
            if (initialShift is null)
            {
                return Refuse(404, ApiErrorCategory.ResourceNotFound, "SHIFT_NOT_FOUND", "The requested shift was not found.");
            }

            if (initialShift.StartsAt <= now || (!string.Equals(initialShift.EventStatus, "PLANNED", StringComparison.OrdinalIgnoreCase) &&
                                                 !string.Equals(initialShift.EventStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)))
            {
                return Refuse(409, ApiErrorCategory.Conflict, "SHIFT_NOT_OPEN", "This shift is no longer open for sign-up.");
            }

            // Begin shared transaction via SqlUnitOfWork
            await using var tx = await unitOfWork.BeginAsync(cancellationToken);
            await unitOfWork.EnlistAsync(volDb, cancellationToken);

            // Execute UPDLOCK, HOLDLOCK on the target shift
            var lockedShift = await shiftService.LockForAssignmentAsync(shiftId, cancellationToken);
            if (lockedShift is null)
            {
                return Refuse(404, ApiErrorCategory.ResourceNotFound, "SHIFT_NOT_FOUND", "The requested shift was not found.");
            }

            // Check own-assignment first under lock (4b, 5b)
            var existing = await volStore.FindByShiftAndVolunteerAsync(shiftId, volunteerId, cancellationToken);
            if (existing is not null)
            {
                if (existing.Status is AssignmentStatus.Active or AssignmentStatus.Pending)
                {
                    var currentHeld = await volStore.CountHeldAsync(shiftId, cancellationToken);
                    var slotsRemaining = Math.Max(0, lockedShift.RequiredCount - currentHeld);
                    return new SignUpOutcome(200, new SignUpResultDto(
                        existing.Id, shiftId, existing.Status, slotsRemaining, true), null);
                }

                if (existing.Status == AssignmentStatus.Denied)
                {
                    return Refuse(409, ApiErrorCategory.Conflict, "SIGNUP_DENIED",
                        "An administrator previously denied sign-up for this shift.");
                }
            }

            // Re-check shift start time under lock
            if (lockedShift.StartsAt <= now)
            {
                return Refuse(409, ApiErrorCategory.Conflict, "SHIFT_NOT_OPEN", "This shift has already started.");
            }

            // Check capacity under lock (4a)
            var heldCount = await volStore.CountHeldAsync(shiftId, cancellationToken);
            if (heldCount >= lockedShift.RequiredCount)
            {
                return Refuse(409, ApiErrorCategory.Conflict, "SHIFT_FULL", "This shift is already full.");
            }

            // Determine status: ACTIVE if approved, PENDING if NONE (5c1)
            var assignedStatus = holdsVolunteerRole ? AssignmentStatus.Active : AssignmentStatus.Pending;
            Guid assignmentId;

            if (existing is not null && existing.Status == AssignmentStatus.Cancelled)
            {
                existing.Status = assignedStatus;
                existing.AssignedAt = now;
                existing.CancelledAt = null;
                await volStore.UpdateAssignmentAsync(existing, cancellationToken);
                assignmentId = existing.Id;
            }
            else
            {
                var newAssignment = new ShiftAssignment
                {
                    Id = Guid.NewGuid(),
                    ShiftId = shiftId,
                    VolunteerId = volunteerId,
                    Status = assignedStatus,
                    AssignedAt = now,
                    CancelledAt = null
                };
                await volStore.AddAssignmentAsync(newAssignment, cancellationToken);
                assignmentId = newAssignment.Id;
            }

            await tx.CommitAsync(cancellationToken);

            var remainingSlots = Math.Max(0, lockedShift.RequiredCount - (heldCount + 1));
            return new SignUpOutcome(201, new SignUpResultDto(
                assignmentId, shiftId, assignedStatus, remainingSlots, false), null);
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // Two-layer duplicate backstop (5b unique collision race): re-read committed row in fresh scope
            var reRead = await volStore.FindByShiftAndVolunteerAsync(shiftId, volunteerId, cancellationToken);
            if (reRead is not null && reRead.Status is AssignmentStatus.Active or AssignmentStatus.Pending)
            {
                var shift = await shiftService.FindByIdAsync(shiftId, cancellationToken);
                var held = await volStore.CountHeldAsync(shiftId, cancellationToken);
                var remaining = shift is not null ? Math.Max(0, shift.RequiredCount - held) : 0;
                return new SignUpOutcome(200, new SignUpResultDto(
                    reRead.Id, shiftId, reRead.Status, remaining, true), null);
            }

            LogUnexpectedSignUpError(logger, dbEx);
            return Refuse(500, ApiErrorCategory.Unexpected, "SAVE_FAILED", "The assignment could not be saved.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnexpectedSignUpError(logger, exception);
            return Refuse(500, ApiErrorCategory.Unexpected, "SAVE_FAILED", "The assignment could not be saved.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601);
    }

    private static SignUpOutcome Refuse(int status, ApiErrorCategory category, string code, string message) =>
        new(status, null, new ApiError(category, code, message));
}

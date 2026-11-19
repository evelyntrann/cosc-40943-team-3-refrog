namespace ReFrog.Contracts.Volunteer;

/// <summary>Status of a volunteer's shift assignment (docs/design/VOL.md data model).</summary>
public static class AssignmentStatus
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string Cancelled = "CANCELLED";
    public const string Denied = "DENIED";
}

/// <summary>An open shift available for volunteers to claim (UC-VOL-signup steps 1–2).</summary>
public sealed record OpenShiftDto(
    Guid ShiftId,
    Guid EventId,
    Guid LocationId,
    string LocationName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredCount,
    int OpenSlots,
    bool AssignedToMe);

/// <summary>Outcome of claiming a shift (UC-VOL-signup step 5, 5b, 5c).</summary>
public sealed record SignUpResultDto(
    Guid AssignmentId,
    Guid ShiftId,
    string Status,
    int OpenSlots,
    bool AlreadyAssigned);

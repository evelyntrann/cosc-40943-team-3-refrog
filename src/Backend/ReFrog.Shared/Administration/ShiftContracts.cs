namespace ReFrog.Contracts.Administration;

/// <summary><c>GET /api/v1/admin/shift-options</c> (UC-ADM-manage-shifts step 2).</summary>
public sealed record ShiftOptionsResponse(ShiftOptionsEvent Event, IReadOnlyList<ShiftOptionsLocation> Locations);

/// <summary>The current event. Dates are calendar dates in America/Chicago.</summary>
public sealed record ShiftOptionsEvent(Guid EventId, int EventYear, DateOnly StartDate, DateOnly EndDate, string Status);

/// <summary>A location of the current event, its weekly hours, and its default required count (null if not set).</summary>
public sealed record ShiftOptionsLocation(
    Guid LocationId,
    string Name,
    string Status,
    IReadOnlyList<LocationHours> OperatingHours,
    int? DefaultRequiredCount);

/// <summary>One weekday's operating hours, in America/Chicago local time.</summary>
public sealed record LocationHours(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

/// <summary>
/// <c>POST /api/v1/admin/shifts</c> (step 3). <see cref="RequestId"/> is one per form fill and kept on retry
/// (ADM.md key decision 13). <see cref="Date"/> is <c>yyyy-MM-dd</c> and the times <c>HH:mm</c>, in America/Chicago;
/// they are text so a malformed value is named as a field error. Omit <see cref="RequiredCount"/> to use the
/// location's default.
/// </summary>
public sealed record CreateShiftRequest(
    Guid RequestId,
    Guid? LocationId,
    string? Date,
    string? StartTime,
    string? EndTime,
    int? RequiredCount);

/// <summary>A created shift. <see cref="StartsAt"/> and <see cref="EndsAt"/> are UTC instants.</summary>
public sealed record ShiftResponse(Guid ShiftId, Guid LocationId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int RequiredCount);

/// <summary><c>PUT /api/v1/admin/events/{eventId}/locations/{locationId}/staffing</c>.</summary>
public sealed record SetLocationStaffingRequest(int? DefaultRequiredCount);

public sealed record LocationStaffingResponse(Guid EventId, Guid LocationId, int DefaultRequiredCount);

/// <summary>
/// A shift an administrator can choose to change (<c>UC-ADM-manage-shifts</c> 6a). <see cref="HeldCount"/> counts
/// approved and pending sign-ups. <see cref="Version"/> is sent back with a change, so a stale one can be refused.
/// </summary>
public sealed record ManagedShift(
    Guid ShiftId,
    Guid LocationId,
    string LocationName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredCount,
    int HeldCount,
    string Version);

/// <summary><c>GET /api/v1/admin/shifts</c>: the current event's shifts, earliest first.</summary>
public sealed record ShiftListResponse(IReadOnlyList<ManagedShift> Shifts);

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Time;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Features.Administration;

/// <summary>A service answer: the HTTP status and the shared envelope (architecture §8.2.3).</summary>
public sealed record AdminReply<T>(int StatusCode, ApiResponse<T> Response);

/// <summary>
/// <c>UC-ADM-manage-shifts</c>: shift options (step 2), create a shift (steps 3–5, 4a, 4b), each location's default
/// required count (Associated Information), and changing an existing shift (6a). Rules: <c>BR-location-staffing</c>,
/// <c>BR-event-schedule</c>; ADM.md key decisions 3, 4, 8, 11–13. The caller has already passed
/// <see cref="AdminAuthorization"/> (2a); the actor id comes from the session.
/// </summary>
public sealed class ShiftAdminService(
    EventConfigService events,
    IShiftStore store,
    IAdminAuditLog audit,
    IAssignmentQueryService assignments,
    IAccountEmailDirectory accounts,
    TimeProvider clock,
    ILogger<ShiftAdminService> logger)
{
    private const string ChangeUnresolved = "ASSIGNED_SHIFT_CHANGE_UNRESOLVED";
    private const string CapacityBelowHeld = "CAPACITY_BELOW_HELD";

    private static readonly IReadOnlyDictionary<Guid, int> NoDefaults = new Dictionary<Guid, int>();
    private static readonly IReadOnlyDictionary<string, string> NoFields = new Dictionary<string, string>();

    private static readonly Action<ILogger, string, Exception?> LogEventConfigUnavailable =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7203, "EventConfigUnavailable"),
            "Event configuration could not be read ({FailureType})");

    private static readonly Action<ILogger, string, Exception?> LogShiftReadUnavailable =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7204, "ShiftReadUnavailable"),
            "Shift information could not be read ({FailureType})");

    public async Task<AdminReply<ShiftOptionsResponse>> GetOptionsAsync(CancellationToken cancellationToken)
    {
        ConfiguredEvent? current;
        IReadOnlyList<ConfiguredLocation> locations;
        IReadOnlyDictionary<Guid, int> defaults;
        try
        {
            current = await events.GetCurrentEventAsync(cancellationToken);
            if (current is null)
            {
                return NoCurrentEvent<ShiftOptionsResponse>();
            }

            locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
            defaults = await store.GetLocationDefaultsAsync(current.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEventConfigUnavailable(logger, exception.GetType().Name, null);
            return Failure<ShiftOptionsResponse>(503, ApiErrorCategory.Unavailable, "UNAVAILABLE",
                "Shift setup is temporarily unavailable. Try again.");
        }

        return new(200, ApiResponse.Success(new ShiftOptionsResponse(
            new ShiftOptionsEvent(current.Id, current.EventYear, current.StartDate, current.EndDate, current.Status),
            locations.Select(location => new ShiftOptionsLocation(
                location.Id,
                location.Name,
                location.Status,
                location.Hours.Select(hours => new LocationHours(hours.Day, hours.Opens, hours.Closes)).ToList(),
                defaults.TryGetValue(location.Id, out var count) ? count : null)).ToList())));
    }

    public async Task<AdminReply<ShiftResponse>> CreateAsync(
        CreateShiftRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
        {
            return Invalid<ShiftResponse>(new Dictionary<string, string>
            {
                ["requestId"] = "Refresh the page and try again.",
            });
        }

        ConfiguredEvent? current;
        IReadOnlyList<ConfiguredLocation> locations;
        IReadOnlyDictionary<Guid, int> defaults;
        try
        {
            // ROB-duplicate-submission: a retry of a form that already created its shift gets that shift back.
            // It is answered before validation, so a configuration change since the first try (a location
            // closed, hours or dates edited) cannot turn a true retry into a refusal.
            var earlier = await store.FindByRequestIdAsync(request.RequestId, cancellationToken);
            if (earlier is not null)
            {
                return SameInput(earlier, request) ? Created(earlier) : RequestIdReused();
            }

            current = await events.GetCurrentEventAsync(cancellationToken);
            if (current is null)
            {
                return NoCurrentEvent<ShiftResponse>();
            }

            locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
            defaults = await store.GetLocationDefaultsAsync(current.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 4b: nothing was stored.
            LogEventConfigUnavailable(logger, exception.GetType().Name, null);
            return NotRecorded<ShiftResponse>();
        }

        var check = Validate(request, current, locations, defaults);
        if (check.Fields.Count > 0)
        {
            return Invalid<ShiftResponse>(check.Fields);
        }

        var shift = new StoredShift(Guid.NewGuid(), current.Id, check.LocationId, check.StartsAt, check.EndsAt,
            check.RequiredCount, request.RequestId);
        audit.Record(actorId, "SHIFT_CREATED", "Shift", shift.Id.ToString());
        var saved = await store.AddShiftAsync(shift, actorId, clock.GetUtcNow(), cancellationToken);
        return saved.Outcome switch
        {
            SaveOutcome.Saved => Created(shift),
            // A concurrent retry stored it first; the store returns that shift, read under its lock.
            SaveOutcome.DuplicateRequest when saved.Existing is { } stored =>
                SameInput(stored, request) ? Created(stored) : RequestIdReused(),
            _ => NotRecorded<ShiftResponse>(),
        };
    }

    public async Task<AdminReply<LocationStaffingResponse>> SetLocationDefaultAsync(
        Guid eventId, Guid locationId, SetLocationStaffingRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var configured = await events.FindEventAsync(eventId, cancellationToken);
            if (configured is null ||
                !(await events.GetLocationsForEventAsync(eventId, cancellationToken)).Any(x => x.Id == locationId))
            {
                return Failure<LocationStaffingResponse>(404, ApiErrorCategory.Validation, "NOT_FOUND",
                    "That event or location was not found.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEventConfigUnavailable(logger, exception.GetType().Name, null);
            return NotRecorded<LocationStaffingResponse>();
        }

        if (request.DefaultRequiredCount is not > 0)
        {
            return Invalid<LocationStaffingResponse>(new Dictionary<string, string>
            {
                ["defaultRequiredCount"] = "Enter a number of volunteers greater than zero.",
            });
        }

        var count = request.DefaultRequiredCount.Value;
        audit.Record(actorId, "LOCATION_DEFAULT_SET", "LocationStaffing",
            string.Create(CultureInfo.InvariantCulture, $"{eventId}/{locationId}"));
        return await store.SetLocationDefaultAsync(eventId, locationId, count, cancellationToken) == SaveOutcome.Saved
            ? new(200, ApiResponse.Success(new LocationStaffingResponse(eventId, locationId, count)))
            : NotRecorded<LocationStaffingResponse>();
    }

    /// <summary>6a: the current event's shifts with their held counts, for choosing one to change.</summary>
    public async Task<AdminReply<ShiftListResponse>> ListShiftsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var current = await events.GetCurrentEventAsync(cancellationToken);
            if (current is null)
            {
                return NoCurrentEvent<ShiftListResponse>();
            }

            var locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
            var shifts = await store.ListForEventAsync(current.Id, cancellationToken);
            var held = await assignments.CountsHeldByShiftsAsync(shifts.Select(x => x.Id).ToList(), cancellationToken);
            return new(200, ApiResponse.Success(new ShiftListResponse(
                shifts.Select(shift => ToManaged(shift, locations, held.GetValueOrDefault(shift.Id))).ToList())));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogShiftReadUnavailable(logger, exception.GetType().Name, null);
            return ShiftsUnavailable<ShiftListResponse>();
        }
    }

    /// <summary>
    /// 6a1: who an intended edit or removal would affect and whether it is allowed. Stores nothing; the rules are
    /// applied again under the shift lock when the change is confirmed.
    /// </summary>
    public async Task<AdminReply<ShiftImpactResponse>> AssessChangeAsync(
        Guid shiftId, string? action, ChangeShiftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var removal = string.Equals(action, "remove", StringComparison.Ordinal);
        if (!removal && !string.Equals(action, "edit", StringComparison.Ordinal))
        {
            return Invalid<ShiftImpactResponse>(new Dictionary<string, string> { ["action"] = "Choose edit or remove." });
        }

        if (!IsVersion(request.ExpectedVersion))
        {
            return InvalidVersion<ShiftImpactResponse>();
        }

        try
        {
            var (target, refusal) = await FindTargetAsync<ShiftImpactResponse>(shiftId, request.ExpectedVersion, cancellationToken);
            if (target is null)
            {
                return refusal!;
            }

            var held = await assignments.ListHeldAsync(shiftId, cancellationToken);
            string? refusalCode;
            if (removal)
            {
                refusalCode = await RemovalRefusalAsync(shiftId, cancellationToken);
            }
            else
            {
                var edit = CheckEdit(target, request);
                if (edit.Fields.Count > 0)
                {
                    return Invalid<ShiftImpactResponse>(edit.Fields);
                }

                refusalCode = EditRefusal(edit, held.Count);
            }

            var emails = await accounts.GetEmailsAsync(held.Select(x => x.VolunteerId).ToList(), cancellationToken);
            return new(200, ApiResponse.Success(new ShiftImpactResponse(
                shiftId,
                target.Shift.Version,
                held.Count,
                held.Select(x => new AffectedVolunteer(emails.GetValueOrDefault(x.VolunteerId), x.Status)).ToList(),
                refusalCode is null,
                refusalCode)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogShiftReadUnavailable(logger, exception.GetType().Name, null);
            return ShiftsUnavailable<ShiftImpactResponse>();
        }
    }

    /// <summary>The shift to change: in the current event and still at the version the administrator was shown.</summary>
    private async Task<(ShiftTarget? Target, AdminReply<T>? Refusal)> FindTargetAsync<T>(
        Guid shiftId, string expectedVersion, CancellationToken cancellationToken)
    {
        var current = await events.GetCurrentEventAsync(cancellationToken);
        if (current is null)
        {
            return (null, NoCurrentEvent<T>());
        }

        var shift = await store.FindByIdAsync(shiftId, cancellationToken);
        if (shift is null || shift.EventId != current.Id)
        {
            return (null, ShiftNotFound<T>());
        }

        if (!string.Equals(shift.Version, expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return (null, VersionStale<T>());
        }

        var locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
        return (new ShiftTarget(current, shift, locations), null);
    }

    /// <summary>
    /// A change that keeps the location, date, and times checks only the count, so a location that has since closed
    /// or changed its hours does not block it. Any other edit passes the same checks as create (4a).
    /// </summary>
    private static ShiftEdit CheckEdit(ShiftTarget target, ChangeShiftRequest request)
    {
        var stored = target.Shift;
        var count = request.RequiredCount ?? stored.RequiredCount;
        var details = new CreateShiftRequest(
            stored.CreateRequestId, request.LocationId, request.Date, request.StartTime, request.EndTime, null);
        if (SameInput(stored, details))
        {
            var fields = count > 0
                ? NoFields
                : new Dictionary<string, string> { ["requiredCount"] = "Enter a number of volunteers greater than zero." };
            return new ShiftEdit(fields, true, stored.LocationId, stored.StartsAt, stored.EndsAt, count);
        }

        var check = Validate(details with { RequiredCount = count }, target.Current, target.Locations, NoDefaults);
        return new ShiftEdit(check.Fields, false, check.LocationId, check.StartsAt, check.EndsAt, check.RequiredCount);
    }

    /// <summary>ADM.md "What a change may do": with volunteers held, only the count may change, and never below held.</summary>
    private static string? EditRefusal(ShiftEdit edit, int heldCount)
    {
        if (heldCount == 0)
        {
            return null;
        }

        if (!edit.SameDetails)
        {
            return ChangeUnresolved;
        }

        return edit.RequiredCount < heldCount ? CapacityBelowHeld : null;
    }

    /// <summary>Any assignment on record blocks removal, including CANCELLED and DENIED history (ADM.md key decision 4).</summary>
    private async Task<string?> RemovalRefusalAsync(Guid shiftId, CancellationToken cancellationToken) =>
        await assignments.HasAnyAssignmentAsync(shiftId, cancellationToken) ? ChangeUnresolved : null;

    private static bool IsVersion([NotNullWhen(true)] string? value) =>
        value is { Length: 16 } && value.All(char.IsAsciiHexDigit);

    private static AdminReply<T> InvalidVersion<T>() => Invalid<T>(new Dictionary<string, string>
    {
        ["expectedVersion"] = "Reload the shifts and try again.",
    });

    private static AdminReply<T> ShiftNotFound<T>() => Failure<T>(404, ApiErrorCategory.Validation, "NOT_FOUND",
        "That shift was not found. Reload the shifts and try again.");

    private static AdminReply<T> VersionStale<T>() => Failure<T>(409, ApiErrorCategory.Validation, "SHIFT_VERSION_STALE",
        "This shift changed since you opened it. Review it again before saving.");

    private static ManagedShift ToManaged(StoredShift shift, IReadOnlyList<ConfiguredLocation> locations, int heldCount) => new(
        shift.Id,
        shift.LocationId,
        locations.FirstOrDefault(x => x.Id == shift.LocationId)?.Name ?? "Unknown location",
        shift.StartsAt,
        shift.EndsAt,
        shift.RequiredCount,
        heldCount,
        shift.Version);

    private static AdminReply<T> ShiftsUnavailable<T>() => Failure<T>(503, ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "Shift information is temporarily unavailable. Try again.");

    /// <summary>The server's check of the entered details (4a, §8.2.5). The first problem per field is reported.</summary>
    private static ShiftCheck Validate(
        CreateShiftRequest request,
        ConfiguredEvent current,
        IReadOnlyList<ConfiguredLocation> locations,
        IReadOnlyDictionary<Guid, int> defaults)
    {
        var fields = new Dictionary<string, string>();
        var location = request.LocationId is { } locationId ? locations.FirstOrDefault(x => x.Id == locationId) : null;
        if (request.LocationId is null)
        {
            fields["locationId"] = "Choose a location.";
        }
        else if (location is null)
        {
            fields["locationId"] = "Choose a location of this event.";
        }
        else if (location.Status == DonationLocationStatus.Closed)
        {
            fields["locationId"] = "This location is closed. Choose another location.";
        }

        var usable = location is not null && location.Status != DonationLocationStatus.Closed;
        var date = ParseDate(request.Date, fields);
        var start = ParseTime(request.StartTime, "startTime", "Enter a start time.", fields);
        var end = ParseTime(request.EndTime, "endTime", "Enter an end time.", fields);

        if (date is { } day && (day < current.StartDate || day > current.EndDate))
        {
            fields["date"] = $"Choose a date from {EventTime.FormatDate(current.StartDate)} to {EventTime.FormatDate(current.EndDate)}.";
        }

        if (start is { } startTime && end is { } endTime && endTime <= startTime)
        {
            fields.TryAdd("endTime", "The end time must be after the start time on the same day.");
        }

        var startsAt = default(DateTimeOffset);
        var endsAt = default(DateTimeOffset);
        if (date is { } shiftDate)
        {
            if (start is { } s && !EventTime.TryToUtc(shiftDate, s, out startsAt))
            {
                fields.TryAdd("startTime", "That time does not exist on this date because of the daylight-saving change.");
            }

            if (end is { } e && !EventTime.TryToUtc(shiftDate, e, out endsAt))
            {
                fields.TryAdd("endTime", "That time does not exist on this date because of the daylight-saving change.");
            }

            if (usable)
            {
                CheckOperatingHours(location!, shiftDate, start, end, fields);
            }
        }

        var requiredCount = 0;
        if (request.RequiredCount is { } explicitCount)
        {
            if (explicitCount <= 0)
            {
                fields["requiredCount"] = "Enter a number of volunteers greater than zero.";
            }

            requiredCount = explicitCount;
        }
        else if (usable && defaults.TryGetValue(location!.Id, out var locationDefault))
        {
            requiredCount = locationDefault;
        }
        else if (usable)
        {
            fields["requiredCount"] = "Enter the number of volunteers; this location has no default yet.";
        }

        return new ShiftCheck(fields, request.LocationId ?? Guid.Empty, startsAt, endsAt, requiredCount);
    }

    /// <summary>
    /// Start and end are each checked against the weekday's opening periods, so every bad time is named in one
    /// answer (§8.2.5); a shift must also start and end in the same period.
    /// </summary>
    private static void CheckOperatingHours(
        ConfiguredLocation location, DateOnly date, TimeOnly? start, TimeOnly? end, Dictionary<string, string> fields)
    {
        var hours = location.Hours.Where(x => x.Day == date.DayOfWeek).ToList();
        if (hours.Count == 0)
        {
            fields.TryAdd("date", $"{location.Name} is not open on {date.DayOfWeek}s.");
            return;
        }

        var describe = string.Join(", ", hours.Select(x => $"{EventTime.FormatTime(x.Opens)} to {EventTime.FormatTime(x.Closes)}"));
        var opensFor = $"{location.Name} is open {describe} on {date.DayOfWeek}s.";
        var startPeriod = start is { } s ? hours.FirstOrDefault(x => x.Opens <= s && s < x.Closes) : null;
        var endPeriod = end is { } e ? hours.FirstOrDefault(x => x.Opens < e && e <= x.Closes) : null;
        if (start is not null && startPeriod is null)
        {
            fields.TryAdd("startTime", opensFor);
        }

        if (end is not null && endPeriod is null)
        {
            fields.TryAdd("endTime", opensFor);
        }
        else if (startPeriod is not null && endPeriod is not null && startPeriod != endPeriod)
        {
            fields.TryAdd("endTime", $"A shift must end in the same opening period it starts in. {opensFor}");
        }
    }

    private static DateOnly? ParseDate(string? value, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fields["date"] = "Enter a date.";
            return null;
        }

        if (TryParseDate(value, out var date))
        {
            return date;
        }

        fields["date"] = "Enter a date as year-month-day.";
        return null;
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static TimeOnly? ParseTime(string? value, string field, string missing, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fields[field] = missing;
            return null;
        }

        if (TryParseTime(value, out var time))
        {
            return time;
        }

        fields[field] = "Enter a time such as 2:00 p.m.";
        return null;
    }

    /// <summary>
    /// Whether a retry carries the input that created <paramref name="stored"/>: same location, the same Chicago date
    /// and times, and, if it sends a count, the same count. A retry that omits the count matches whatever count the
    /// shift was given, since the location default may have changed since.
    /// </summary>
    private static bool SameInput(StoredShift stored, CreateShiftRequest request) =>
        request.LocationId == stored.LocationId &&
        TryParseDate(request.Date, out var date) &&
        TryParseTime(request.StartTime, out var start) &&
        TryParseTime(request.EndTime, out var end) &&
        EventTime.TryToUtc(date, start, out var startsAt) && startsAt == stored.StartsAt &&
        EventTime.TryToUtc(date, end, out var endsAt) && endsAt == stored.EndsAt &&
        (request.RequiredCount is null || request.RequiredCount == stored.RequiredCount);

    private static AdminReply<ShiftResponse> Created(StoredShift shift) => new(201, ApiResponse.Success(
        new ShiftResponse(shift.Id, shift.LocationId, shift.StartsAt, shift.EndsAt, shift.RequiredCount)));

    private static AdminReply<ShiftResponse> RequestIdReused() => Failure<ShiftResponse>(409, ApiErrorCategory.Validation,
        "REQUEST_ID_REUSED",
        "A shift was already created from this form with other details. Refresh the page before creating another shift.");

    private static AdminReply<T> NoCurrentEvent<T>() => Failure<T>(409, ApiErrorCategory.Validation, "NO_CURRENT_EVENT",
        "There is no planned or active event to add shifts to.");

    private static AdminReply<T> NotRecorded<T>() => Failure<T>(503, ApiErrorCategory.Unavailable, "NOT_RECORDED",
        "The change was not saved. Try again.");

    private static AdminReply<T> Invalid<T>(IReadOnlyDictionary<string, string> fields) => Failure<T>(422,
        ApiErrorCategory.Validation, "VALIDATION_FAILED", "Check the highlighted fields.", fields);

    private static AdminReply<T> Failure<T>(int status, ApiErrorCategory category, string code, string message,
        IReadOnlyDictionary<string, string>? fields = null) =>
        new(status, ApiResponse.Failure<T>(new ApiError(category, code, message, fields)));

    private sealed record ShiftCheck(
        IReadOnlyDictionary<string, string> Fields,
        Guid LocationId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        int RequiredCount);

    private sealed record ShiftTarget(ConfiguredEvent Current, StoredShift Shift, IReadOnlyList<ConfiguredLocation> Locations);

    private sealed record ShiftEdit(
        IReadOnlyDictionary<string, string> Fields,
        bool SameDetails,
        Guid LocationId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        int RequiredCount);
}

using System.Globalization;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Time;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;

namespace ReFrog.Server.Features.Administration;

/// <summary>A service answer: the HTTP status and the shared envelope (architecture §8.2.3).</summary>
public sealed record AdminReply<T>(int StatusCode, ApiResponse<T> Response);

/// <summary>
/// <c>UC-ADM-manage-shifts</c>, first build: shift options (step 2), create a shift (steps 3–5, 4a, 4b), and each
/// location's default required count (Associated Information). Rules: <c>BR-location-staffing</c>,
/// <c>BR-event-schedule</c>; ADM.md key decisions 3, 11–13. The caller has already passed
/// <see cref="AdminAuthorization"/> (2a); the actor id comes from the session.
/// </summary>
public sealed class ShiftAdminService(
    EventConfigService events,
    IShiftStore store,
    IAdminAuditLog audit,
    TimeProvider clock,
    ILogger<ShiftAdminService> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogEventConfigUnavailable =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7203, "EventConfigUnavailable"),
            "Event configuration could not be read ({FailureType})");

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
        StoredShift? earlier;
        try
        {
            current = await events.GetCurrentEventAsync(cancellationToken);
            if (current is null)
            {
                return NoCurrentEvent<ShiftResponse>();
            }

            locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
            defaults = await store.GetLocationDefaultsAsync(current.Id, cancellationToken);
            earlier = await store.FindByRequestIdAsync(request.RequestId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 4b: nothing was stored.
            LogEventConfigUnavailable(logger, exception.GetType().Name, null);
            return NotRecorded<ShiftResponse>();
        }

        var check = Validate(request, current, locations, defaults);
        if (earlier is not null)
        {
            // ROB-duplicate-submission: a retry of the same form returns the shift it already created.
            return SameShift(earlier, check) ? Created(earlier) : RequestIdReused();
        }

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
                SameShift(stored, check) ? Created(stored) : RequestIdReused(),
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

    private static void CheckOperatingHours(
        ConfiguredLocation location, DateOnly date, TimeOnly? start, TimeOnly? end, Dictionary<string, string> fields)
    {
        var hours = location.Hours.Where(x => x.Day == date.DayOfWeek).ToList();
        if (hours.Count == 0)
        {
            fields.TryAdd("date", $"{location.Name} is not open on {date.DayOfWeek}s.");
            return;
        }

        if (start is not { } startTime || end is not { } endTime || endTime <= startTime)
        {
            return;
        }

        var describe = string.Join(", ", hours.Select(x => $"{EventTime.FormatTime(x.Opens)} to {EventTime.FormatTime(x.Closes)}"));
        var window = hours.FirstOrDefault(x => x.Opens <= startTime && startTime < x.Closes);
        if (window is null)
        {
            fields.TryAdd("startTime", $"{location.Name} is open {describe} on {date.DayOfWeek}s.");
        }
        else if (endTime > window.Closes)
        {
            fields.TryAdd("endTime", $"{location.Name} closes at {EventTime.FormatTime(window.Closes)} on {date.DayOfWeek}s.");
        }
    }

    private static DateOnly? ParseDate(string? value, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fields["date"] = "Enter a date.";
            return null;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        fields["date"] = "Enter a date as year-month-day.";
        return null;
    }

    private static TimeOnly? ParseTime(string? value, string field, string missing, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fields[field] = missing;
            return null;
        }

        if (TimeOnly.TryParseExact(value.Trim(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            return time;
        }

        fields[field] = "Enter a time such as 2:00 p.m.";
        return null;
    }

    private static bool SameShift(StoredShift stored, ShiftCheck check) =>
        check.Fields.Count == 0 &&
        stored.LocationId == check.LocationId &&
        stored.StartsAt == check.StartsAt &&
        stored.EndsAt == check.EndsAt &&
        stored.RequiredCount == check.RequiredCount;

    private static AdminReply<ShiftResponse> Created(StoredShift shift) => new(201, ApiResponse.Success(
        new ShiftResponse(shift.Id, shift.LocationId, shift.StartsAt, shift.EndsAt, shift.RequiredCount)));

    private static AdminReply<ShiftResponse> RequestIdReused() => Failure<ShiftResponse>(409, ApiErrorCategory.Validation,
        "REQUEST_ID_REUSED", "This form was already used for a different shift. Refresh the page and try again.");

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
}

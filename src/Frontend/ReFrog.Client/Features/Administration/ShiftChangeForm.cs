using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Time;
using ReFrog.Contracts.Volunteer;

namespace ReFrog.Client.Features.Administration;

/// <summary>
/// The admin shift page's state for changing an existing shift (<c>UC-ADM-manage-shifts</c> 6a). A change can be
/// confirmed only after the server has previewed exactly what will be sent (6a1), so changing any detail asks for a
/// new review. The server decides what is allowed (architecture §8.2.4, §8.2.5), and "saved" is said only after it
/// answers (<c>FR-SAVE-confirmed-success</c>).
/// </summary>
public sealed class ShiftChangeForm(HttpClient http)
{
    private string locationChoice = string.Empty;
    private string date = string.Empty;
    private string startTime = string.Empty;
    private string endTime = string.Empty;
    private int? requiredCount;

    public IReadOnlyList<ManagedShift> Shifts { get; private set; } = [];
    public ApiError? LoadError { get; private set; }

    /// <summary>The shift being changed, or null when none is open.</summary>
    public ManagedShift? Selected { get; private set; }

    public bool Removing { get; private set; }
    public ShiftImpactResponse? Impact { get; private set; }
    public ApiError? Error { get; private set; }
    public string? Confirmation { get; private set; }
    public bool Busy { get; private set; }

    public bool CanConfirm => Selected is not null && Impact?.Permitted == true && !Busy;

    public string LocationChoice
    {
        get => locationChoice;
        set
        {
            locationChoice = value;
            Impact = null;
        }
    }

    public string Date
    {
        get => date;
        set
        {
            date = value;
            Impact = null;
        }
    }

    public string StartTime
    {
        get => startTime;
        set
        {
            startTime = value;
            Impact = null;
        }
    }

    public string EndTime
    {
        get => endTime;
        set
        {
            endTime = value;
            Impact = null;
        }
    }

    public int? RequiredCount
    {
        get => requiredCount;
        set
        {
            requiredCount = value;
            Impact = null;
        }
    }

    /// <summary>Why the reviewed change is not allowed, or null when it is.</summary>
    public string? RefusalMessage
    {
        get
        {
            if (Impact is not { RefusalCode: { } code } impact)
            {
                return null;
            }

            if (code == "CAPACITY_BELOW_HELD")
            {
                var signedUp = impact.HeldCount == 1 ? "1 volunteer has" : $"{impact.HeldCount} volunteers have";
                return $"{signedUp} already signed up. Enter at least {impact.HeldCount}.";
            }

            return impact.HeldCount > 0
                ? "Volunteers are signed up for this shift, so it cannot be moved or removed yet. You can still change how many volunteers it needs."
                : "Volunteers have signed up for this shift before, so it cannot be removed. You can still edit it.";
        }
    }

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public static string StatusLabel(string status) =>
        status == AssignmentStatus.Active ? "Approved" : "Pending approval";

    public async Task LoadAsync()
    {
        LoadError = null;
        try
        {
            using var response = await http.GetAsync(new Uri("api/v1/admin/shifts", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftListResponse>>();
            Shifts = answer?.Data?.Shifts ?? [];
            LoadError = answer is null ? ListUnavailable() : answer.Error;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            Shifts = [];
            LoadError = ListUnavailable();
        }
    }

    /// <summary>Opens a shift for editing, starting from its current details in Chicago time.</summary>
    public void BeginEdit(ManagedShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        Selected = shift;
        Removing = false;
        Error = null;
        Confirmation = null;
        var starts = EventTime.ToEventLocal(shift.StartsAt);
        LocationChoice = shift.LocationId.ToString();
        Date = starts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        StartTime = starts.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndTime = EventTime.ToEventLocal(shift.EndsAt).ToString("HH:mm", CultureInfo.InvariantCulture);
        RequiredCount = shift.RequiredCount;
    }

    /// <summary>Opens a shift for removal and shows who that would affect (6a1).</summary>
    public async Task BeginRemovalAsync(ManagedShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        Selected = shift;
        Removing = true;
        Error = null;
        Confirmation = null;
        await ReviewAsync();
    }

    public void Cancel()
    {
        Close();
        Error = null;
    }

    /// <summary>6a1: asks the server who the change would affect and whether it is allowed. Stores nothing.</summary>
    public async Task ReviewAsync()
    {
        if (Selected is not { } shift)
        {
            return;
        }

        Busy = true;
        Error = null;
        Impact = null;
        try
        {
            using var response = await http.GetAsync(new Uri(
                $"api/v1/admin/shifts/{shift.ShiftId}/impact?{ImpactQuery(shift)}", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftImpactResponse>>();
            if (answer?.Data is { } impact)
            {
                Impact = impact;
                return;
            }

            await ShowRefusalAsync(answer?.Error ?? ReviewUnavailable());
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            Error = ReviewUnavailable();
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Applies the reviewed change. Returns true only when the server confirmed it.</summary>
    public async Task<bool> ConfirmAsync()
    {
        if (!CanConfirm)
        {
            return false;
        }

        var shift = Selected!;
        Busy = true;
        Error = null;
        try
        {
            using var message = await ChangeMessageAsync(shift);
            if (message is null)
            {
                Error = NotRecorded();
                return false;
            }

            using var response = await http.SendAsync(message);
            var refusal = Removing
                ? await RefusalInAsync<ShiftRemovedResponse>(response)
                : await RefusalInAsync<ManagedShift>(response);
            if (refusal is not null)
            {
                Impact = null;
                await ShowRefusalAsync(refusal);
                return false;
            }

            Confirmation = Removing ? "Shift removed." : "Shift saved.";
            Close();
            await LoadAsync();
            return true;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            // The request may have reached the server, so the page cannot say the change was not saved (§8.2.1).
            Error = new ApiError(ApiErrorCategory.Unavailable, "OUTCOME_UNKNOWN",
                "We could not confirm whether this change was saved. Check the shift below before trying again.");
            Close();
            await LoadAsync();
            return false;
        }
        finally
        {
            Busy = false;
        }
    }

    private string ImpactQuery(ManagedShift shift)
    {
        var version = $"expectedVersion={Uri.EscapeDataString(shift.Version)}";
        if (Removing)
        {
            return $"action=remove&{version}";
        }

        var count = RequiredCount is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"&requiredCount={value}")
            : string.Empty;
        return $"action=edit&{version}&locationId={Uri.EscapeDataString(LocationChoice)}&date={Uri.EscapeDataString(Date)}" +
            $"&startTime={Uri.EscapeDataString(StartTime)}&endTime={Uri.EscapeDataString(EndTime)}{count}";
    }

    private async Task<HttpRequestMessage?> ChangeMessageAsync(ManagedShift shift)
    {
        var token = await AntiforgeryRequests.FetchTokenAsync(http);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return Removing
            ? AntiforgeryRequests.Create(HttpMethod.Delete,
                $"api/v1/admin/shifts/{shift.ShiftId}?expectedVersion={Uri.EscapeDataString(shift.Version)}", token)
            : AntiforgeryRequests.Create(HttpMethod.Patch, $"api/v1/admin/shifts/{shift.ShiftId}", token, new ChangeShiftRequest(
                shift.Version,
                Guid.TryParse(LocationChoice, out var locationId) ? locationId : null,
                Date,
                StartTime,
                EndTime,
                RequiredCount));
    }

    private static async Task<ApiError?> RefusalInAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var answer = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        return answer?.Data is not null ? null : answer?.Error ?? NotRecorded();
    }

    /// <summary>A shift that changed or is gone cannot be reviewed from this copy, so the list is read again.</summary>
    private async Task ShowRefusalAsync(ApiError error)
    {
        Error = error;
        if (error.Code is "SHIFT_VERSION_STALE" or "NOT_FOUND")
        {
            Close();
            await LoadAsync();
        }
    }

    private void Close()
    {
        Selected = null;
        Removing = false;
        Impact = null;
    }

    private static ApiError ListUnavailable() => new(ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "Shifts are temporarily unavailable. Try again.");

    private static ApiError ReviewUnavailable() => new(ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "We could not check this change. Try again.");

    private static ApiError NotRecorded() => new(ApiErrorCategory.Unavailable, "NOT_RECORDED",
        "The change was not saved. Try again.");
}

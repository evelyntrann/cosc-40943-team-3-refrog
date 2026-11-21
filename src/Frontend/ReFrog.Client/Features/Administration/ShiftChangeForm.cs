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

    // Exactly what the server previewed; this, not the live inputs, is what a confirmed edit sends.
    private ChangeShiftRequest? reviewed;

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
            ClearReview();
        }
    }

    public string Date
    {
        get => date;
        set
        {
            date = value;
            ClearReview();
        }
    }

    public string StartTime
    {
        get => startTime;
        set
        {
            startTime = value;
            ClearReview();
        }
    }

    public string EndTime
    {
        get => endTime;
        set
        {
            endTime = value;
            ClearReview();
        }
    }

    public int? RequiredCount
    {
        get => requiredCount;
        set
        {
            requiredCount = value;
            ClearReview();
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
        StartTime = TimeInput(starts);
        EndTime = TimeInput(EventTime.ToEventLocal(shift.EndsAt));
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
        ClearReview();
        var removing = Removing;
        var details = EnteredDetails(shift);
        try
        {
            using var response = await http.GetAsync(new Uri(
                $"api/v1/admin/shifts/{shift.ShiftId}/impact?{ImpactQuery(shift)}", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftImpactResponse>>();
            if (answer?.Data is { } impact)
            {
                // A detail changed while the review was on its way, so this answer describes something else.
                if (ReferenceEquals(Selected, shift) && Removing == removing && details == EnteredDetails(shift))
                {
                    Impact = impact;
                    reviewed = details with { ExpectedAffected = impact.AffectedVersion };
                }

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
        var removing = Removing;
        var details = reviewed!;
        Busy = true;
        Error = null;
        try
        {
            // Nothing has been sent until the token arrives, so a failure here keeps the reviewed change to retry.
            var token = await TokenOrNullAsync();
            if (token is null)
            {
                Error = NotRecorded();
                return false;
            }

            return await SendChangeAsync(shift, removing, details, token);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task<bool> SendChangeAsync(ManagedShift shift, bool removing, ChangeShiftRequest details, string token)
    {
        try
        {
            using var message = removing
                ? AntiforgeryRequests.Create(HttpMethod.Delete,
                    $"api/v1/admin/shifts/{shift.ShiftId}?expectedVersion={Uri.EscapeDataString(shift.Version)}", token)
                : AntiforgeryRequests.Create(HttpMethod.Patch, $"api/v1/admin/shifts/{shift.ShiftId}", token, details);
            using var response = await http.SendAsync(message);

            ApiError? refusal;
            string confirmation;
            if (removing)
            {
                refusal = await RefusalInAsync<ShiftRemovedResponse>(response);
                confirmation = "Shift removed.";
            }
            else
            {
                var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ManagedShift>>();
                refusal = answer?.Data is null ? answer?.Error ?? NotRecorded() : null;
                confirmation = answer?.Data?.Version == shift.Version
                    ? "Nothing was different, so no change was saved."
                    : "Shift saved.";
            }

            if (refusal is not null)
            {
                ClearReview();
                await ShowRefusalAsync(refusal);
                return false;
            }

            Confirmation = confirmation;
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
    }

    private async Task<string?> TokenOrNullAsync()
    {
        try
        {
            var token = await AntiforgeryRequests.FetchTokenAsync(http);
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    private ChangeShiftRequest EnteredDetails(ManagedShift shift) => new(
        shift.Version,
        Guid.TryParse(LocationChoice, out var locationId) ? locationId : null,
        Date,
        StartTime,
        EndTime,
        RequiredCount);

    /// <summary>Keeps any seconds a stored time has, so an untouched time still matches the shift on the server.</summary>
    private static string TimeInput(DateTimeOffset local) =>
        local.ToString(local.Second == 0 ? "HH:mm" : "HH:mm:ss", CultureInfo.InvariantCulture);

    private void ClearReview()
    {
        Impact = null;
        reviewed = null;
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
        ClearReview();
    }

    private static ApiError ListUnavailable() => new(ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "Shifts are temporarily unavailable. Try again.");

    private static ApiError ReviewUnavailable() => new(ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "We could not check this change. Try again.");

    private static ApiError NotRecorded() => new(ApiErrorCategory.Unavailable, "NOT_RECORDED",
        "The change was not saved. Try again.");
}

using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;

namespace ReFrog.Client.Features.Administration;

/// <summary>
/// The admin shift page's state for <c>UC-ADM-manage-shifts</c>. It sends what the administrator entered and
/// shows what the server decides; the server's check is the one that counts (architecture §8.2.4, §8.2.5).
/// Entered values are kept on every refusal, and a retry after a failure reuses the same request id
/// (ADM.md key decision 13), so a lost response cannot create the shift twice. "Not created" is said only when the
/// server said nothing was stored (<c>NOT_RECORDED</c>) or the request was never sent.
/// </summary>
public sealed class ShiftAdminForm(HttpClient http)
{
    public ShiftOptionsResponse? Options { get; private set; }
    public ApiError? LoadError { get; private set; }

    public string LocationChoice { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int? RequiredCount { get; set; }

    /// <summary>One per form fill; kept on retry, replaced after a shift is created.</summary>
    public Guid RequestId { get; private set; } = Guid.NewGuid();

    public ApiError? Error { get; private set; }
    public ShiftResponse? Created { get; private set; }
    public bool Busy { get; private set; }

    /// <summary>Each location's default count as entered, by location id.</summary>
    public Dictionary<Guid, int?> DefaultInputs { get; } = [];

    public Dictionary<Guid, ApiError> DefaultErrors { get; } = [];
    public Guid? DefaultSaved { get; private set; }

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public string? DefaultError(Guid locationId) =>
        DefaultErrors.TryGetValue(locationId, out var error)
            ? error.Fields?.TryGetValue("defaultRequiredCount", out var message) == true ? message : error.Message
            : null;

    public string LocationName(Guid locationId) =>
        Options?.Locations.FirstOrDefault(x => x.LocationId == locationId)?.Name ?? "the location";

    public async Task LoadAsync()
    {
        LoadError = null;
        try
        {
            using var response = await http.GetAsync(new Uri("api/v1/admin/shift-options", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftOptionsResponse>>();
            Options = answer?.Data;
            LoadError = answer is null ? Unavailable("UNAVAILABLE", "Shift setup is temporarily unavailable. Try again.") : answer.Error;
            DefaultInputs.Clear();
            foreach (var location in Options?.Locations ?? [])
            {
                DefaultInputs[location.LocationId] = location.DefaultRequiredCount;
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            LoadError = Unavailable("UNAVAILABLE", "Shift setup is temporarily unavailable. Try again.");
        }
    }

    /// <summary>Steps 3–5. Returns true when the shift was created.</summary>
    public async Task<bool> SubmitAsync()
    {
        Busy = true;
        Error = null;
        Created = null;
        try
        {
            var request = new CreateShiftRequest(
                RequestId,
                Guid.TryParse(LocationChoice, out var locationId) ? locationId : null,
                Date,
                StartTime,
                EndTime,
                RequiredCount);
            using var message = await WithTokenAsync(HttpMethod.Post, "api/v1/admin/shifts", request);
            if (message is null)
            {
                Error = NotRecorded();
                return false;
            }

            using var response = await http.SendAsync(message);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>();
            if (answer?.Data is { } shift)
            {
                Created = shift;
                RequestId = Guid.NewGuid();
                StartTime = string.Empty;
                EndTime = string.Empty;
                RequiredCount = null;
                return true;
            }

            Error = answer?.Error ?? NotRecorded();
            return false;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            // 4b: the request may have reached the server, so the page cannot say the shift was not created
            // (§8.2.1). Resubmitting unchanged reuses the request id and cannot create it twice.
            Error = Unavailable("OUTCOME_UNKNOWN",
                "We could not confirm whether the shift was created. Select Create shift again without changing anything; it will not be created twice.");
            return false;
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Sets one location's default required count for the current event.</summary>
    public async Task<bool> SaveDefaultAsync(Guid locationId)
    {
        if (Options is null)
        {
            return false;
        }

        DefaultSaved = null;
        DefaultErrors.Remove(locationId);
        try
        {
            using var message = await WithTokenAsync(HttpMethod.Put,
                $"api/v1/admin/events/{Options.Event.EventId}/locations/{locationId}/staffing",
                new SetLocationStaffingRequest(DefaultInputs.GetValueOrDefault(locationId)));
            if (message is null)
            {
                DefaultErrors[locationId] = NotRecorded();
                return false;
            }

            using var response = await http.SendAsync(message);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<LocationStaffingResponse>>();
            if (answer?.Data is { } saved)
            {
                Options = Options with
                {
                    Locations = Options.Locations
                        .Select(x => x.LocationId == locationId ? x with { DefaultRequiredCount = saved.DefaultRequiredCount } : x)
                        .ToList(),
                };
                DefaultSaved = locationId;
                return true;
            }

            DefaultErrors[locationId] = answer?.Error ?? NotRecorded();
            return false;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Saving the same number again is safe, so the page asks for exactly that.
            DefaultErrors[locationId] = Unavailable("OUTCOME_UNKNOWN",
                "We could not confirm whether this was saved. Select Save again.");
            return false;
        }
    }

    private async Task<HttpRequestMessage?> WithTokenAsync<T>(HttpMethod method, string path, T body)
    {
        var token = await AntiforgeryRequests.FetchTokenAsync(http);
        return string.IsNullOrEmpty(token) ? null : AntiforgeryRequests.Create(method, path, token, body);
    }

    private static ApiError NotRecorded() => Unavailable("NOT_RECORDED", "The change was not saved. Try again.");

    private static ApiError Unavailable(string code, string message) => new(ApiErrorCategory.Unavailable, code, message);
}

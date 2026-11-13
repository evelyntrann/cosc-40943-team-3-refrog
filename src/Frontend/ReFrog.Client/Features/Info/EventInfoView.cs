using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Info;

namespace ReFrog.Client.Features.Info;

/// <summary>
/// State and server call behind <c>EventInfoPanel</c> (UC-INF-view-event-info).
/// Which event is shown is decided by the server; this class only displays its answer.
/// </summary>
public sealed class EventInfoView(HttpClient http)
{
    private static readonly ApiError NoAnswer = new(ApiErrorCategory.Unavailable, "NETWORK_UNAVAILABLE",
        "Event information could not be loaded. Check your connection and try again.");

    public EventInfoResponse? Info { get; private set; }

    /// <summary>The server's error from the shared envelope, or NETWORK_UNAVAILABLE when no readable answer arrived.</summary>
    public ApiError? Error { get; private set; }

    /// <summary>True after a failed load; the page offers a retry (architecture §8.2.1).</summary>
    public bool Unavailable => Error is not null;

    public bool Loading => Info is null && Error is null;

    public bool IsActive => Info?.Status == EventInfoStatus.Active && Info.ScheduleWindow is not null;

    /// <summary>The event's calendar dates, for example "Monday, May 3, 2027 to Saturday, May 8, 2027".</summary>
    public string? DateRange => IsActive
        ? $"{FormatDate(Info!.ScheduleWindow!.StartDate)} to {FormatDate(Info.ScheduleWindow.EndDate)}"
        : null;

    public async Task LoadAsync()
    {
        Info = null;
        Error = null;
        try
        {
            // Read the shared envelope for every status, so a server error keeps its category and code.
            using var response = await http.GetAsync(new Uri("api/v1/info/event", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<EventInfoResponse>>();
            Info = answer?.Data;
            Error = Info is null ? answer?.Error ?? NoAnswer : null;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException
            or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // Includes non-JSON replies (for example a captive portal) and a malformed envelope.
            Info = null;
            Error = NoAnswer;
        }
    }

    // Event dates are already calendar dates in America/Chicago, so they are formatted without conversion.
    public static string FormatDate(DateOnly date) =>
        date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
}

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
    public EventInfoResponse? Info { get; private set; }

    /// <summary>True when no answer could be read; the page offers a retry (architecture §8.2.1).</summary>
    public bool Unavailable { get; private set; }

    public bool Loading => Info is null && !Unavailable;

    public bool IsActive => Info?.Status == EventInfoStatus.Active && Info.ScheduleWindow is not null;

    /// <summary>The event's calendar dates, for example "Monday, May 3, 2027 to Saturday, May 8, 2027".</summary>
    public string? DateRange => IsActive
        ? $"{FormatDate(Info!.ScheduleWindow!.StartDate)} to {FormatDate(Info.ScheduleWindow.EndDate)}"
        : null;

    public async Task LoadAsync()
    {
        Info = null;
        Unavailable = false;
        try
        {
            var answer = await http.GetFromJsonAsync<ApiResponse<EventInfoResponse>>("api/v1/info/event");
            Info = answer?.Data;
            Unavailable = Info is null;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            Unavailable = true;
        }
    }

    // Event dates are already calendar dates in America/Chicago, so they are formatted without conversion.
    public static string FormatDate(DateOnly date) =>
        date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
}

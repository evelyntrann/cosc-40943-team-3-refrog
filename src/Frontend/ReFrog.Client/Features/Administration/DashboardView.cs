using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Time;

namespace ReFrog.Client.Features.Administration;

/// <summary>
/// State and server call behind the admin dashboard page (<c>UC-ADM-view-dashboard</c>). The server decides the
/// counts and which locations are unfilled or have no activity; this class only shows its answer (§8.2.4).
/// </summary>
public sealed class DashboardView(HttpClient http)
{
    private static readonly ApiError NoAnswer = new(ApiErrorCategory.Unavailable, "UNAVAILABLE",
        "Dashboard information is temporarily unavailable. Try again.");

    public DashboardResponse? Summary { get; private set; }

    /// <summary>The server's error, or <c>UNAVAILABLE</c> when no readable answer arrived.</summary>
    public ApiError? Error { get; private set; }

    public bool Loading => Summary is null && Error is null;

    /// <summary>
    /// A failed load clears the last answer, so the page says the data is unavailable instead of showing zeros or an
    /// earlier answer as current (<c>FR-DISPLAY-unavailable-data</c>).
    /// </summary>
    public async Task LoadAsync()
    {
        Summary = null;
        Error = null;
        try
        {
            using var response = await http.GetAsync(new Uri("api/v1/admin/dashboard", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardResponse>>();
            Summary = answer?.Data;
            Error = Summary is null ? answer?.Error ?? NoAnswer : null;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException
            or NotSupportedException)
        {
            Summary = null;
            Error = NoAnswer;
        }
    }

    /// <summary>"Monday, May 3, 2027, 2:00 p.m. to 6:00 p.m." in Chicago time (§8.2.2).</summary>
    public static string When(DashboardShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        var ends = TimeOnly.FromDateTime(EventTime.ToEventLocal(shift.EndsAt).DateTime);
        return $"{EventTime.FormatInstant(shift.StartsAt)} to {EventTime.FormatTime(ends)}";
    }

    /// <summary>"3 of 4 volunteers signed up (2 approved, 1 awaiting approval), 1 open".</summary>
    public static string Describe(DashboardCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        var volunteers = coverage.RequiredCount == 1 ? "volunteer" : "volunteers";
        return $"{coverage.HeldCount} of {coverage.RequiredCount} {volunteers} signed up " +
            $"({coverage.ActiveCount} approved, {coverage.PendingCount} awaiting approval), {coverage.OpenCount} open";
    }
}

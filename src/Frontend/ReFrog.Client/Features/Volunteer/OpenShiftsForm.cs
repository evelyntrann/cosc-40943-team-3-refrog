using System.Net.Http.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;

namespace ReFrog.Client.Features.Volunteer;

public sealed class OpenShiftsForm(HttpClient http)
{
    public IReadOnlyList<OpenShiftDto> Shifts { get; private set; } = [];
    public bool Loading { get; private set; }
    public bool Busy { get; private set; }
    public ApiError? LoadError { get; private set; }
    public ApiError? SignUpError { get; private set; }
    public string? FeedbackMessage { get; private set; }

    public async Task LoadAsync()
    {
        Loading = true;
        LoadError = null;
        try
        {
            using var response = await http.GetAsync(new Uri("api/v1/shifts/open", UriKind.Relative));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<OpenShiftDto>>>();
            if (response.IsSuccessStatusCode && answer?.Data is not null)
            {
                Shifts = answer.Data;
            }
            else
            {
                LoadError = answer?.Error ?? Unavailable("UNAVAILABLE", "Open shifts could not be loaded. Try again.");
            }
        }
        catch (Exception)
        {
            LoadError = Unavailable("NETWORK_UNAVAILABLE", "Open shifts could not be loaded. Check connection and retry.");
        }
        finally
        {
            Loading = false;
        }
    }

    public async Task<SignUpResultDto?> SignUpAsync(Guid shiftId)
    {
        Busy = true;
        SignUpError = null;
        FeedbackMessage = null;

        try
        {
            using var response = await http.PostAsync(new Uri($"api/v1/shifts/{shiftId}/assignments", UriKind.Relative), null);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();

            if (response.IsSuccessStatusCode && answer?.Data is not null)
            {
                var result = answer.Data;
                if (result.AlreadyAssigned)
                {
                    FeedbackMessage = "You are already signed up for this shift.";
                }
                else if (result.Status == AssignmentStatus.Pending)
                {
                    FeedbackMessage = "Signed up, awaiting administrator approval.";
                }
                else
                {
                    FeedbackMessage = "Sign-up confirmed!";
                }

                await LoadAsync();
                return result;
            }

            SignUpError = answer?.Error ?? Unavailable("SAVE_FAILED", "Sign-up did not complete. No assignment was made.");
            await LoadAsync();
            return null;
        }
        catch (Exception)
        {
            SignUpError = Unavailable("SAVE_FAILED", "Sign-up did not complete. Check connection and try again.");
            return null;
        }
        finally
        {
            Busy = false;
        }
    }

    private static ApiError Unavailable(string code, string message) =>
        new(ApiErrorCategory.Unavailable, code, message);
}

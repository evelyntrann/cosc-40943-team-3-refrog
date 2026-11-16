using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;

namespace ReFrog.Server.Features.Administration;

/// <summary>
/// <c>UC-ADM-manage-shifts</c> routes (ADM.md API contract). Every route calls
/// <see cref="AdminAuthorization.RequireAdministratorAsync"/> first (2a); every write then requires a valid
/// <c>RequestVerificationToken</c> header (ADM.md key decision 7). These handlers check request shape only.
/// </summary>
public static class AdminShiftEndpoints
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAdminShiftEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin");

        admin.MapGet("/shift-options", async (
            HttpContext context, AdminAuthorization authorization, ShiftAdminService service, CancellationToken cancellationToken) =>
        {
            var access = await authorization.RequireAdministratorAsync(context.User);
            if (!access.Granted)
            {
                return Refused<ShiftOptionsResponse>(access);
            }

            return Reply(await service.GetOptionsAsync(cancellationToken));
        });

        admin.MapPost("/shifts", async (
            HttpContext context, AdminAuthorization authorization, IAntiforgery antiforgery, ShiftAdminService service,
            CancellationToken cancellationToken) =>
        {
            var access = await authorization.RequireAdministratorAsync(context.User);
            if (!access.Granted)
            {
                return Refused<ShiftResponse>(access);
            }

            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return AntiforgeryInvalid<ShiftResponse>();
            }

            var input = await ReadAsync<CreateShiftRequest>(context.Request, cancellationToken);
            return input is null
                ? InvalidRequest<ShiftResponse>()
                : Reply(await service.CreateAsync(input, access.ActorId, cancellationToken));
        });

        admin.MapPut("/events/{eventId:guid}/locations/{locationId:guid}/staffing", async (
            Guid eventId, Guid locationId, HttpContext context, AdminAuthorization authorization,
            IAntiforgery antiforgery, ShiftAdminService service, CancellationToken cancellationToken) =>
        {
            var access = await authorization.RequireAdministratorAsync(context.User);
            if (!access.Granted)
            {
                return Refused<LocationStaffingResponse>(access);
            }

            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return AntiforgeryInvalid<LocationStaffingResponse>();
            }

            var input = await ReadAsync<SetLocationStaffingRequest>(context.Request, cancellationToken);
            return input is null
                ? InvalidRequest<LocationStaffingResponse>()
                : Reply(await service.SetLocationDefaultAsync(eventId, locationId, input, access.ActorId, cancellationToken));
        });

        return endpoints;
    }

    private static async Task<bool> ValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return default;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, WebJsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static IResult Reply<T>(AdminReply<T> reply) => Results.Json(reply.Response, statusCode: reply.StatusCode);

    private static IResult Refused<T>(AdminAccess access) =>
        Results.Json(ApiResponse.Failure<T>(access.Refusal!), statusCode: access.StatusCode);

    private static IResult AntiforgeryInvalid<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "ANTIFORGERY_INVALID", "Refresh the page and try again.")),
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidRequest<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED", "Enter a valid request.")),
        statusCode: StatusCodes.Status422UnprocessableEntity);
}

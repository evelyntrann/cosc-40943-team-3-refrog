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

        admin.MapGet("/shifts", async (
            HttpContext context, AdminAuthorization authorization, ShiftAdminService service, CancellationToken cancellationToken) =>
        {
            var access = await authorization.RequireAdministratorAsync(context.User);
            if (!access.Granted)
            {
                return Refused<ShiftListResponse>(access);
            }

            return Reply(await service.ListShiftsAsync(cancellationToken));
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

            var body = await ReadAsync<CreateShiftRequest>(context.Request, cancellationToken);
            return body.Value is null
                ? InvalidRequest<ShiftResponse>(body.BadField)
                : Reply(await service.CreateAsync(body.Value, access.ActorId, cancellationToken));
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

            var body = await ReadAsync<SetLocationStaffingRequest>(context.Request, cancellationToken);
            return body.Value is null
                ? InvalidRequest<LocationStaffingResponse>(body.BadField)
                : Reply(await service.SetLocationDefaultAsync(eventId, locationId, body.Value, access.ActorId, cancellationToken));
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

    /// <summary>
    /// The request body, or the top-level field whose value has the wrong type (§8.2.5: name the bad field).
    /// <see cref="Body{T}.BadField"/> is null when the body is not JSON at all.
    /// </summary>
    private static async Task<Body<T>> ReadAsync<T>(HttpRequest request, CancellationToken cancellationToken)
        where T : class
    {
        if (!request.HasJsonContentType())
        {
            return new(null, null);
        }

        try
        {
            return new(await JsonSerializer.DeserializeAsync<T>(request.Body, WebJsonOptions, cancellationToken), null);
        }
        catch (JsonException exception)
        {
            return new(null, FieldOf(exception.Path));
        }
    }

    /// <summary><c>$.locationId</c> or <c>$.locationId[0]</c> → <c>locationId</c>; <c>$</c> or nothing → null.</summary>
    private static string? FieldOf(string? path)
    {
        if (path is null || !path.StartsWith("$.", StringComparison.Ordinal) || path.Length == 2)
        {
            return null;
        }

        var name = path[2..];
        var end = name.IndexOfAny(['.', '[']);
        return end < 0 ? name : end == 0 ? null : name[..end];
    }

    private sealed record Body<T>(T? Value, string? BadField)
        where T : class;

    private static IResult Reply<T>(AdminReply<T> reply) => Results.Json(reply.Response, statusCode: reply.StatusCode);

    private static IResult Refused<T>(AdminAccess access) =>
        Results.Json(ApiResponse.Failure<T>(access.Refusal!), statusCode: access.StatusCode);

    private static IResult AntiforgeryInvalid<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "ANTIFORGERY_INVALID", "Refresh the page and try again.")),
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidRequest<T>(string? badField) => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED",
            badField is null ? "Enter a valid request." : "Check the highlighted fields.",
            badField is null ? null : new Dictionary<string, string> { [badField] = "Enter a valid value." })),
        statusCode: StatusCodes.Status422UnprocessableEntity);
}

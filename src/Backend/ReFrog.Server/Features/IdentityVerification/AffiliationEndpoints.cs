using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Features.IdentityVerification;

/// <summary>
/// IDV.md API contract. The account comes only from <see cref="ICurrentUser"/>; nothing here accepts an account id,
/// role, or verification status from the browser. Both POSTs validate antiforgery themselves (AUTH.md "Antiforgery").
/// </summary>
public static class AffiliationEndpoints
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAffiliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/v1/me");

        me.MapGet("/shopping-eligibility", async (
            ICurrentUser user, AffiliationService service, CancellationToken cancellationToken) =>
        {
            if (user.AccountId is not { } accountId)
            {
                return SignedOut<ShoppingEligibilityResponse>();
            }

            var access = await user.CurrentVolunteerAccessAsync(cancellationToken);
            return Results.Ok(ApiResponse.Success(await service.StatusAsync(accountId, access, cancellationToken)));
        });

        me.MapPost("/tcu-email/codes", async (
            HttpContext context, ICurrentUser user, IAntiforgery antiforgery, AffiliationService service,
            CancellationToken cancellationToken) =>
        {
            if (user.AccountId is not { } accountId)
            {
                return SignedOut<TcuCodeResponse>();
            }

            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return AntiforgeryInvalid<TcuCodeResponse>();
            }

            var input = await ReadAsync<RequestTcuCodeRequest>(context.Request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<TcuCodeResponse>();
            }

            var access = await user.CurrentVolunteerAccessAsync(cancellationToken);
            var reply = await service.RequestCodeAsync(accountId, access, input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });

        me.MapPost("/tcu-email/verification", async (
            HttpContext context, ICurrentUser user, IAntiforgery antiforgery, AffiliationService service,
            CancellationToken cancellationToken) =>
        {
            if (user.AccountId is not { } accountId)
            {
                return SignedOut<TcuVerificationResponse>();
            }

            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return AntiforgeryInvalid<TcuVerificationResponse>();
            }

            var input = await ReadAsync<VerifyTcuCodeRequest>(context.Request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<TcuVerificationResponse>();
            }

            var access = await user.CurrentVolunteerAccessAsync(cancellationToken);
            var reply = await service.VerifyCodeAsync(accountId, access, input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
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

    private static IResult SignedOut<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Authorization, "AUTHENTICATION_REQUIRED", "Sign in to continue.")),
        statusCode: StatusCodes.Status401Unauthorized);

    private static IResult AntiforgeryInvalid<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "ANTIFORGERY_INVALID", "Refresh the page and try again.")),
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidRequest<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED", "Enter a valid request.")),
        statusCode: StatusCodes.Status422UnprocessableEntity);
}

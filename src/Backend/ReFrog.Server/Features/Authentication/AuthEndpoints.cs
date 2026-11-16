using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Server.Features.Authentication;

public static class AuthEndpoints
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var registrations = endpoints.MapGroup("/api/v1/auth/registrations").AllowAnonymous();
        registrations.MapPost("/", async (HttpRequest request, RegistrationService service, CancellationToken cancellationToken) =>
        {
            if (request.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return SignedIn<RegistrationChallengeResponse>();
            }

            var input = await ReadAsync<StartRegistrationRequest>(request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<RegistrationChallengeResponse>();
            }

            var reply = await service.StartAsync(input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });
        registrations.MapPost("/{registrationId:guid}/codes", async (
            Guid registrationId, HttpRequest request, RegistrationService service, CancellationToken cancellationToken) =>
        {
            if (request.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return SignedIn<RegistrationChallengeResponse>();
            }

            var input = await ReadAsync<ResendRegistrationCodeRequest>(request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<RegistrationChallengeResponse>();
            }

            var reply = await service.ResendAsync(registrationId, input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });
        registrations.MapPost("/{registrationId:guid}/verification", async (
            Guid registrationId, HttpRequest request, RegistrationService service, CancellationToken cancellationToken) =>
        {
            if (request.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return SignedIn<RegisteredAccountResponse>();
            }

            var input = await ReadAsync<VerifyRegistrationRequest>(request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<RegisteredAccountResponse>();
            }

            var reply = await service.VerifyAsync(registrationId, input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });
        return endpoints;
    }

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/session", async (
            HttpRequest request, SessionService service, CancellationToken cancellationToken) =>
        {
            if (request.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return Results.Json(ApiResponse.Failure<SignInResponse>(new ApiError(
                    ApiErrorCategory.Authorization, "ALREADY_SIGNED_IN", "You are already signed in.")),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var input = await ReadAsync<SignInRequest>(request, cancellationToken);
            if (input is null)
            {
                return InvalidRequest<SignInResponse>();
            }

            var reply = await service.SignInAsync(input, cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        }).AllowAnonymous();

        endpoints.MapGet("/api/v1/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Json(ApiResponse.Success(new AntiforgeryTokenResponse(tokens.RequestToken!)));
        }).AllowAnonymous();

        endpoints.MapDelete("/api/v1/auth/session", async (
            HttpContext context, IAntiforgery antiforgery, SessionService service) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Json(ApiResponse.Failure<SignOutResponse>(new ApiError(
                    ApiErrorCategory.Validation, "ANTIFORGERY_INVALID", "Refresh the page and try again.")),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var reply = await service.SignOutAsync(context.User);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });

        endpoints.MapGet("/api/v1/me/session", async (HttpContext context, SessionService service) =>
        {
            var reply = await service.CurrentAsync(context.User);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });

        return endpoints;
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

    private static IResult InvalidRequest<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED", "Enter a valid request.")),
        statusCode: StatusCodes.Status422UnprocessableEntity);

    private static IResult SignedIn<T>() => Results.Json(
        ApiResponse.Failure<T>(new ApiError(ApiErrorCategory.Authorization, "ALREADY_SIGNED_IN", "Sign out before registering another account.")),
        statusCode: StatusCodes.Status403Forbidden);
}

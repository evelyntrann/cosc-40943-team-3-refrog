using Microsoft.AspNetCore.Mvc;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;

namespace ReFrog.Server.Features.Volunteer;

public static class VolunteerEndpoints
{
    public static IEndpointRouteBuilder MapVolunteerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shifts");

        group.MapGet("/open", async (
            HttpContext context,
            [FromServices] VolunteerAuthorization auth,
            [FromServices] AssignmentService service,
            CancellationToken cancellationToken) =>
        {
            var access = await auth.RequireVolunteerUserAsync(context.User);
            if (!access.Granted)
            {
                return Results.Json(
                    ApiResponse.Failure<IReadOnlyList<OpenShiftDto>>(access.Refusal!),
                    statusCode: access.StatusCode);
            }

            var shifts = await service.ListOpenShiftsAsync(access.Actor!.AccountId, cancellationToken);
            return Results.Ok(ApiResponse.Success(shifts));
        });

        group.MapPost("/{shiftId:guid}/assignments", async (
            Guid shiftId,
            HttpContext context,
            [FromServices] VolunteerAuthorization auth,
            [FromServices] AssignmentService service,
            CancellationToken cancellationToken) =>
        {
            var access = await auth.RequireVolunteerUserAsync(context.User);
            if (!access.Granted)
            {
                return Results.Json(
                    ApiResponse.Failure<SignUpResultDto>(access.Refusal!),
                    statusCode: access.StatusCode);
            }

            var outcome = await service.SignUpAsync(
                access.Actor!.AccountId, access.Actor!.HoldsVolunteerRole, shiftId, cancellationToken);

            if (!outcome.Succeeded)
            {
                return Results.Json(
                    ApiResponse.Failure<SignUpResultDto>(outcome.Refusal!),
                    statusCode: outcome.StatusCode);
            }

            return outcome.StatusCode == 201
                ? Results.Created($"/api/v1/shifts/{shiftId}/assignments", ApiResponse.Success(outcome.Result!))
                : Results.Ok(ApiResponse.Success(outcome.Result!));
        });

        return app;
    }
}

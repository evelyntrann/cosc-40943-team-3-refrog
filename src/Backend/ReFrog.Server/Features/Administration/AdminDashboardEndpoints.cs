using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;

namespace ReFrog.Server.Features.Administration;

/// <summary>
/// <c>UC-ADM-view-dashboard</c> route (ADM.md API contract). It calls
/// <see cref="AdminAuthorization.RequireAdministratorAsync"/> before reading anything (2a).
/// </summary>
public static class AdminDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/dashboard", async (
            HttpContext context, AdminAuthorization authorization, DashboardService service, CancellationToken cancellationToken) =>
        {
            var access = await authorization.RequireAdministratorAsync(context.User);
            if (!access.Granted)
            {
                return Results.Json(ApiResponse.Failure<DashboardResponse>(access.Refusal!), statusCode: access.StatusCode);
            }

            var reply = await service.GetSummaryAsync(cancellationToken);
            return Results.Json(reply.Response, statusCode: reply.StatusCode);
        });

        return endpoints;
    }
}

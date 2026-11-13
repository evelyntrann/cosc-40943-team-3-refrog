using ReFrog.Contracts.Api;

namespace ReFrog.Server.Features.Info;

public static class InfoEndpoints
{
    /// <summary>Public event information; no sign-in (architecture §8.1, trust boundary).</summary>
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var info = endpoints.MapGroup("/api/v1/info").AllowAnonymous();
        info.MapGet("/event", async (InfoService service, CancellationToken cancellationToken) =>
            Results.Json(ApiResponse.Success(await service.GetEventInfoAsync(cancellationToken))));
        return endpoints;
    }
}

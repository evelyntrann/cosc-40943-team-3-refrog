using ReFrog.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Contracts.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReFrogCore(builder.Configuration);

var pickupDirectory = builder.Configuration["Email:DevelopmentPickupDirectory"];
if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(pickupDirectory) &&
    string.IsNullOrWhiteSpace(builder.Configuration["Email:Provider"]))
{
    builder.Services.AddScoped<IEmailSender>(provider =>
        new DevelopmentPickupEmailSender(Path.GetFullPath(pickupDirectory, builder.Environment.ContentRootPath),
            provider.GetRequiredService<TimeProvider>()));
}

var app = builder.Build();

if (args.Contains("--apply-migrations", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
    return;
}

var logUnexpected = LoggerMessage.Define(LogLevel.Error, new EventId(7902, "UnexpectedRequestFailure"),
    "Unexpected request failure");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    try
    {
        await next();
    }
    catch (Exception)
    {
        logUnexpected(app.Logger, null);
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(ApiResponse.Failure<object>(
            new ApiError(ApiErrorCategory.Unexpected, "UNEXPECTED_ERROR", "The request could not be completed.")));
    }
});
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRegistrationEndpoints();
app.Map("/api/{**path}", () => Results.Json(ApiResponse.Failure<object>(
    new ApiError(ApiErrorCategory.Validation, "NOT_FOUND", "The requested resource was not found.")),
    statusCode: StatusCodes.Status404NotFound));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

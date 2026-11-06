namespace ReFrog.Server.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReFrogCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace FSH.Framework.Web.Sse;

public static class Extensions
{
    /// <summary>
    /// Registers SSE connection manager as a singleton.
    /// </summary>
    public static IServiceCollection AddHeroSse(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.AddSingleton<SseConnectionManager>();
        services.AddSingleton<ISseTokenService, SseTokenService>();
        return services;
    }
}

using FSH.Framework.Shared.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace FSH.Framework.Web.Localization;

public static class LocalizationExtensions
{
    public const string CultureCookieName = "foodos.culture";

    public static IHostApplicationBuilder AddHeroLocalization(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<LocalizationOptions>()
            .Bind(builder.Configuration.GetSection(LocalizationOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DefaultCulture), "LocalizationOptions:DefaultCulture is required.")
            .Validate(o => o.SupportedCultures is { Length: > 0 }, "LocalizationOptions:SupportedCultures must not be empty.")
            .ValidateOnStart();

        builder.Services.AddLocalization();
        builder.Services.AddSingleton<IConfigureOptions<RequestLocalizationOptions>, ConfigureRequestLocalization>();

        return builder;
    }

    public static WebApplication UseHeroLocalization(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseRequestLocalization();
        return app;
    }

    public static IEndpointRouteBuilder MapHeroLocalizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/api/v1/meta/cultures", (IOptions<LocalizationOptions> localization) =>
            {
                LocalizationOptions value = localization.Value;
                return Results.Ok(new SupportedCulturesResponse(value.DefaultCulture, value.SupportedCultures));
            })
            .AllowAnonymous()
            .WithTags("Meta")
            .WithName("GetSupportedCultures");
        return endpoints;
    }

    private sealed class ConfigureRequestLocalization(IOptions<LocalizationOptions> options)
        : IConfigureOptions<RequestLocalizationOptions>
    {
        public void Configure(RequestLocalizationOptions localization)
        {
            ArgumentNullException.ThrowIfNull(localization);

            var settings = options.Value;
            var defaultCulture = new CultureInfo(settings.DefaultCulture);
            var supported = settings.SupportedCultures
                .Select(c => new CultureInfo(c))
                .ToList();

            localization.DefaultRequestCulture = new RequestCulture(defaultCulture);
            localization.SupportedCultures = supported;
            localization.SupportedUICultures = supported;
            localization.ApplyCurrentCultureToResponseHeaders = true;
            localization.RequestCultureProviders =
            [
                new QueryStringRequestCultureProvider { QueryStringKey = "culture", UIQueryStringKey = "culture" },
                new CookieRequestCultureProvider { CookieName = CultureCookieName },
                new AcceptLanguageHeaderRequestCultureProvider(),
            ];
        }
    }
}

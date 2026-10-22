using System.Globalization;
using CoachHub.Application.Common.Localization;
using Microsoft.Net.Http.Headers;

namespace CoachHub.API.Common.Localization;

/// <summary>
/// Sets the request's UI language from <c>Accept-Language</c> (P19): Arabic when the client prefers it, else English.
/// Only the UI culture changes, so messages are translated while dates and numbers the server writes (CSV, PDF,
/// logs) keep their formats.
/// </summary>
public static class UiLanguageMiddleware
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo(UiCulture.English);
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo(UiCulture.Arabic);

    public static IApplicationBuilder UseUiLanguage(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        CultureInfo.CurrentUICulture = Resolve(context.Request.Headers.AcceptLanguage.ToString());
        await next(context);
    });

    internal static CultureInfo Resolve(string acceptLanguage)
    {
        if (!StringWithQualityHeaderValue.TryParseList(acceptLanguage.Split(','), out var languages)) return English;
        foreach (var language in languages.OrderByDescending(x => x.Quality ?? 1))
        {
            var tag = language.Value.Value ?? string.Empty;
            if (tag.StartsWith(UiCulture.Arabic, StringComparison.OrdinalIgnoreCase)) return Arabic;
            if (tag.StartsWith(UiCulture.English, StringComparison.OrdinalIgnoreCase)) return English;
        }
        return English;
    }
}

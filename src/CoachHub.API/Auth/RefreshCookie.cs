using CoachHub.API.Common.Routing;

namespace CoachHub.API.Auth;

/// <summary>
/// The refresh token travels only in this cookie: HttpOnly (scripts cannot read it), Secure,
/// SameSite=Strict (never sent cross-site), and scoped to the auth endpoints that use it.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "coachhub_refresh";
    public const string Path = "/" + ApiRoutes.V1 + "/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options(null));

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true
    };
}

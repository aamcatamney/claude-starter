using Microsoft.AspNetCore.Antiforgery;

namespace claude_starter.Endpoints.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";
    public const string XsrfCookieName = "XSRF-TOKEN";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);

        group.MapLoginEndpoint();
        group.MapLogoutEndpoint();
        group.MapRegisterEndpoint();
        group.MapMeEndpoint();

        return app;
    }

    internal static void IssueXsrfCookie(HttpContext http, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(http);
        if (tokens.RequestToken is null) return;

        http.Response.Cookies.Append(XsrfCookieName, tokens.RequestToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
    }
}

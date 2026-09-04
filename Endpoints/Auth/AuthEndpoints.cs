using Microsoft.AspNetCore.Antiforgery;

namespace claude_starter.Endpoints.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";
    public const string XsrfCookieName = "XSRF-TOKEN";

    /// <summary>
    /// Claim carrying the user's security stamp. A cookie whose stamp no longer
    /// matches the stored one is rejected, which is how a password reset ends
    /// sessions that were opened before it.
    /// </summary>
    public const string SecurityStampClaim = "security_stamp";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, bool passkeysEnabled = false)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);

        group.MapLoginEndpoint();
        group.MapLogoutEndpoint();
        group.MapRegisterEndpoint();
        group.MapMeEndpoint();
        group.MapForgotPasswordEndpoint();
        group.MapResetPasswordEndpoint();
        group.MapVerifyEmailEndpoint();
        group.MapResendVerificationEndpoint();

        // Mapped only when enabled, so the routes are absent rather than
        // present-and-refusing.
        if (passkeysEnabled)
        {
            group.MapPasskeyRegisterOptionsEndpoint();
            group.MapPasskeyRegisterEndpoint();
            group.MapPasskeySignInOptionsEndpoint();
            group.MapPasskeySignInEndpoint();
            group.MapPasskeyListEndpoint();
            group.MapPasskeyDeleteEndpoint();
        }

        return app;
    }

    /// <summary>
    /// Establishes a session. Shared by password and passkey sign-in so both
    /// carry the same claims — notably the security stamp, without which the
    /// cookie is rejected on the next request.
    /// </summary>
    internal static async Task SignInAsync(
        HttpContext http,
        Models.User user,
        bool persistent,
        IAntiforgery antiforgery)
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
                new System.Security.Claims.Claim(SecurityStampClaim, user.SecurityStamp.ToString()),
            },
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignInAsync(
            http,
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = persistent,
                ExpiresUtc = persistent ? DateTimeOffset.UtcNow.AddDays(14) : null,
            });

        // SignInAsync writes the cookie but leaves HttpContext.User anonymous,
        // and antiforgery binds a token to the current user. Without this the
        // token would be bound to nobody.
        http.User = principal;

        IssueXsrfCookie(http, antiforgery);
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

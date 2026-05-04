using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using claude_starter.Repositories;
using claude_starter.Services.Auth;

namespace claude_starter.Endpoints.Auth;

public static class RegisterEndpoint
{
    private const int MinPasswordLength = 12;
    private const int MaxEmailLength = 254;

    public sealed record RegisterRequest(string Email, string Password, string? DisplayName);

    public static IEndpointRouteBuilder MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        RegisterRequest request,
        HttpContext http,
        IUserRepository users,
        IPasswordHasher hasher,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.Register");
        var email = (request.Email ?? string.Empty).Trim();

        if (email.Length == 0 || email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out _))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email");
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");
        }

        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered");
        }

        var hash = hasher.Hash(request.Password);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName!.Trim();

        var id = await users.CreateAsync(email, hash, displayName, ct);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email.ToLowerInvariant()),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });

        AuthEndpoints.IssueXsrfCookie(http, antiforgery);

        logger.LogInformation("Register success. UserId={UserId}", id);

        return Results.Ok(new LoginEndpoint.UserResponse(id, email.ToLowerInvariant(), displayName));
    }
}

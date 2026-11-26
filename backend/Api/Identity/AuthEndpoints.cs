using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Identity;

public sealed record RegisterRequest(string? UserName, string? Password);

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record UserDto(Guid Id, string UserName);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).RequireRateLimiting("auth");
        group.MapPost("/login", LoginAsync).RequireRateLimiting("auth");
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", MeAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.UserName, request.Password, cancellationToken);
        var payload = new AuthResponse(result.Token, result.ExpiresAt, ToDto(result.User));
        return Results.Created("/api/v1/auth/me", payload);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.UserName, request.Password, cancellationToken);
        var payload = new AuthResponse(result.Token, result.ExpiresAt, ToDto(result.User));
        return Results.Ok(payload);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext http, AuthService authService, CancellationToken cancellationToken)
    {
        var userId = RequireUserId(http.User);
        var sessionId = RequireSessionId(http.User);
        await authService.LogoutAsync(sessionId, userId, cancellationToken);
        return Results.NoContent();
    }

    private static IResult MeAsync(HttpContext http)
    {
        var user = http.User;
        return Results.Ok(new UserDto(RequireUserId(user), user.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty));
    }

    private static UserDto ToDto(User user) => new(user.Id, user.UserName);

    private static Guid RequireUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            throw Shared.ApiException.Unauthorized();
        }

        return userId;
    }

    private static Guid RequireSessionId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(BearerTokenAuthenticationHandler.SessionIdClaim)?.Value;
        if (!Guid.TryParse(value, out var sessionId))
        {
            throw Shared.ApiException.Unauthorized();
        }

        return sessionId;
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Identity;

/// <summary>
/// Esquema de autenticación propio: token opaco enviado como
/// <c>Authorization: Bearer &lt;token&gt;</c> (o <c>?access_token=</c> para
/// clientes que no pueden añadir cabeceras, p. ej. WebSocket/SignalR).
/// Valida la sesión en base de datos en cada petición, por lo que el
/// logout y la caducidad son efectivos de inmediato.
/// </summary>
public sealed class BearerTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";
    public const string SessionIdClaim = "sid";
    public const string ContentOperatorClaim = "content-operator";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var db = Context.RequestServices.GetRequiredService<AppDbContext>();
        var tokenHash = SessionToken.Hash(token);
        var session = await db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);

        if (session?.User is null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("La sesión no es válida o ha expirado.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.UserName),
            new(SessionIdClaim, session.Id.ToString()),
        };
        if (IsContentOperator(session.User.UserName))
        {
            claims.Add(new Claim(ContentOperatorClaim, session.User.UserName));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// Operadores del catálogo global: allowlist de nombres de usuario
    /// exactos (case-insensitive). Config vacía por defecto → nadie;
    /// '*' habilita a cualquier autenticado (solo factory de tests).
    /// </summary>
    private bool IsContentOperator(string userName)
    {
        var configured = Context.RequestServices
            .GetRequiredService<IConfiguration>()["Content:OperatorUsers"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        var allowed = configured.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Contains("*", StringComparer.Ordinal)
            || allowed.Contains(userName, StringComparer.OrdinalIgnoreCase);
    }

    private string? ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var value = header["Bearer ".Length..].Trim();
            if (value.Length > 0)
            {
                return value;
            }
        }

        if (Request.Query.TryGetValue("access_token", out var queryValue))
        {
            var value = queryValue.ToString();
            if (value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await WriteProblemAsync(
            StatusCodes.Status401Unauthorized, "No autenticado.",
            "Se requiere una sesión válida.");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await WriteProblemAsync(
            StatusCodes.Status403Forbidden, "Acceso denegado.",
            "No tienes permisos para realizar esta operación.");
    }

    private async Task WriteProblemAsync(int status, string title, string detail)
    {
        var problems = Context.RequestServices.GetService<IProblemDetailsService>();
        if (problems is null)
        {
            return;
        }

        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}

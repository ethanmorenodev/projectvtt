using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Identity;

public sealed record AuthResult(User User, string Token, DateTime ExpiresAt);

/// <summary>Registro, inicio de sesión y cierre de sesión en el servidor.</summary>
public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    AuthSessionOptions sessionOptions)
{
    public async Task<AuthResult> RegisterAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        var cleanUserName = InputGuard.UserName(userName);
        var cleanPassword = InputGuard.Password(password);
        var normalized = cleanUserName.ToUpperInvariant();

        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalized, cancellationToken))
        {
            throw ApiException.Conflict("Ese nombre de usuario ya está en uso.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = cleanUserName,
            NormalizedUserName = normalized,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, cleanPassword);

        var (token, expiresAt) = CreateSession();
        db.Users.Add(user);
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            TokenHash = SessionToken.Hash(token),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(user, token, expiresAt);
    }

    public async Task<AuthResult> LoginAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            throw ApiException.BadRequest("Se requieren nombre de usuario y contraseña.");
        }

        var normalized = userName.Trim().ToUpperInvariant();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken);

        var verification = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (verification == PasswordVerificationResult.Failed)
        {
            // Mismo mensaje para usuario inexistente y contraseña incorrecta.
            throw ApiException.Unauthorized("Usuario o contraseña incorrectos.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user!.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        var (token, expiresAt) = CreateSession();
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            TokenHash = SessionToken.Hash(token),
            UserId = user!.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(user, token, expiresAt);
    }

    /// <summary>Revoca la sesión actual. Idempotente.</summary>
    public async Task LogoutAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private (string Token, DateTime ExpiresAt) CreateSession() =>
        (SessionToken.Create(), DateTime.UtcNow.AddMinutes(sessionOptions.LifetimeMinutes));
}

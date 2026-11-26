namespace ProjectVTT.Api.Domain;

/// <summary>
/// Sesión de autenticación opaca: el token solo existe en el cliente y en su
/// hash aquí, lo que permite revocarla (logout) de forma inmediata.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }
    public required string TokenHash { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

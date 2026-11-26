namespace ProjectVTT.Api.Domain;

/// <summary>Usuario de la plataforma. La contraseña solo se almacena hasheada.</summary>
public class User
{
    public Guid Id { get; set; }
    public required string UserName { get; set; }
    public required string NormalizedUserName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<UserSession> Sessions { get; set; } = [];
}

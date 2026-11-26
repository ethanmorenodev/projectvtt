namespace ProjectVTT.Api.Identity;

/// <summary>Opciones de sesión leídas de la configuración (Auth:SessionMinutes).</summary>
public sealed class AuthSessionOptions
{
    public int LifetimeMinutes { get; set; } = 720;
}

using Microsoft.Extensions.Configuration;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Validación de configuración al arrancar (Épica H, ADR-024): fail-fast
/// con mensajes que NUNCA imprimen valores (podrían ser credenciales).
/// Los secretos no viven en el repositorio: llegan por variables de
/// entorno (`ConnectionStrings__Default`) o user-secrets en desarrollo.
/// </summary>
public static class StartupGuard
{
    /// <summary>Proveedores de base de datos admitidos.</summary>
    public static readonly IReadOnlySet<string> ValidProviders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "postgres", "sqlite" };

    /// <summary>
    /// Valida la configuración mínima y devuelve proveedor y cadena de
    /// conexión. Lanza <see cref="InvalidOperationException"/> si falta
    /// algo esencial.
    /// </summary>
    public static (string Provider, string ConnectionString) Validate(
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "postgres";
        if (!ValidProviders.Contains(provider))
        {
            throw new InvalidOperationException(
                $"Database:Provider no soportado: '{provider}'. Usa 'postgres' o 'sqlite'.");
        }

        // Mensaje sin valores: la cadena de conexión puede contener
        // credenciales y jamás debe llegar a logs ni consola.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Default. Arranca el entorno de desarrollo " +
                "(docker compose -f infra/docker/compose.dev.yml up -d), define la variable " +
                "ConnectionStrings__Default o un user-secrets en desarrollo.");
        }

        return (provider, connectionString);
    }
}

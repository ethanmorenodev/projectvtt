using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectVTT.Api.Data;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Comprobación de salud de la base de datos (Épica H, ADR-026): responde
/// sano si la conexión acepta consultas.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("La base de datos responde.")
            : HealthCheckResult.Unhealthy("La base de datos no responde.");
}

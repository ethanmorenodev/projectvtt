namespace ProjectVTT.Api.Shared;

/// <summary>
/// Endpoints de operación (Épica H, ADR-026): readiness profunda y
/// métricas en formato de Prometheus. El `/health` de liveness (JSON,
/// contrato histórico) se mantiene en `Program.cs`.
/// </summary>
public static class OpsEndpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Readiness: comprobación real de la base de datos (200/503).
        endpoints.MapHealthChecks("/health/ready");

        // Métricas en texto de Prometheus 0.0.4, sin dependencias.
        endpoints.MapGet("/metrics", (MetricsCollector metrics) =>
            Results.Text(
                metrics.RenderPrometheus(),
                "text/plain; version=0.0.4; charset=utf-8"));

        return endpoints;
    }
}

using System.Diagnostics;
using System.Security.Claims;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Log estructurado de peticiones (Épica H, ADR-022): una línea por
/// petición con plantilla estable (método, ruta, estado, duración y
/// usuario). Por seguridad NUNCA se loguean cabeceras, tokens ni
/// cuerpos, y la ruta se toma SIN query string (el hub acepta
/// `?access_token=`, que jamás debe llegar a los logs).
/// </summary>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    MetricsCollector metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation(
                "HTTP {Method} {Path} respondio {StatusCode} en {DurationMs} ms (usuario {UserId})",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                userId ?? "anonimo");
            // Agregados para /metrics (sin rutas ni datos sensibles).
            metrics.RecordRequest(
                context.Request.Method, context.Response.StatusCode,
                stopwatch.Elapsed.TotalSeconds);
        }
    }
}

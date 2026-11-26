using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Métricas básicas de operación (Épica H, ADR-026): contadores de
/// peticiones y duraciones acumuladas con exposición en formato texto de
/// Prometheus, sin dependencias extra. Solo agregados por método y
/// código de estado: nunca rutas con parámetros ni datos sensibles.
/// </summary>
public sealed class MetricsCollector
{
    private readonly ConcurrentDictionary<(string Method, int Status), long> _requests = new();
    private readonly ConcurrentDictionary<string, (long Count, double SumSeconds)> _durations = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;

    /// <summary>Registra una petición terminada (método, estado y duración).</summary>
    public void RecordRequest(string method, int statusCode, double durationSeconds)
    {
        _requests.AddOrUpdate((method, statusCode), 1, (_, current) => current + 1);
        _durations.AddOrUpdate(
            method,
            (1, durationSeconds),
            (_, current) => (current.Count + 1, current.SumSeconds + durationSeconds));
    }

    /// <summary>Renderiza las métricas en el formato de texto de Prometheus.</summary>
    public string RenderPrometheus()
    {
        var builder = new StringBuilder();
        using var process = Process.GetCurrentProcess();

        AppendGauge(builder, "process_uptime_seconds",
            "Tiempo desde el arranque del proceso.",
            (DateTime.UtcNow - _startedAt).TotalSeconds);
        AppendGauge(builder, "process_working_set_bytes",
            "Memoria residente del proceso.",
            process.WorkingSet64);
        AppendGauge(builder, "dotnet_gc_total_memory_bytes",
            "Memoria total reportada por el GC.",
            GC.GetTotalMemory(forceFullCollection: false));

        builder.AppendLine("# HELP http_requests_total Peticiones HTTP por método y código de estado.");
        builder.AppendLine("# TYPE http_requests_total counter");
        foreach (var entry in _requests.OrderBy(e => e.Key.Method).ThenBy(e => e.Key.Status))
        {
            builder.Append("http_requests_total{method=\"")
                .Append(EscapeLabelValue(entry.Key.Method))
                .Append("\",status=\"").Append(entry.Key.Status)
                .Append("\"} ").Append(entry.Value).AppendLine();
        }

        builder.AppendLine("# HELP http_request_duration_seconds Duración de las peticiones HTTP.");
        builder.AppendLine("# TYPE http_request_duration_seconds summary");
        foreach (var entry in _durations.OrderBy(e => e.Key))
        {
            builder.Append("http_request_duration_seconds_count{method=\"")
                .Append(EscapeLabelValue(entry.Key))
                .Append("\"} ").Append(entry.Value.Count).AppendLine();
            builder.Append("http_request_duration_seconds_sum{method=\"")
                .Append(EscapeLabelValue(entry.Key))
                .Append("\"} ").Append(entry.Value.SumSeconds.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendGauge(
        StringBuilder builder, string name, string help, double value)
    {
        builder.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        builder.Append("# TYPE ").Append(name).AppendLine(" gauge");
        builder.Append(name).Append(' ')
            .Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
    }

    /// <summary>Escape de valores de etiqueta según el formato de Prometheus.</summary>
    internal static string EscapeLabelValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}

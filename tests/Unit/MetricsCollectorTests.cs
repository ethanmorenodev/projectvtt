using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Métricas de operación (ADR-026): formato de texto de Prometheus con
/// contadores por método/estado, duraciones agregadas y métricas de
/// proceso, sin datos sensibles.
/// </summary>
public class MetricsCollectorTests
{
    [Fact]
    public void Render_IncludesProcessGaugesAndHelp()
    {
        var rendered = new MetricsCollector().RenderPrometheus();

        Assert.Contains("# TYPE process_uptime_seconds gauge", rendered);
        Assert.Contains("# TYPE process_working_set_bytes gauge", rendered);
        Assert.Contains("# TYPE dotnet_gc_total_memory_bytes gauge", rendered);
        Assert.Contains("# TYPE http_requests_total counter", rendered);
        Assert.Contains("# TYPE http_request_duration_seconds summary", rendered);
    }

    [Fact]
    public void RecordRequest_AggregatesByMethodAndStatus()
    {
        var collector = new MetricsCollector();
        collector.RecordRequest("GET", 200, 0.01);
        collector.RecordRequest("GET", 200, 0.02);
        collector.RecordRequest("POST", 500, 0.5);

        var rendered = collector.RenderPrometheus();

        Assert.Contains("http_requests_total{method=\"GET\",status=\"200\"} 2", rendered);
        Assert.Contains("http_requests_total{method=\"POST\",status=\"500\"} 1", rendered);
        Assert.Contains("http_request_duration_seconds_count{method=\"GET\"} 2", rendered);
        // 0.01 + 0.02 = 0.03 (formato invariante a la cultura).
        Assert.Contains("http_request_duration_seconds_sum{method=\"GET\"} 0.03", rendered);
    }

    [Fact]
    public void Render_EscapesLabelValues()
    {
        var collector = new MetricsCollector();
        collector.RecordRequest("GET\"\\", 200, 0.1);

        var rendered = collector.RenderPrometheus();

        Assert.Contains("method=\"GET\\\"\\\\\"", rendered);
        // El valor de la etiqueta nunca rompe la línea del formato.
        Assert.DoesNotContain("method=\"GET\"\n", rendered);
    }
}

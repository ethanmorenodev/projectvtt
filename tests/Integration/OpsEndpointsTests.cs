using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Endpoints de operación (ADR-026): `/health` (liveness, contrato
/// histórico), `/health/ready` (readiness con base de datos) y `/metrics`
/// (formato texto de Prometheus, sin dependencias ni datos sensibles).
/// </summary>
public class OpsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Health_KeepsHistoricContract()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReady_ChecksTheDatabase()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");
        response.EnsureSuccessStatusCode();
        // El writer por defecto de health checks responde en texto.
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Metrics_ExposesPrometheusText()
    {
        var response = await factory.CreateClient().GetAsync("/metrics");
        response.EnsureSuccessStatusCode();
        Assert.StartsWith(
            "text/plain", response.Content.Headers.ContentType?.MediaType ?? "");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("# TYPE process_uptime_seconds gauge", body);
        Assert.Contains("# TYPE http_requests_total counter", body);
        Assert.Contains("# TYPE http_request_duration_seconds summary", body);
    }

    [Fact]
    public async Task Metrics_CountsRequests()
    {
        var client = factory.CreateClient();
        await client.GetAsync("/health");
        await client.GetAsync("/health");

        var body = await (await client.GetAsync("/metrics")).Content.ReadAsStringAsync();

        // El contador registra las peticiones (GET /health y /metrics).
        Assert.Contains("http_requests_total{method=\"GET\",status=\"200\"}", body);
        Assert.Contains("http_request_duration_seconds_count{method=\"GET\"}", body);
    }

    [Fact]
    public async Task Metrics_DoNotLeakRequestDetails()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "nadie", password = "SUPER-SECRETO" });

        var body = await (await client.GetAsync("/metrics")).Content.ReadAsStringAsync();

        // Solo agregados: ni rutas ni credenciales.
        Assert.DoesNotContain("SUPER-SECRETO", body);
        Assert.DoesNotContain("/api/v1", body);
    }
}

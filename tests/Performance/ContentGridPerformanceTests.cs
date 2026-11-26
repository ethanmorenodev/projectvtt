using System.Net.Http.Json;
using Xunit.Abstractions;

namespace ProjectVTT.Tests.Performance;

/// <summary>
/// Rejilla de contenido (PMB-4.9): se aísla en su propia clase/fixture porque
/// las 100 lecturas paralelas dejan presión en el pool de hilos y contaminan
/// el p95 de las demás pruebas si comparten la misma API.
/// </summary>
public class ContentGridPerformanceTests(
    PerfApiFixture fixture, ITestOutputHelper output) : IClassFixture<PerfApiFixture>
{
    private const double P95ThresholdMs = 1000;

    [Fact]
    public async Task Rejilla_ConOchentaItems_TieneLatenciaAceptable()
    {
        var token = await fixture.RegisterAsync("perf_biblio", "e2e_pass_123");
        using var typeResponse = await fixture.SendAsync(
            "/api/v1/content/types", token, HttpMethod.Post, new
            {
                system = "dnd5e",
                key = $"race-{Guid.NewGuid():N}",
                displayName = "Raza",
                schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
            });
        typeResponse.EnsureSuccessStatusCode();
        var typeId = (await typeResponse.Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetString()!;

        const int itemCount = 80;
        for (var i = 0; i < itemCount; i++)
        {
            using var created = await fixture.SendAsync(
                "/api/v1/content/items", token, HttpMethod.Post, new
                {
                    typeId,
                    name = $"Raza de carga {i}",
                    properties = new { speed = 20 + i },
                    scope = "user",
                });
            created.EnsureSuccessStatusCode();
        }

        var durations = await PerfApiFixture.MeasureAsync(
            total: 100,
            parallelism: 20,
            operation: _ => fixture.SendAsync("/api/v1/content/items", token, HttpMethod.Get));
        output.WriteLine(
            $"rejilla: p50={PerfApiFixture.Percentile(durations, 0.5):F1} ms, " +
            $"p95={PerfApiFixture.Percentile(durations, 0.95):F1} ms, n={durations.Count}");

        var p95 = PerfApiFixture.Percentile(durations, 0.95);
        Assert.True(p95 < P95ThresholdMs, $"p95 demasiado alto: {p95:F1} ms");

        using var list = await fixture.SendAsync(
            "/api/v1/content/items", token, HttpMethod.Get);
        var json = await list.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(json.GetArrayLength() >= itemCount);
        Assert.True(json.EnumerateArray().All(x => x.TryGetProperty("usageCount", out _)));
    }
}

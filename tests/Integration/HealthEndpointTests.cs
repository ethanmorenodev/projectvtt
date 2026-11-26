using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Prueba humo: la API arranca y responde en /health sin requerir
/// dependencias externas más allá de la base de datos configurada.
/// </summary>
public class HealthEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Health_ReturnsOkWithServiceStatus()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("projectvtt-api", body.Service);
    }

    private sealed record HealthResponse(string Status, string Service);
}

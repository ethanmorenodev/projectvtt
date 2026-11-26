using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Concurrencia real sobre el estado compartido del servidor: varias peticiones
/// se lanzan a la vez con Task.WhenAll (no con Task.Delay) para comprobar que
/// solo una escritura gana y que ninguna acaba en 500.
/// </summary>
public class ConcurrencyTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private const int Transferencias = 10;
    private const int Escrituras = 8;

    [Fact]
    public async Task TransferenciasParalelasDelMismoItem_SoloUnaGana()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp concurrencia transfer");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CrearItemAsync(owner, sourceId, "Flechas", 5);

        var resultados = await Task.WhenAll(Enumerable.Range(0, Transferencias)
            .Select(_ => TransferirUnaUnidadAsync(owner, sourceId, targetId, itemId)));

        var estados = resultados.Select(r => r.Status).ToList();
        Assert.False(estados.Any(s => (int)s >= 500),
            $"El servidor devolvió 500: {Detalle(resultados)}");
        var ganadoras = estados.Count(s => s == HttpStatusCode.OK);
        Assert.True(ganadoras >= 1 && ganadoras <= 1,
            $"Se esperaba exactamente una transferencia ganadora de {Transferencias}; " +
            $"estados: {Detalle(resultados)}");
        Assert.All(resultados.Where(r => r.Status != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.Status));

        var origen = await CantidadTotalAsync(owner, sourceId);
        var destino = await CantidadTotalAsync(owner, targetId);
        Assert.True(origen + destino == 5,
            $"origen={origen}, destino={destino}; estados: {Detalle(resultados)}");
    }

    [Fact]
    public async Task EscriturasParalelasDeAnotaciones_SoloUnaVersionGana()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp concurrencia anotaciones");
        var sceneJson = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Escena concurrente");
        var sceneId = Guid.Parse(sceneJson.GetProperty("id").GetString()!);

        var resultados = await Task.WhenAll(Enumerable.Range(0, Escrituras)
            .Select(i => EscribirAnotacionAsync(owner, sceneId, i)));

        var estados = resultados.Select(r => r.Status).ToList();
        Assert.False(estados.Any(s => (int)s >= 500),
            $"El servidor devolvió 500: {Detalle(resultados)}");
        Assert.True(estados.Count(s => s == HttpStatusCode.OK) == 1,
            $"Se esperaba una sola escritura ganadora de {Escrituras}; estados: {Detalle(resultados)}");
        Assert.All(resultados.Where(r => r.Status != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.Status));

        var state = await (await owner.GetAsync($"/api/v1/scenes/{sceneId}/annotations"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, state.GetProperty("version").GetInt64());
        Assert.Single(state.GetProperty("items").EnumerateArray());
    }

    // --- Utilidades ------------------------------------------------------

    private sealed record Resultado(HttpStatusCode Status, string Cuerpo);

    private static string Detalle(IEnumerable<Resultado> resultados) =>
        string.Join(", ", resultados.Select(r => $"{(int)r.Status}: {r.Cuerpo[..Math.Min(160, r.Cuerpo.Length)]}"));

    private static async Task<Resultado> TransferirUnaUnidadAsync(
        HttpClient client, string sourceId, string targetId, Guid itemId)
    {
        // El DTO de transferencia no tiene commandId: el testigo es Version.
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId, quantity = 1, version = 0L });
        return new Resultado(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Resultado> EscribirAnotacionAsync(HttpClient client, Guid sceneId, int indice)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/annotations",
            new
            {
                commandId = Guid.NewGuid(),
                expectedVersion = 0L,
                annotationId = (Guid?)null,
                kind = "draw",
                points = new[] { new { x = indice * 10.0, y = (indice + 1) * 10.0 } },
                text = (string?)null,
                clear = false,
            });
        return new Resultado(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CrearItemAsync(
        HttpClient client, string actorId, string name, int quantity)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name, quantity });
        var inventory = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return inventory.GetProperty("items").EnumerateArray()
            .First().GetProperty("id").GetGuid();
    }

    private static async Task<int> CantidadTotalAsync(HttpClient client, string actorId)
    {
        var json = await (await client.GetAsync($"/api/v1/actors/{actorId}/items"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        return json.GetProperty("items").EnumerateArray()
            .Sum(item => item.GetProperty("quantity").GetInt32());
    }
}

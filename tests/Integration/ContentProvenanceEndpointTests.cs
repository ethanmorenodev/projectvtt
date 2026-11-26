using System.Net;
using System.Net.Http.Json;
using ProjectVTT.Api.Content;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Procedencia visible en el explorador (PMB-4.2, ADR-036): el DTO de item
/// expone <c>packKey</c>/<c>packItemKey</c> y el endpoint de paquetes
/// publica licencia y atribuciones para que el cliente pueda mostrar la
/// atribución obligatoria (CC BY 4.0). Los paquetes son metadatos globales
/// pero siguen requiriendo sesión.
/// </summary>
public class ContentProvenanceEndpointTests(ApiTestFactory factory)
    : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task LosPaquetes_SeanConLicenciaYAtribucion()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var packs = await client.GetFromJsonAsync<List<ContentPackDto>>(
            "/api/v1/content/packs");

        Assert.NotNull(packs);
        var srd = Assert.Single(packs!, p => p.Key == "dnd5e-srd-5.1");
        Assert.Equal("CC BY 4.0", srd.License);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", srd.LicenseUrl);
        Assert.Contains(srd.Attribution, a => a.Contains("Wizards of the Coast"));
        Assert.Equal("5.1", srd.Version);
    }

    [Fact]
    public async Task LosItemsDelSrd_ExponenLaIdentidadDeSuPaquete()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var items = await client.GetFromJsonAsync<List<ContentItemDto>>(
            "/api/v1/content/items?typeKey=race");

        Assert.NotNull(items);
        Assert.NotEmpty(items!);
        var dwarf = Assert.Single(items!, i => i.PackItemKey == "race/dwarf");
        Assert.Equal("dnd5e-srd-5.1", dwarf.PackKey);
        Assert.Equal("global", dwarf.Scope);
        Assert.Equal("published", dwarf.WorkflowState);
        // Los items creados sin paquete lo llevan a null (no inventan procedencia).
        Assert.All(items!, i => Assert.Equal("dnd5e-srd-5.1", i.PackKey));
    }

    [Fact]
    public async Task LosPaquetes_RequierenSesion()
    {
        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/content/packs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

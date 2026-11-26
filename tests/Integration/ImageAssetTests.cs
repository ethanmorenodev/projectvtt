using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Biblioteca de imágenes (PMB-4.7): subida con validación en servidor,
/// facetas, metadatos de licencia/atribución, permisos por dueño y
/// exportación de la nota de atribución (ADR-036).
/// </summary>
public class ImageAssetTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task SubirYListar_ConFacetasYPapel_DevuelveMetadatosYArchivo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var first = await UploadAsync(owner, "enano.png", "image/png", "miniatura",
            ["Personajes"], ["srd"], "CC BY 3.0", ["Icono de enano (CC BY 3.0)"]);
        var second = await UploadAsync(owner, "fondo.jpg", "image/jpeg", "ilustracion",
            ["Escenarios"], ["fondo"], "CC0", ["Fondo propio (CC0)"]);

        Assert.Equal("miniatura", first.GetProperty("role").GetString());
        Assert.Equal("Personajes", first.GetProperty("albums")[0].GetString());
        Assert.Equal("CC BY 3.0", first.GetProperty("license").GetString());

        var all = await ListAsync(owner);
        Assert.Equal(2, all.Length);
        Assert.Single(await ListAsync(owner, role: "ilustracion"));
        Assert.Single(await ListAsync(owner, album: "Escenarios"));
        Assert.Single(await ListAsync(owner, tag: "srd"));
        Assert.Single(await ListAsync(owner, license: "CC0"));
        Assert.Single(await ListAsync(owner, query: "fondo"));

        var id = first.GetProperty("id").GetGuid();
        using var single = await owner.GetAsync($"/api/v1/images/{id}");
        Assert.Equal("enano.png", (await single.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("fileName").GetString());

        using var file = await owner.GetAsync($"/api/v1/images/{id}/file");
        file.EnsureSuccessStatusCode();
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", file.Headers.TryGetValues(
            "X-Content-Type-Options", out var values) ? values.First() : null);
    }

    [Fact]
    public async Task Subida_ValidaFormatoYTamano_EnServidor()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();

        using var badType = new MultipartFormDataContent();
        using var text = new ByteArrayContent([1, 2, 3]);
        text.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        badType.Add(text, "file", "mal.txt");
        using var rejected = await owner.PostAsync("/api/v1/images", badType);
        await rejected.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var empty = new MultipartFormDataContent();
        using var zero = new ByteArrayContent([]);
        zero.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        empty.Add(zero, "file", "vacio.png");
        using var rejectedEmpty = await owner.PostAsync("/api/v1/images", empty);
        await rejectedEmpty.EnsureStatusAsync(HttpStatusCode.BadRequest);

        Assert.Empty(await ListAsync(owner));
    }

    [Fact]
    public async Task ActualizarYBorrar_SoloElDueno_PuedeModificarLaImagen()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var image = await UploadAsync(owner, "enano.png", "image/png", "miniatura",
            [], [], "Propietario", []);
        var id = image.GetProperty("id").GetGuid();

        using var stranger = factory.CreateClient();
        var token = await stranger.RegisterUserAsync(TestClientExtensions.NewUserName("otro"));
        stranger.SetBearerToken(token);

        using var strangerPatch = await stranger.PatchAsJsonAsync($"/api/v1/images/{id}",
            new { albums = new[] { "Robado" } });
        await strangerPatch.EnsureStatusAsync(HttpStatusCode.NotFound);
        using var strangerDelete = await stranger.DeleteAsync($"/api/v1/images/{id}");
        await strangerDelete.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var patch = await owner.PatchAsJsonAsync($"/api/v1/images/{id}", new
        {
            role = "icono",
            albums = new[] { "Interfaz" },
            tags = new[] { "ui" },
            focal = "50,30",
        });
        var updated = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("icono", updated.GetProperty("role").GetString());
        Assert.Equal("Interfaz", updated.GetProperty("albums")[0].GetString());
        Assert.Equal("50,30", updated.GetProperty("focal").GetString());

        using var delete = await owner.DeleteAsync($"/api/v1/images/{id}");
        await delete.EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Empty(await ListAsync(owner));
        using var gone = await owner.GetAsync($"/api/v1/images/{id}/file");
        await gone.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creditos_ExportaPaquetesYAssets_ConAtribuciones()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        await UploadAsync(owner, "enano.png", "image/png", "miniatura", [], [], "CC BY 3.0",
            ["Icono de enano de game-icons.net (CC BY 3.0)"]);

        using var response = await owner.GetAsync("/api/v1/credits/export");
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Wizards of the Coast", text);
        Assert.Contains("CC BY 4.0", text);
        Assert.Contains("game-icons.net", text);
        // Atribución de la iconografía de la interfaz (PMB-8.6b, CC BY 3.0).
        Assert.Contains("Lorc, Delapouite y colaboradores", text);
        Assert.Contains("http://creativecommons.org/licenses/by/3.0/", text);
    }

    [Fact]
    public async Task Sustituir_ConservaIdYReferencias_ConVariantes()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var image = await UploadAsync(owner, "enano.png", "image/png", "miniatura",
            [], [], "CC BY 3.0", ["Icono (CC BY 3.0)"]);
        var imageId = image.GetProperty("id").GetGuid();

        // Un item referencia la imagen por imageId (sin copiar el archivo).
        using var typeResponse = await owner.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });
        var typeId = (await typeResponse.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
        using var itemResponse = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Enano",
            properties = new { speed = 25 },
            imageId,
            scope = "user",
        });
        await itemResponse.EnsureStatusAsync(HttpStatusCode.Created);

        using var before = await owner.GetAsync($"/api/v1/images/{imageId}");
        Assert.Equal(1, (await before.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("referenceCount").GetInt32());

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([9, 8, 7, 6]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "enano-v2.png");
        using var thumb = new ByteArrayContent([5, 4, 3]);
        thumb.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(thumb, "thumb-sm", "enano-sm.png");
        using var replace = await owner.PostAsync($"/api/v1/images/{imageId}/replace", form);
        var updated = await replace.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal(imageId, updated.GetProperty("id").GetGuid());
        Assert.Equal("enano-v2.png", updated.GetProperty("fileName").GetString());
        Assert.Contains("thumb-sm", updated.GetProperty("variants")
            .EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(1, updated.GetProperty("referenceCount").GetInt32());

        // La referencia del item sigue siendo el mismo imageId.
        using var list = await owner.GetAsync("/api/v1/content/items");
        var item = (await list.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray()
            .Single(x => x.TryGetProperty("imageId", out var value)
                && value.ValueKind != JsonValueKind.Null
                && value.GetGuid() == imageId);
        Assert.Equal("Enano", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Filtros_PorPaqueteYLicencia()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        await UploadAsync(owner, "enano.png", "image/png", "miniatura", [], [], "CC BY 3.0", []);
        await UploadAsync(owner, "fondo.png", "image/png", "ilustracion", [], [], "CC0", []);

        Assert.Equal(2, (await ListAsync(owner)).Length);
        Assert.Single(await ListAsync(owner, license: "CC0"));
        Assert.Equal(2, (await ListAsync(owner, packKey: "propio")).Length);
        Assert.Empty(await ListAsync(owner, packKey: "dnd5e-srd-5.1"));
    }

    [Fact]
    public async Task Sustituir_ValidaFormatoYExigeDueno()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var image = await UploadAsync(owner, "enano.png", "image/png", "miniatura", [], [], "CC0", []);
        var imageId = image.GetProperty("id").GetGuid();

        using var stranger = factory.CreateClient();
        var token = await stranger.RegisterUserAsync(TestClientExtensions.NewUserName("otro"));
        stranger.SetBearerToken(token);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "otro.png");
        using var forbidden = await stranger.PostAsync($"/api/v1/images/{imageId}/replace", form);
        await forbidden.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var bad = new MultipartFormDataContent();
        using var text = new ByteArrayContent([1, 2, 3]);
        text.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        bad.Add(text, "file", "mal.txt");
        using var rejected = await owner.PostAsync($"/api/v1/images/{imageId}/replace", bad);
        await rejected.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<JsonElement> UploadAsync(
        HttpClient client, string name, string contentType, string role,
        string[] albums, string[] tags, string license, string[] attribution)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", name);
        form.Add(new StringContent(role), "role");
        form.Add(new StringContent(string.Join(',', albums)), "albums");
        form.Add(new StringContent(string.Join(',', tags)), "tags");
        form.Add(new StringContent(license), "license");
        form.Add(new StringContent(string.Join(',', attribution)), "attribution");
        using var response = await client.PostAsync("/api/v1/images", form);
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement[]> ListAsync(
        HttpClient client, string? role = null, string? album = null,
        string? tag = null, string? license = null, string? query = null,
        string? packKey = null)
    {
        var filters = new List<string>();
        if (role is not null) filters.Add($"role={role}");
        if (album is not null) filters.Add($"album={album}");
        if (tag is not null) filters.Add($"tag={tag}");
        if (license is not null) filters.Add($"license={license}");
        if (query is not null) filters.Add($"query={query}");
        if (packKey is not null) filters.Add($"packKey={packKey}");
        var url = "/api/v1/images" + (filters.Count == 0 ? string.Empty : "?" + string.Join('&', filters));
        using var response = await client.GetAsync(url);
        return (await response.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
    }
}

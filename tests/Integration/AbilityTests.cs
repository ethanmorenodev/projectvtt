using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Estructura de habilidades (PMB-5.2, ADR-040): activación, efectos tipados
/// con `auto`/`manual`, duración, usos, costes y requisitos. La semántica la
/// valida el servidor; el cliente solo envía datos.
/// </summary>
public class AbilityTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearHabilidadValida_ConEfectosTipadosYUso()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await AbilityTypeIdAsync(owner);

        using var response = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Bendición de prueba",
            properties = ValidProperties(),
            tags = new[] { "srd" },
            scope = "user",
        });
        var created = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var properties = created.GetProperty("properties");
        Assert.Equal("action", properties.GetProperty("activation").GetString());
        Assert.Equal("auto", properties.GetProperty("resolution").GetString());
        Assert.Equal(2, properties.GetProperty("effects")[0].GetProperty("value").GetInt32());
        Assert.Equal(3, properties.GetProperty("uses").GetProperty("max").GetInt32());
        Assert.Equal("draft", created.GetProperty("workflowState").GetString());
    }

    [Fact]
    public async Task Crear_RechazaActivacionEfectosYDuracionInvalidos()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = Guid.Parse(await AbilityTypeIdAsync(owner));

        using var badActivation = await PostAsync(owner, typeId, "activation", "reaccion");
        await badActivation.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var badDuration = await PostAsync(owner, typeId, "duration", "mucho");
        await badDuration.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var noEffects = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId,
            name = "Sin efectos",
            properties = Without("effects"),
            scope = "user",
        });
        await noEffects.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // Un kind no automatizable exige resolution=manual (ADR-040).
        using var unknownAuto = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId,
            name = "Efecto no automatizable",
            properties = new
            {
                activation = "action", duration = "instant", resolution = "auto",
                effects = new[] { new { key = "hechizo Personalizado", kind = "custom", value = 1 } },
            },
            scope = "user",
        });
        await unknownAuto.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // El mismo efecto con resolution=manual sí se acepta (resolución asistida).
        using var manual = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId,
            name = "Efecto asistido",
            properties = new
            {
                activation = "action", duration = "rounds:3", resolution = "manual",
                effects = new[] { new { key = "hechizo Personalizado", kind = "custom", value = 1 } },
            },
            scope = "user",
        });
        await manual.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Actualizar_ValidaLaEstructuraYSinCambiosInvalidos()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = Guid.Parse(await AbilityTypeIdAsync(owner));
        using var create = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, name = "Bendición", properties = ValidProperties(), scope = "user",
        });
        var id = (await create.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();

        using var badPatch = await owner.PatchAsJsonAsync($"/api/v1/content/items/{id}", new
        {
            properties = new
            {
                activation = "free", duration = "instant", resolution = "auto",
                effects = new[] { new { key = "bendecido", kind = "modifier", value = "mucho" } },
            },
        });
        await badPatch.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var read = await owner.GetAsync($"/api/v1/content/items/{id}");
        var unchanged = await read.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("action", unchanged.GetProperty("properties")
            .GetProperty("activation").GetString());

        using var goodPatch = await owner.PatchAsJsonAsync($"/api/v1/content/items/{id}", new
        {
            properties = new
            {
                activation = "bonus", duration = "instant", resolution = "manual",
                effects = new[] { new { key = "bendecido", kind = "modifier", value = 2 } },
            },
        });
        var updated = await goodPatch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("bonus", updated.GetProperty("properties").GetProperty("activation").GetString());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> AbilityTypeIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/content/types");
        var types = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return types.EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == "ability")
            .GetProperty("id").GetString()!;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, Guid typeId, string property, string value)
    {
        var properties = new JsonObject
        {
            ["activation"] = "action",
            ["duration"] = "instant",
            ["resolution"] = "auto",
            ["effects"] = new JsonArray
            {
                new JsonObject { ["key"] = "bendecido", ["kind"] = "modifier", ["value"] = 2 },
            },
        };
        properties[property] = value;
        return await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, name = "Habilidad", properties,
            scope = "user",
        });
    }

    private static JsonObject ValidProperties() => new()
    {
        ["activation"] = "action",
        ["duration"] = "concentration",
        ["resolution"] = "auto",
        ["effects"] = new JsonArray
        {
            new JsonObject
            {
                ["key"] = "bendecido", ["kind"] = "modifier", ["value"] = 2,
            },
        },
        ["uses"] = new JsonObject { ["max"] = 3, ["reset"] = "short-rest" },
        ["costs"] = new JsonArray
        {
            new JsonObject { ["key"] = "spellSlot", ["amount"] = 1 },
        },
        ["requirements"] = new JsonArray { "nivel 3" },
    };

    private static JsonObject Without(string key)
    {
        var properties = ValidProperties();
        properties.Remove(key);
        return properties;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// ChoiceGroups (PMB-5.3, CONTENT_MODEL §9): el contenido declara grupos de
/// elección y el creador valida la selección en servidor (ADR-034/040).
/// </summary>
public class ChoiceGroupTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearContenidoConGrupoValido()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        using var response = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Clase con elecciones",
            properties = Properties(Group(2, ["A", "B", "C"])),
            scope = "user",
        });
        var created = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var group = created.GetProperty("properties").GetProperty("choiceGroups")[0];
        Assert.Equal("skills", group.GetProperty("id").GetString());
        Assert.Equal(2, group.GetProperty("count").GetInt32());
        Assert.True(group.GetProperty("mandatory").GetBoolean());
    }

    [Fact]
    public async Task Crear_RechazaDefinicionesInvalidas()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = Guid.Parse(await CreateTypeAsync(owner));

        using var tooMany = await PostAsync(owner, typeId, Group(4, ["A", "B"]));
        await tooMany.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var badTiming = Group(1, ["A"]);
        badTiming["timing"] = "cuandoQuiera";
        using var invalidTiming = await PostAsync(owner, typeId, badTiming);
        await invalidTiming.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var noOptions = Group(1, []);
        using var emptyOptions = await PostAsync(owner, typeId, noOptions);
        await emptyOptions.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var duplicateIds = Properties(Group(1, ["A"]), Group(1, ["B"]));
        using var duplicate = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, name = "Duplicado", properties = duplicateIds, scope = "user",
        });
        await duplicate.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creador_ValidaLaSeleccionContraElGrupoDelContenido()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var dwarf = await FindIdAsync(client, "race", "Dwarf");
        var fighter = await FindIdAsync(client, "class", "Fighter");
        var background = await FindIdAsync(client, "background", "Soldier");

        using var valid = await PreviewAsync(client, dwarf, fighter, background,
            ["Stealth", "Perception"]);
        Assert.True((await valid.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("valid").GetBoolean());

        using var wrongCount = await PreviewAsync(client, dwarf, fighter, background, ["Stealth"]);
        var invalid = await wrongCount.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(invalid.GetProperty("valid").GetBoolean());
        var messages = invalid.GetProperty("stepResults").EnumerateArray()
            .SelectMany(step => step.GetProperty("errors").EnumerateArray()
                .Select(error => error.GetString() ?? string.Empty))
            .ToArray();
        Assert.Contains(messages, message => message.Contains("Debes elegir 2 opción(es)"));

        using var unknownOption = await PreviewAsync(client, dwarf, fighter, background,
            ["Stealth", "Opción Inventada"]);
        var unknown = await unknownOption.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(unknown.GetProperty("valid").GetBoolean());

        using var duplicated = await PreviewAsync(client, dwarf, fighter, background,
            ["Stealth", "stealth"]);
        Assert.False((await duplicated.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("valid").GetBoolean());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new
            {
                fields = new object[]
                {
                    new { key = "name", type = "string", required = false },
                    new { key = "choiceGroups", type = "list", required = false },
                },
            },
        });
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private static JsonObject Group(int count, string[] options) => new()
    {
        ["id"] = "skills",
        ["title"] = "Habilidades",
        ["count"] = count,
        ["mandatory"] = true,
        ["timing"] = "creation",
        ["changeable"] = false,
        ["options"] = new JsonArray(options.Select(value => (JsonNode?)JsonValue.Create(value))
            .ToArray()),
    };

    private static JsonObject Properties(params JsonObject[] groups) => new()
    {
        ["choiceGroups"] = new JsonArray(groups.Select(group => (JsonNode?)group).ToArray()),
    };

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, Guid typeId, JsonObject group) =>
        client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, name = "Clase", properties = Properties(group), scope = "user",
        });

    private static async Task<string> FindIdAsync(
        HttpClient client, string typeKey, string name)
    {
        using var response = await client.GetAsync(
            $"/api/v1/content/items?system=dnd5e&typeKey={typeKey}");
        var items = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return items.EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == name)
            .GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> PreviewAsync(
        HttpClient client, string raceId, string classId, string backgroundId, string[] skills) =>
        client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps = new JsonObject
            {
                ["identity"] = new JsonObject { ["name"] = "Elenar" },
                ["race"] = new JsonObject { ["itemId"] = raceId },
                ["class"] = new JsonObject { ["itemId"] = classId },
                ["background"] = new JsonObject { ["itemId"] = backgroundId },
                ["attributes"] = new JsonObject
                {
                    ["method"] = "manual",
                    ["values"] = new JsonObject
                    {
                        ["strength"] = 15, ["dexterity"] = 12, ["constitution"] = 14,
                        ["intelligence"] = 10, ["wisdom"] = 13, ["charisma"] = 8,
                    },
                },
                ["skills"] = new JsonObject
                {
                    ["chosen"] = new JsonArray(skills.Select(
                        value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                },
            },
        });
}

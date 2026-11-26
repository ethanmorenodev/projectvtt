using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Recorridos completos de la nueva experiencia (EXP-1/EXP-3) sobre los
/// contratos existentes: retrato del personaje persistente y clase homebrew
/// creada, publicada y versionada sin reglas duplicadas en el cliente.
/// </summary>
public class ExperienceFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Actor_RetratoSePersisteYCambiaSinPerderLaFicha()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña retrato");
        var portraitA = Guid.NewGuid();

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", new
            {
                name = "Elenar",
                rulesetKey = "dnd5e",
                attributes = new Dictionary<string, int>
                {
                    ["strength"] = 12, ["dexterity"] = 14, ["constitution"] = 14,
                    ["intelligence"] = 10, ["wisdom"] = 11, ["charisma"] = 9,
                },
                resources = new Dictionary<string, object>(),
                kind = "pc",
                level = 1,
                portraitId = portraitA,
            });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var actorId = json.GetProperty("id").GetString()!;
        Assert.Equal(portraitA.ToString(), json.GetProperty("portraitId").GetString());

        var read = await owner.GetAsync($"/api/v1/actors/{actorId}");
        var before = await read.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(portraitA.ToString(), before.GetProperty("portraitId").GetString());
        Assert.Equal("Elenar", before.GetProperty("name").GetString());

        var portraitB = Guid.NewGuid();
        var patch = await owner.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 0L, portraitId = portraitB });
        var patched = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(portraitB.ToString(), patched.GetProperty("portraitId").GetString());
        Assert.Equal("Elenar", patched.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Borrador_EncuadreDelRetratoSePersiste()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña encuadre");
        var crop = new { scale = 1.35, x = 38, y = 62 };

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", new
            {
                name = "Elenar",
                rulesetKey = "dnd5e",
                attributes = new Dictionary<string, int>
                {
                    ["strength"] = 12, ["dexterity"] = 14, ["constitution"] = 14,
                    ["intelligence"] = 10, ["wisdom"] = 11, ["charisma"] = 9,
                },
                resources = new Dictionary<string, object>(),
                kind = "pc",
                level = 1,
                build = new { step = 1, portraitCrop = crop },
            });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var actorId = json.GetProperty("id").GetString()!;

        var read = (await (await owner.GetAsync($"/api/v1/actors/{actorId}"))
            .EnsureStatusAsync(HttpStatusCode.OK));
        var saved = read.GetProperty("build").GetProperty("portraitCrop");
        Assert.Equal(1.35, saved.GetProperty("scale").GetDouble());
        Assert.Equal(38, saved.GetProperty("x").GetInt32());
        Assert.Equal(62, saved.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task ClaseHomebrew_CrearBorradorPublicarYGenerarRevision()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var types = (await (await owner.GetAsync("/api/v1/content/types"))
            .EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        var classType = Assert.Single(types, t => t.GetProperty("key").GetString() == "class");
        var typeId = classType.GetProperty("id").GetString()!;

        var create = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Guardián del Umbral",
            summary = "Defensa y control",
            description = "Concepto: sella brechas entre mundos.",
            properties = new
            {
                hitDie = 10,
                primaryAbilities = new[] { "Constitución" },
                savingThrows = new[] { "Fuerza", "Sabiduría" },
                skillChoices = 2,
                armorProficiencies = new[] { "Ligera", "Media" },
                weaponProficiencies = new[] { "Simples" },
            },
            scope = "user",
        });
        var draft = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var id = draft.GetProperty("id").GetString()!;
        Assert.Equal("draft", draft.GetProperty("workflowState").GetString());
        Assert.Equal(10, draft.GetProperty("properties").GetProperty("hitDie").GetInt32());

        var before = (await (await owner.GetAsync($"/api/v1/content/items/{id}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.OK)).GetArrayLength();
        Assert.Equal(0, before);

        await (await owner.PostAsJsonAsync($"/api/v1/content/items/{id}/publish", new { message = (string?)null }))
            .EnsureStatusAsync(HttpStatusCode.OK);

        var published = (await (await owner.GetAsync($"/api/v1/content/items/{id}"))
            .EnsureStatusAsync(HttpStatusCode.OK));
        Assert.Equal("published", published.GetProperty("workflowState").GetString());
        var after = (await (await owner.GetAsync($"/api/v1/content/items/{id}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.OK)).GetArrayLength();
        Assert.Equal(before + 1, after);
    }
}

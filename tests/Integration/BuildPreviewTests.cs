using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Previsualización de builds (PMB-3.2, ADR-034): las reglas viven solo en
/// servidor — validación por paso, contenido real del SRD con herencia
/// resuelta (subrazas) y estadísticas derivadas (compra de puntos incluida,
/// decisión del propietario 2026-11-10).
/// </summary>
public class BuildPreviewTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static async Task<string> FindItemIdAsync(HttpClient client, string typeKey, string name)
    {
        var response = await client.GetAsync($"/api/v1/content/items?system=dnd5e&typeKey={typeKey}");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        foreach (var item in json.EnumerateArray())
        {
            if (item.GetProperty("name").GetString() == name)
            {
                return item.GetProperty("id").GetString()!;
            }
        }
        throw new InvalidOperationException($"Contenido '{name}' no encontrado ({typeKey}).");
    }

    private static JsonObject BuildSteps(string raceId, string classId, string backgroundId) => new()
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
            ["chosen"] = new JsonArray("Stealth", "Perception", "Athletics", "Deception"),
        },
    };

    [Fact]
    public async Task BuildValido_DerivaEstadisticasConLaHerenciaDeLaSubraza()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Contenido real del paquete SRD (razas con subrazas).
        var mountainDwarf = await FindItemIdAsync(client, "race", "Mountain Dwarf");
        var rogue = await FindItemIdAsync(client, "class", "Rogue");
        var criminal = await FindItemIdAsync(client, "background", "Criminal");

        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps = BuildSteps(mountainDwarf, rogue, criminal),
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.True(preview.GetProperty("valid").GetBoolean());
        var derived = preview.GetProperty("derivedStats");
        // Enano de montaña: 15 base + 2 de la subraza (extend) = 17.
        Assert.Equal(17, derived.GetProperty("abilities")
            .GetProperty("strength").GetProperty("score").GetInt32());
        Assert.Equal(3, derived.GetProperty("abilities")
            .GetProperty("strength").GetProperty("modifier").GetInt32());
        // Enano: 14 base + 2 racial = 16.
        Assert.Equal(16, derived.GetProperty("abilities")
            .GetProperty("constitution").GetProperty("score").GetInt32());
        // Herencia: velocidad enana (25) e idiomas efectivos.
        Assert.Equal(25, derived.GetProperty("speed").GetInt32());
        var languages = derived.GetProperty("languages").EnumerateArray()
            .Select(x => x.GetString()).ToArray();
        Assert.Contains("Dwarvish", languages);
    }

    [Fact]
    public async Task BuildInvalido_ReportaErroresPorPasoSinAplicarSilenciosamente()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var woodElf = await FindItemIdAsync(client, "race", "Wood Elf");
        var bard = await FindItemIdAsync(client, "class", "Bard");
        var sage = await FindItemIdAsync(client, "background", "Sage");

        // Sin nombre, atributos fuera de rango y demasiadas habilidades.
        var steps = BuildSteps(woodElf, bard, sage);
        steps.Remove("identity");
        steps["attributes"]!["values"]!["strength"] = 25;
        steps["skills"] = new JsonObject
        {
            ["chosen"] = new JsonArray("Stealth"),
        };

        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.False(preview.GetProperty("valid").GetBoolean());
        var allErrors = preview.GetProperty("stepResults").EnumerateArray()
            .SelectMany(s => s.GetProperty("errors").EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty))
            .ToArray();
        Assert.Contains(allErrors, e => e.Contains("Falta el paso 'identity'"));
        Assert.Contains(allErrors, e => e.Contains("entre 3 y 20"));
        Assert.Contains(allErrors, e => e.Contains("3 habilidades"));

        // El elfo silvato sí resuelve su velocidad por herencia (override).
        var raceResult = preview.GetProperty("stepResults").EnumerateArray()
            .Single(s => s.GetProperty("step").GetString() == "race");
        Assert.True(raceResult.GetProperty("ok").GetBoolean());
        Assert.Equal(35, preview.GetProperty("derivedStats")
            .GetProperty("speed").GetInt32());
    }

    [Fact]
    public async Task CompraDePuntos_ValidaPresupuestoYRangos()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var human = await FindItemIdAsync(client, "race", "Human");
        var fighter = await FindItemIdAsync(client, "class", "Fighter");
        var soldier = await FindItemIdAsync(client, "background", "Soldier");

        var steps = BuildSteps(human, fighter, soldier);
        steps["attributes"] = new JsonObject
        {
            ["method"] = "pointBuy",
            ["values"] = new JsonObject
            {
                ["strength"] = 15, ["dexterity"] = 15, ["constitution"] = 15,
                ["intelligence"] = 15, ["wisdom"] = 15, ["charisma"] = 15,
            },
        };

        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var allErrors = preview.GetProperty("stepResults").EnumerateArray()
            .SelectMany(s => s.GetProperty("errors").EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty))
            .ToArray();
        Assert.Contains(allErrors, e => e.Contains("presupuesto"));

        // Con valores legales (15/14/13/12/10/8 = 27 puntos) y la cuota de
        // habilidades del Fighter (2): válido.
        steps["attributes"] = new JsonObject
        {
            ["method"] = "pointBuy",
            ["values"] = new JsonObject
            {
                ["strength"] = 15, ["dexterity"] = 14, ["constitution"] = 13,
                ["intelligence"] = 12, ["wisdom"] = 10, ["charisma"] = 8,
            },
        };
        steps["skills"] = new JsonObject
        {
            ["chosen"] = new JsonArray("Athletics", "Perception"),
        };
        var okResponse = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var okPreview = await okResponse.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(okPreview.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task BlueprintsYVariantes_ElServidorEsLaUnicaAutoridad()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // El blueprint dnd5e está disponible para el asistente.
        var blueprints = await client.GetAsync("/api/v1/characters/blueprints?system=dnd5e");
        var list = await blueprints.EnsureStatusAsync(HttpStatusCode.OK);
        var standard = list.EnumerateArray()
            .Single(b => b.GetProperty("key").GetString() == "standard");
        Assert.Equal(9, standard.GetProperty("steps").GetArrayLength());

        // Una variante que no pertenece a la raza elegida se rechaza.
        var elf = await FindItemIdAsync(client, "race", "Elf");
        var mountainDwarf = await FindItemIdAsync(client, "race", "Mountain Dwarf");
        var wizard = await FindItemIdAsync(client, "class", "Wizard");
        var acolyte = await FindItemIdAsync(client, "background", "Acolyte");
        var steps = BuildSteps(elf, wizard, acolyte);
        steps["race"] = new JsonObject
        {
            ["itemId"] = elf,
            ["variantItemId"] = mountainDwarf,
        };

        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var raceResult = preview.GetProperty("stepResults").EnumerateArray()
            .Single(s => s.GetProperty("step").GetString() == "race");
        Assert.False(raceResult.GetProperty("ok").GetBoolean());
        Assert.Contains(
            raceResult.GetProperty("errors").EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty),
            e => e.Contains("no pertenece"));
    }

    private static JsonElement PendingChoice(JsonElement derived) =>
        derived.GetProperty("pendingBonuses").EnumerateArray()
            .Single(p => p.TryGetProperty("options", out _));

    [Fact]
    public async Task BonusChoice_SinEleccion_OfreceOpcionesYNoAplica()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var halfElf = await FindItemIdAsync(client, "race", "Half-Elf");
        var rogue = await FindItemIdAsync(client, "class", "Rogue");
        var criminal = await FindItemIdAsync(client, "background", "Criminal");

        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps = BuildSteps(halfElf, rogue, criminal),
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(preview.GetProperty("valid").GetBoolean());

        var derived = preview.GetProperty("derivedStats");
        var pending = PendingChoice(derived);
        Assert.False(pending.GetProperty("applied").GetBoolean());
        var options = pending.GetProperty("options").EnumerateArray()
            .Select(o => o.GetString()).ToArray();
        Assert.Equal(new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" }, options);
        Assert.Equal(12, derived.GetProperty("abilities")
            .GetProperty("dexterity").GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task BonusChoice_ConEleccion_AplicaLaBonificacionALaHabilidadElegida()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var halfElf = await FindItemIdAsync(client, "race", "Half-Elf");
        var rogue = await FindItemIdAsync(client, "class", "Rogue");
        var criminal = await FindItemIdAsync(client, "background", "Criminal");

        var steps = BuildSteps(halfElf, rogue, criminal);
        steps["bonusChoices"] = new JsonObject
        {
            ["Two other abilities of your choice"] = "dexterity",
        };
        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(preview.GetProperty("valid").GetBoolean());

        var derived = preview.GetProperty("derivedStats");
        Assert.Equal(13, derived.GetProperty("abilities")
            .GetProperty("dexterity").GetProperty("score").GetInt32());
        var pending = PendingChoice(derived);
        Assert.True(pending.GetProperty("applied").GetBoolean());
        Assert.Equal("dexterity", pending.GetProperty("ability").GetString());
    }

    [Fact]
    public async Task BonusChoice_HabilidadDesconocida_SeIgnoraSinAplicar()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var halfElf = await FindItemIdAsync(client, "race", "Half-Elf");
        var rogue = await FindItemIdAsync(client, "class", "Rogue");
        var criminal = await FindItemIdAsync(client, "background", "Criminal");

        var steps = BuildSteps(halfElf, rogue, criminal);
        steps["bonusChoices"] = new JsonObject
        {
            ["Two other abilities of your choice"] = "no-existe",
        };
        var response = await client.PostAsJsonAsync("/api/v1/characters/build/preview", new
        {
            system = "dnd5e",
            steps,
        });
        var preview = await response.EnsureStatusAsync(HttpStatusCode.OK);

        var derived = preview.GetProperty("derivedStats");
        Assert.False(PendingChoice(derived).GetProperty("applied").GetBoolean());
        Assert.Equal(12, derived.GetProperty("abilities")
            .GetProperty("dexterity").GetProperty("score").GetInt32());
    }
}

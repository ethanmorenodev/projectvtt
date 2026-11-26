using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Paquete seed `dnd5e-srd-5.1` (PMB-P.4, ADR-037): contenido real del
/// SRD con procedencia CC BY 4.0, importado al arrancar de forma
/// idempotente; si el contenido difiere, la actualización publica una
/// revisión (nunca silenciosa).
/// </summary>
public class ContentSeedTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task ElPaqueteSeed_ImportaContenidoRealConProcedencia()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pack = await db.ContentPacks.AsNoTracking()
            .SingleAsync(x => x.Key == "dnd5e-srd-5.1");
        Assert.Equal("CC BY 4.0", pack.License);
        Assert.Contains("Wizards of the Coast", pack.AttributionJson);
        Assert.Contains("System Reference Document 5.1", pack.AttributionJson);

        // Tipos del creador (mínimo, CONTENT_MODEL §3).
        var typeKeys = await db.ContentTypes.AsNoTracking()
            .Where(x => x.System == "dnd5e")
            .Select(x => x.Key)
            .ToListAsync();
        Assert.Contains("race", typeKeys);
        Assert.Contains("class", typeKeys);
        Assert.Contains("background", typeKeys);

        // Las 9 razas base del SRD 5.1, globales y publicadas.
        var races = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey!.StartsWith("race/")
                && x.ParentId == null)
            .ToListAsync();
        Assert.Equal(9, races.Count);
        Assert.All(races, race =>
        {
            Assert.Equal(ContentScope.Global, race.Scope);
            Assert.Equal(ContentWorkflowState.Published, race.WorkflowState);
        });
        Assert.Contains(races, x => x.PackItemKey == "race/dwarf" && x.Name == "Dwarf");
        Assert.Contains(races, x => x.PackItemKey == "race/tiefling" && x.Name == "Tiefling");

        // Datos reales del SRD (bonificaciones, velocidad, idiomas).
        var elf = races.Single(x => x.PackItemKey == "race/elf");
        Assert.Contains("\"Dexterity\"", elf.PropertiesJson);
        Assert.Contains("\"Elvish\"", elf.PropertiesJson);

        // Las 9 subrazas del SRD como variantes del árbol, con herencia
        // (Dwarf 2, Elf 3, Halfling 2, Gnome 2).
        var subraces = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.ParentId != null)
            .ToListAsync();
        Assert.Equal(9, subraces.Count);
        var woodElf = subraces.Single(x => x.PackItemKey == "race/elf/wood-elf");
        Assert.Equal(elf.Id, woodElf.ParentId);
        Assert.Contains("\"speed\":\"override\"", woodElf.InheritanceJson);
        Assert.Contains("\"abilityBonuses\":\"extend\"", woodElf.InheritanceJson);

        // La herencia se resuelve raíz → hoja: la velocidad del elfo
        // silvato manda (35) y las bonificaciones/rasgos se acumulan.
        static (JsonObject, Dictionary<string, string>) Parse(System.Text.Json.Nodes.JsonNode? properties, string inheritanceJson)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (System.Text.Json.Nodes.JsonNode.Parse(inheritanceJson) is JsonObject obj)
            {
                foreach (var (key, value) in obj)
                {
                    dict[key] = value!.GetValue<string>();
                }
            }
            return (properties!.AsObject(), dict);
        }

        var chain = new[]
        {
            Parse(System.Text.Json.Nodes.JsonNode.Parse(elf.PropertiesJson), elf.InheritanceJson),
            Parse(System.Text.Json.Nodes.JsonNode.Parse(woodElf.PropertiesJson), woodElf.InheritanceJson),
        };
        var effective = ContentResolver.ResolveEffective(chain);
        Assert.Equal(35, effective["speed"]!.GetValue<double>());
        Assert.Equal(2, effective["abilityBonuses"]!.AsArray().Count);
        Assert.True(effective["traits"]!.AsArray().Count >= 6);

        // Las 12 clases del SRD 5.1 con sus datos (dado, salvaciones…).
        var classes = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey!.StartsWith("class/"))
            .ToListAsync();
        Assert.Equal(12, classes.Count);
        var rogue = classes.Single(x => x.PackItemKey == "class/rogue");
        Assert.Contains("\"hitDie\":8", rogue.PropertiesJson);
        Assert.Contains("\"skillChoices\":4", rogue.PropertiesJson);
        var wizard = classes.Single(x => x.PackItemKey == "class/wizard");
        Assert.Contains("\"Intelligence\"", wizard.PropertiesJson);
        Assert.Contains("\"Wisdom\"", wizard.PropertiesJson);

        // Los 13 trasfondos del SRD 5.1 con su rasgo característico.
        var backgrounds = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey!.StartsWith("background/"))
            .ToListAsync();
        Assert.Equal(13, backgrounds.Count);
        var acolyte = backgrounds.Single(x => x.PackItemKey == "background/acolyte");
        Assert.Contains("Shelter of the Faithful", acolyte.PropertiesJson);
        var soldier = backgrounds.Single(x => x.PackItemKey == "background/soldier");
        Assert.Contains("Vehicles (land)", soldier.PropertiesJson);

        // Condiciones del SRD 5.1 (Fase 4): 15 estados con descripción real.
        var conditions = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey!.StartsWith("condition/"))
            .ToListAsync();
        Assert.Equal(15, conditions.Count);
        var blinded = conditions.Single(x => x.PackItemKey == "condition/blinded");
        Assert.Contains("automatically fails any ability check", blinded.PropertiesJson);

        // Subclases del SRD 5.1 (Fase 4): 12 arquetipos con su clase y rasgos.
        var subclasses = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey!.StartsWith("subclass/"))
            .ToListAsync();
        Assert.Equal(12, subclasses.Count);
        var champion = subclasses.Single(x => x.PackItemKey == "subclass/champion");
        Assert.Contains("\"classKey\":\"fighter\"", champion.PropertiesJson);
    }

    [Fact]
    public async Task ElPaquete5_2_ImportaContenidoRealConProcedencia()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pack = await db.ContentPacks.AsNoTracking()
            .SingleAsync(x => x.Key == "dnd5e-srd-5.2");
        Assert.Equal("CC BY 4.0", pack.License);
        Assert.Contains("System Reference Document 5.2.1", pack.AttributionJson);

        var items = await db.ContentItems.AsNoTracking()
            .Where(x => x.PackKey == "dnd5e-srd-5.2")
            .ToListAsync();
        Assert.Equal(100, items.Count);
        Assert.Equal(40, items.Count(x => x.PackItemKey!.StartsWith("ability/")));
        Assert.Equal(30, items.Count(x => x.PackItemKey!.StartsWith("creature/")));
        Assert.Equal(30, items.Count(x => x.PackItemKey!.StartsWith("item/")));
        Assert.All(items, item =>
        {
            Assert.Equal(ContentScope.Global, item.Scope);
            Assert.Equal(ContentWorkflowState.Published, item.WorkflowState);
        });
        Assert.Contains(items, x => x.PackItemKey == "ability/acid-arrow" && x.Name == "Acid Arrow");
        Assert.Contains(items, x => x.PackItemKey == "item/acid");
        Assert.Contains(items, x => x.PackItemKey == "creature/aboleth");
    }

    [Fact]
    public async Task ElSeed_EsIdempotenteYAlDiferirPublicaRevision()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var elf = await db.ContentItems
            .SingleAsync(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey == "race/elf");
        var before = await db.ContentRevisions.AsNoTracking()
            .CountAsync(x => x.ItemId == elf.Id);

        // Sin cambios: segunda importación sin revisiones nuevas.
        await ContentSeedLoader.ImportAsync(factory.Services);
        await using var verify1 = factory.Services.CreateAsyncScope();
        var db1 = verify1.ServiceProvider.GetRequiredService<AppDbContext>();
        var afterIdle = await db1.ContentRevisions.AsNoTracking()
            .CountAsync(x => x.ItemId == elf.Id);
        Assert.Equal(before, afterIdle);

        // Con cambios: la restauración publica una revisión nueva (visible).
        var tampered = await db1.ContentItems
            .SingleAsync(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey == "race/elf");
        tampered.Name = "Elfo (editado a mano)";
        await db1.SaveChangesAsync();

        await ContentSeedLoader.ImportAsync(factory.Services);
        await using var verify2 = factory.Services.CreateAsyncScope();
        var db2 = verify2.ServiceProvider.GetRequiredService<AppDbContext>();
        var restored = await db2.ContentItems.AsNoTracking()
            .SingleAsync(x => x.PackKey == "dnd5e-srd-5.1" && x.PackItemKey == "race/elf");
        Assert.Equal("Elf", restored.Name);
        Assert.Equal(before + 1, restored.CurrentRevision);
    }
}

using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Paquetes de contenido (PMB-P.8, ADR-036): registro idempotente,
/// identidad (paquete, key), reimportación con resolución explícita de
/// conflictos y publicación nunca silenciosa. Cada prueba usa su propia
/// identidad (la BD es compartida por la clase).
/// </summary>
public class ContentPackTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static CreateContentPackRequest PackRequest(string key) => new(
        Key: key,
        Name: "D&D 5e SRD 5.1",
        Version: "5.1",
        System: "dnd5e",
        SourceUrl: "https://www.dndbeyond.com/sources/dnd/srd-5-1",
        License: "CC BY 4.0",
        LicenseUrl: "https://creativecommons.org/licenses/by/4.0/",
        Attribution: ["Wizards of the Coast, System Reference Document 5.1 (CC BY 4.0)"]);

    private static ImportContentItemRequest ItemRequest(
        string itemKey, string name = "Humano") => new(
        TypeId: null,
        ItemKey: itemKey,
        Name: name,
        Summary: "Versátil y ambicioso",
        Description: null,
        Tags: ["raza"],
        Properties: JsonNode.Parse("""{"speed":30}"""),
        Inheritance: new Dictionary<string, string>(),
        Message: "Importación SRD");

    /// <summary>Registra el paquete de la prueba y su tipo; devuelve claves.</summary>
    private static async Task<(string PackKey, ContentType Type)> SeedAsync(
        ContentPackService packs, string packKey, string itemPrefix)
    {
        await packs.RegisterAsync(PackRequest(packKey));
        var type = await packs.EnsureTypeAsync(
            "dnd5e", $"race-{itemPrefix}", "Raza",
            JsonNode.Parse("""{"fields":[{"key":"speed","type":"number"}]}"""));
        return (packKey, type);
    }

    [Fact]
    public async Task Registrar_EsIdempotentePorClave()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var key = $"pack-{Guid.NewGuid():N}";

        await packs.RegisterAsync(PackRequest(key));
        await packs.RegisterAsync(PackRequest(key) with { Name = "D&D 5e SRD 5.1 (revisado)" });

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ContentPacks.AsNoTracking()
            .Where(x => x.Key == key)
            .ToListAsync();
        Assert.Single(stored);
        Assert.Equal("D&D 5e SRD 5.1 (revisado)", stored[0].Name);
        Assert.Contains("Wizards of the Coast", stored[0].AttributionJson);
    }

    [Fact]
    public async Task Importar_CreaItemGlobalPublicadoConIdentidadDePaquete()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var id = Guid.NewGuid().ToString("N");
        var (packKey, type) = await SeedAsync(packs, $"pack-{id}", id);
        var itemKey = $"race/humano-{id}";

        var item = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey) with { TypeId = type.Id },
            ImportConflictStrategy.Keep);

        Assert.Equal(ContentScope.Global, item.Scope);
        Assert.Equal(ContentWorkflowState.Published, item.WorkflowState);
        Assert.Equal(1, item.CurrentRevision);
        Assert.Equal(packKey, item.PackKey);
        Assert.Equal(itemKey, item.PackItemKey);
    }

    [Fact]
    public async Task Reimportar_KeepConservaYUpdatePublicaRevisionNueva()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var id = Guid.NewGuid().ToString("N");
        var (packKey, type) = await SeedAsync(packs, $"pack-{id}", id);
        var itemKey = $"race/humano-{id}";

        var item = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey) with { TypeId = type.Id },
            ImportConflictStrategy.Keep);

        // Keep: nada cambia aunque el contenido entrante difiera.
        var kept = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey, "Humano (cambiado)") with { TypeId = type.Id },
            ImportConflictStrategy.Keep);
        Assert.Equal(item.Id, kept.Id);
        Assert.Equal("Humano", kept.Name);
        Assert.Equal(1, kept.CurrentRevision);

        // Update: nunca silencioso — publica la revisión 2.
        var updated = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey, "Humano (cambiado)") with { TypeId = type.Id },
            ImportConflictStrategy.Update);
        Assert.Equal(item.Id, updated.Id);
        Assert.Equal("Humano (cambiado)", updated.Name);
        Assert.Equal(2, updated.CurrentRevision);

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var revisions = await db.ContentRevisions.AsNoTracking()
            .Where(x => x.ItemId == item.Id)
            .ToListAsync();
        Assert.Equal(2, revisions.Count);
    }

    [Fact]
    public async Task Reimportar_CopyConservaElAnteriorYCreaCopiaTrazable()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var id = Guid.NewGuid().ToString("N");
        var (packKey, type) = await SeedAsync(packs, $"pack-{id}", id);
        var itemKey = $"race/humano-{id}";

        var original = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey) with { TypeId = type.Id },
            ImportConflictStrategy.Keep);
        var copy = await packs.ImportItemAsync(
            packKey, ItemRequest(itemKey, "Humano (v2)") with { TypeId = type.Id },
            ImportConflictStrategy.Copy);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Humano", original.Name);
        Assert.Equal("Humano (v2)", copy.Name);
        Assert.Equal(itemKey, original.PackItemKey);
        Assert.StartsWith($"{itemKey}#copia", copy.PackItemKey);
    }

    [Fact]
    public async Task Importar_PermiteCategoriaComoPadre()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var id = Guid.NewGuid().ToString("N");
        var (packKey, type) = await SeedAsync(packs, $"pack-{id}", id);
        var category = await packs.EnsureTypeAsync("dnd5e", "category", "Categoría",
            JsonNode.Parse("""{"fields":[]}"""));
        var parent = await packs.ImportItemAsync(packKey,
            ItemRequest("category") with { TypeId = category.Id, Properties = new JsonObject() },
            ImportConflictStrategy.Keep);
        var child = await packs.ImportItemAsync(packKey,
            ItemRequest("child") with { TypeId = type.Id, ParentItemKey = "category" },
            ImportConflictStrategy.Keep);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(type.Id, child.TypeId);
    }}

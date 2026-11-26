using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Modelado del contenido (PMB-P.2, ADR-031/035/036): persistencia
/// aditiva de tipos, items, revisiones, usos, relaciones, imágenes y
/// permisos. La validación por esquema, herencia y no-filtración se
/// profundiza en PMB-P.6.
/// </summary>
public class ContentModelTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static async Task<(ContentType type, ContentItem item)> SeedItemAsync(AppDbContext db)
    {
        var type = await db.ContentTypes
            .FirstOrDefaultAsync(x => x.System == "dnd5e" && x.Key == "race");
        if (type == null)
        {
            type = new ContentType
            {
                Id = Guid.NewGuid(),
                System = "dnd5e",
                Key = "race",
                DisplayName = "Raza",
                SchemaJson = """{"fields":[{"key":"speed","type":"number"}]}""",
                CreatedAt = DateTime.UtcNow,
            };
            db.ContentTypes.Add(type);
        }
        var item = new ContentItem
        {
            Id = Guid.NewGuid(),
            System = "dnd5e",
            TypeId = type.Id,
            Name = "Humano",
            PropertiesJson = """{"speed":30}""",
            InheritanceJson = """{"speed":"inherit"}""",
            Scope = ContentScope.Campaign,
            CampaignId = Guid.NewGuid(),
            WorkflowState = ContentWorkflowState.Published,
            CurrentRevision = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ContentItems.Add(item);
        await db.SaveChangesAsync();
        return (type, item);
    }

    [Fact]
    public async Task ContentItem_PersisteConTipoAmbitoYPropiedades()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (type, created) = await SeedItemAsync(db);

        var loaded = await db.ContentItems.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);
        Assert.Equal("Humano", loaded.Name);
        Assert.Equal(ContentScope.Campaign, loaded.Scope);
        Assert.Equal(ContentWorkflowState.Published, loaded.WorkflowState);
        Assert.Equal(type.Id, loaded.TypeId);
        Assert.Contains("speed", loaded.PropertiesJson);
    }

    [Fact]
    public async Task ContentRevision_EsInmutablePorNumeroDeItem()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, item) = await SeedItemAsync(db);

        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Number = 1,
            PayloadJson = """{"name":"Humano"}""",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Segunda revisión con el mismo número: el índice único la rechaza.
        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Number = 1,
            PayloadJson = """{"name":"Humano (2)"}""",
            CreatedAt = DateTime.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ItemUsage_ConservaElHistorialAunqueElItemDesaparezca()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, item) = await SeedItemAsync(db);

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Campaña usos",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Campaigns.Add(campaign);
        var actor = new Actor
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Name = "Elenar",
            RulesetKey = "dnd5e",
            AttributesJson = "{}",
            ResourcesJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Actors.Add(actor);
        db.ItemUsages.Add(new ItemUsage
        {
            Id = Guid.NewGuid(),
            ActorId = actor.Id,
            ItemId = item.Id,
            RevisionNumber = 1,
            SnapshotJson = """{"speed":30}""",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // El item se borra (flujo no habitual); el uso del personaje se
        // conserva: su snapshot ya materializa las propiedades.
        db.ContentItems.Remove(item);
        await db.SaveChangesAsync();

        var usage = await db.ItemUsages.AsNoTracking()
            .SingleAsync(x => x.ActorId == actor.Id);
        Assert.Equal(item.Id, usage.ItemId);
        Assert.Contains("speed", usage.SnapshotJson);
    }

    [Fact]
    public async Task ContentGrant_Y_Relacion_TienenIdentidadUnica()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, item) = await SeedItemAsync(db);
        var (_, other) = await SeedItemAsync(db);

        db.ContentGrants.Add(new ContentGrant
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            SubjectKind = "role",
            SubjectId = "Player",
            Permission = "view",
        });
        db.ContentRelations.Add(new ContentRelation
        {
            Id = Guid.NewGuid(),
            FromItemId = item.Id,
            Kind = "requires",
            ToItemId = other.Id,
        });
        await db.SaveChangesAsync();

        db.ContentGrants.Add(new ContentGrant
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            SubjectKind = "role",
            SubjectId = "Player",
            Permission = "view",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

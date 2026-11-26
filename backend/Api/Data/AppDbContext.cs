using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Api.Data;

/// <summary>
/// Contexto EF Core del monolito. El modelo se mantiene portable entre
/// PostgreSQL (desarrollo/producción) y SQLite (pruebas de integración):
/// no se usan tipos específicos de un proveedor.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();
    public DbSet<CampaignInvite> CampaignInvites => Set<CampaignInvite>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<Token> Tokens => Set<Token>();
    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<ActorItem> ActorItems => Set<ActorItem>();
    public DbSet<ActorEffect> ActorEffects => Set<ActorEffect>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<SceneCombat> SceneCombats => Set<SceneCombat>();
    public DbSet<Combatant> Combatants => Set<Combatant>();
    public DbSet<FogCell> FogCells => Set<FogCell>();
    public DbSet<CampaignNote> CampaignNotes => Set<CampaignNote>();
    public DbSet<HistoryEntry> HistoryEntries => Set<HistoryEntry>();
    public DbSet<SceneMap> SceneMaps => Set<SceneMap>();
    public DbSet<SceneAnnotation> SceneAnnotations => Set<SceneAnnotation>();
    public DbSet<ProcessedCommand> ProcessedCommands => Set<ProcessedCommand>();
    public DbSet<ContentType> ContentTypes => Set<ContentType>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();
    public DbSet<ItemUsage> ItemUsages => Set<ItemUsage>();
    public DbSet<ContentRelation> ContentRelations => Set<ContentRelation>();
    public DbSet<ImageAsset> ImageAssets => Set<ImageAsset>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<WorldContentOverride> WorldContentOverrides => Set<WorldContentOverride>();
    public DbSet<ContentGrant> ContentGrants => Set<ContentGrant>();
    public DbSet<ContentPack> ContentPacks => Set<ContentPack>();
    public DbSet<CharacterBlueprint> CharacterBlueprints => Set<CharacterBlueprint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.NormalizedUserName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.NormalizedUserName).IsUnique();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("campaigns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<CampaignMember>(entity =>
        {
            entity.ToTable("campaign_members");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.JoinedAt).IsRequired();
            entity.HasIndex(x => new { x.CampaignId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CampaignInvite>(entity =>
        {
            entity.ToTable("campaign_invites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ExpiresAt).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.CampaignId);
            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.Invites)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Scene>(entity =>
        {
            entity.ToTable("scenes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.GridCellSize).IsRequired();
            // Forma del grid: "square" (por defecto) o "hex" (ADR-023).
            entity.Property(x => x.GridShape).HasMaxLength(10).IsRequired()
                .HasDefaultValue(GridShapes.Square);
            // Portada opcional: solo el tipo MIME; el blob vive en IAssetStore.
            entity.Property(x => x.CoverContentType).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.CampaignId);
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Token>(entity =>
        {
            entity.ToTable("tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            // Testigo de concurrencia optimista: si otro cambio se aplicó
            // entre la lectura y la escritura, SaveChanges falla.
            entity.Property(x => x.Version).IsConcurrencyToken().IsRequired();
            entity.HasIndex(x => x.SceneId);
            // Vinculación opcional token↔actor (PROJECT_SPEC §5): al borrar
            // el actor el token queda sin vincular (SetNull). La validez de
            // la vinculación al crear se comprueba en SceneService.
            entity.HasIndex(x => x.ActorId);
            entity.HasOne<Actor>()
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Scene)
                .WithMany(x => x.Tokens)
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Actor>(entity =>
        {
            entity.ToTable("actors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RulesetKey).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AttributesJson).IsRequired();
            entity.Property(x => x.ResourcesJson).IsRequired();
            // Testigo de concurrencia optimista (igual que Token).
            entity.Property(x => x.Version).IsConcurrencyToken().IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.CampaignId);
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessedCommand>(entity =>
        {
            entity.ToTable("processed_commands");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ResponseJson).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CampaignId);
        });

        modelBuilder.Entity<ActorItem>(entity =>
        {
            entity.ToTable("actor_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SlotId).HasMaxLength(50);
            entity.HasIndex(x => x.SlotId);
            // Testigo de concurrencia optimista (igual que Token y Actor).
            entity.Property(x => x.Version).IsConcurrencyToken().IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.ActorId);
            // Al borrar el actor desaparece su inventario.
            entity.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActorEffect>(entity =>
        {
            entity.ToTable("actor_effects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            // Una condición no se aplica dos veces al mismo actor.
            entity.HasIndex(x => new { x.ActorId, x.Name }).IsUnique();
            entity.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            // Historial de la campaña ordenado por fecha.
            entity.HasIndex(x => new { x.CampaignId, x.CreatedAt });
            // Al borrar la campaña o el usuario desaparecen sus mensajes.
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DirectMessage>(entity =>
        {
            entity.ToTable("direct_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            // Conversaciones por campaña y participantes, ordenadas por fecha.
            entity.HasIndex(x => new
            {
                x.CampaignId,
                x.FromUserId,
                x.ToUserId,
                x.CreatedAt,
            });
            // Al borrar la campaña o cualquiera de los dos usuarios.
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromUser)
                .WithMany()
                .HasForeignKey(x => x.FromUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ToUser)
                .WithMany()
                .HasForeignKey(x => x.ToUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SceneCombat>(entity =>
        {
            entity.ToTable("scene_combats");
            // Un único encuentro por escena (1:1).
            entity.HasKey(x => x.SceneId);
            entity.Property(x => x.Round).IsRequired();
            entity.Property(x => x.StartedAt).IsRequired();
            entity.HasOne(x => x.Scene)
                .WithMany()
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
            // Al borrar el combatiente actual el turno queda vacío.
            entity.HasOne(x => x.CurrentCombatant)
                .WithMany()
                .HasForeignKey(x => x.CurrentCombatantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Combatant>(entity =>
        {
            entity.ToTable("combatants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Initiative).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            // Orden de turno por escena (initiativa desc, luego alta).
            entity.HasIndex(x => new { x.SceneId, x.Initiative, x.CreatedAt });
            // Al borrar la escena desaparece su encuentro; al borrar el
            // token el combatiente sigue, sin vincular.
            entity.HasOne(x => x.Scene)
                .WithMany()
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Token)
                .WithMany()
                .HasForeignKey(x => x.TokenId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FogCell>(entity =>
        {
            // Celdas REVELADAS de la niebla: lo demás está oculto.
            entity.ToTable("scene_fog_cells");
            entity.HasKey(x => new { x.SceneId, x.X, x.Y });
            entity.Property(x => x.X).IsRequired();
            entity.Property(x => x.Y).IsRequired();
            // Al borrar la escena desaparece su niebla.
            entity.HasOne(x => x.Scene)
                .WithMany()
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CampaignNote>(entity =>
        {
            entity.ToTable("campaign_notes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(5000).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            // Notas de la campaña, de más reciente a más antigua.
            entity.HasIndex(x => new { x.CampaignId, x.CreatedAt });
            // Al borrar la campaña desaparecen sus notas.
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HistoryEntry>(entity =>
        {
            entity.ToTable("campaign_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            // Historial de la campaña, de más reciente a más antiguo.
            entity.HasIndex(x => new { x.CampaignId, x.CreatedAt });
            // Al borrar la campaña o el usuario, sus entradas.
            entity.HasOne(x => x.Campaign)
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SceneMap>(entity =>
        {
            // Un mapa por escena (ADR-028); el contenido vive en IAssetStore.
            entity.ToTable("scene_maps");
            entity.HasKey(x => x.SceneId);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SizeBytes).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasOne(x => x.Scene)
                .WithMany()
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SceneAnnotation>(entity =>
        {
            // Anotaciones compartidas de la escena (ADR-043).
            entity.ToTable("scene_annotations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            entity.Property(x => x.PointsJson).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            // Concurrencia optimista (ADR-043): dos escrituras no pueden
            // persistir la misma versión de una escena.
            entity.HasIndex(x => new { x.SceneId, x.Version }).IsUnique();
            entity.HasOne(x => x.Scene)
                .WithMany()
                .HasForeignKey(x => x.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Modelo de contenido (ADR-031/035/036, puente PMB-P.2) ---------
        // Todo aditivo: nada del MVP se toca.

        modelBuilder.Entity<ContentType>(entity =>
        {
            entity.ToTable("content_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.System).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SchemaJson).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => new { x.System, x.Key }).IsUnique();
        });

        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.ToTable("content_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.System).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.TagsJson).IsRequired();
            entity.Property(x => x.PropertiesJson).IsRequired();
            entity.Property(x => x.InheritanceJson).IsRequired();
            entity.Property(x => x.PrivateFieldsJson).IsRequired();
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.WorkflowState).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.TypeId);
            entity.HasIndex(x => x.ParentId);
            entity.HasIndex(x => new { x.Scope, x.CampaignId });
            entity.HasIndex(x => x.Name);
            // Identidad importada (ADR-036): única por (paquete, key).
            entity.HasIndex(x => new { x.PackKey, x.PackItemKey }).IsUnique();
            entity.HasOne(x => x.Type)
                .WithMany()
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentRevision>(entity =>
        {
            entity.ToTable("content_revisions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).IsRequired();
            // Una revisión por número e item (inmutables, §7).
            entity.HasIndex(x => new { x.ItemId, x.Number }).IsUnique();
            entity.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemUsage>(entity =>
        {
            entity.ToTable("item_usages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SnapshotJson).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.ActorId);
            entity.HasIndex(x => x.ItemId);
            entity.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Cascade);
            // ItemId sin navegación ni cascada: el historial del personaje se
            // conserva aunque el contenido cambie (§7).
        });

        modelBuilder.Entity<ContentRelation>(entity =>
        {
            entity.ToTable("content_relations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.HasIndex(x => x.FromItemId);
            entity.HasIndex(x => x.ToItemId);
            entity.HasIndex(x => new { x.FromItemId, x.Kind, x.ToItemId }).IsUnique();
            entity.HasOne(x => x.FromItem)
                .WithMany()
                .HasForeignKey(x => x.FromItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ToItem)
                .WithMany()
                .HasForeignKey(x => x.ToItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImageAsset>(entity =>
        {
            entity.ToTable("image_assets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(260).IsRequired();
            entity.Property(x => x.TagsJson).IsRequired();
            entity.Property(x => x.AlbumsJson).IsRequired();
            entity.HasIndex(x => x.Role);
            entity.Property(x => x.VariantsJson).IsRequired();
            entity.Property(x => x.PackKey).HasMaxLength(100);
            entity.Property(x => x.License).HasMaxLength(100);
            entity.Property(x => x.LicenseUrl).HasMaxLength(500);
            entity.Property(x => x.Origin).HasMaxLength(500);
            entity.Property(x => x.Author).HasMaxLength(200);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.OwnerUserId);
            entity.HasIndex(x => x.PackKey);
            // Identidad importada (ADR-036): única por (paquete, key).
            entity.HasIndex(x => new { x.PackKey, x.PackItemKey }).IsUnique();
        });

        modelBuilder.Entity<ContentPack>(entity =>
        {
            entity.ToTable("content_packs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(50).IsRequired();
            entity.Property(x => x.System).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SourceUrl).HasMaxLength(500);
            entity.Property(x => x.License).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LicenseUrl).HasMaxLength(500);
            entity.Property(x => x.AttributionJson).IsRequired();
            entity.Property(x => x.ImportedAt).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<CharacterBlueprint>(entity =>
        {
            entity.ToTable("character_blueprints");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.System).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StepsJson).IsRequired();
            entity.Property(x => x.EquipmentSlotsJson);
            entity.Property(x => x.DerivedStatsJson).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => new { x.System, x.Key }).IsUnique();
        });

        modelBuilder.Entity<World>(entity =>
        {
            entity.ToTable("worlds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.System).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.OwnerUserId);
        });

        modelBuilder.Entity<WorldContentOverride>(entity =>
        {
            entity.ToTable("world_content_overrides");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PatchJson).IsRequired();
            entity.HasIndex(x => new { x.WorldId, x.CampaignId, x.ContentItemId }).IsUnique();
        });

        modelBuilder.Entity<ContentGrant>(entity =>
        {
            entity.ToTable("content_grants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SubjectKind).HasMaxLength(16).IsRequired();
            entity.Property(x => x.SubjectId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Permission).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => x.ItemId);
            entity.HasIndex(x => new { x.ItemId, x.SubjectKind, x.SubjectId, x.Permission }).IsUnique();
            entity.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

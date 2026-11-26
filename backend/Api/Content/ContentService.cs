using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Contenido por HTTP (PMB-P.3, ADR-031): CRUD de tipos e items,
/// publicación con revisiones inmutables, usos con snapshot y validación
/// por esquema. La autoridad es el servidor: permisos por ámbito + grants,
/// secrecía del DM aplicada al serializar y reglas de herencia resueltas en
/// el uso. Los ámbitos `global` y `world` se reservan a la importación de
/// paquetes (PMB-P.4/P.7): la API solo crea contenido `user`, `campaign` y
/// `dm`.
/// </summary>
public sealed class ContentService(AppDbContext db)
{
    // --- Tipos ----------------------------------------------------------

    public async Task<List<ContentTypeDto>> ListTypesAsync(
        string? system, CancellationToken cancellationToken = default)
    {
        var query = db.ContentTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(system))
        {
            query = query.Where(x => x.System == system);
        }

        var types = await query.OrderBy(x => x.System).ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);
        return types.Select(ToDto).ToList();
    }

    public async Task<ContentTypeDto> CreateTypeAsync(
        CreateContentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var system = InputGuard.RequiredText(request.System, "system", maxLength: 50);
        var key = InputGuard.RequiredText(request.Key, "key", maxLength: 50);
        var displayName = InputGuard.RequiredText(request.DisplayName, "displayName", maxLength: 100);
        if (request.Schema is not JsonObject)
        {
            throw ApiException.BadRequest("El campo 'schema' debe ser un objeto JSON.");
        }

        if (await db.ContentTypes.AnyAsync(
            x => x.System == system && x.Key == key, cancellationToken))
        {
            throw ApiException.Conflict($"El tipo '{system}/{key}' ya existe.");
        }

        var type = new ContentType
        {
            Id = Guid.NewGuid(),
            System = system,
            Key = key,
            DisplayName = displayName,
            SchemaJson = request.Schema!.ToJsonString(),
            CreatedAt = DateTime.UtcNow,
        };
        db.ContentTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    // --- Items ----------------------------------------------------------

    public async Task<List<ContentItemDto>> ListItemsAsync(
        Guid userId, Guid? campaignId, string? system, string? typeKey,
        CancellationToken cancellationToken = default)
    {
        var query = db.ContentItems.AsNoTracking()
            .Where(x => x.WorkflowState != ContentWorkflowState.Draft
                || x.CreatedByUserId == userId);

        if (campaignId.HasValue)
        {
            // Disponible en la campaña: lo publicado en ella + el contenido
            // global (oficial/compartido, p. ej. el SRD). Lo personal del
            // usuario se filtra después por visibilidad (scope user).
            query = query.Where(x =>
                x.CampaignId == campaignId || x.Scope == ContentScope.Global);
        }
        if (!string.IsNullOrWhiteSpace(system))
        {
            query = query.Where(x => x.System == system);
        }
        if (!string.IsNullOrWhiteSpace(typeKey))
        {
            query = query.Where(x => x.Type.Key == typeKey);
        }

        // La clave del tipo ya se resuelve en el join de la query: se
        // projecciona para no consultar ContentTypes por cada item.
        var items = await query.OrderBy(x => x.Name)
            .Select(x => new { Item = x, TypeKey = x.Type.Key })
            .ToListAsync(cancellationToken);

        // Mismas reglas de CanView, datos cargados en lote: un rol por
        // campaña y los grants de todos los items de una sola vez.
        var roles = await RolesInCampaignsAsync(
            items.Select(x => x.Item.CampaignId), userId, cancellationToken);
        var grants = await GrantsForItemsAsync(
            items.Select(x => x.Item.Id), cancellationToken);

        var visible = new List<(ContentItem Item, CampaignRole? Role, string TypeKey)>();
        foreach (var entry in items)
        {
            CampaignRole? role = null;
            if (entry.Item.CampaignId is Guid campaign
                && roles.TryGetValue(campaign, out var found))
            {
                role = found;
            }
            var itemGrants = grants.GetValueOrDefault(entry.Item.Id) ?? [];
            if (CanView(entry.Item, userId, role, itemGrants))
            {
                visible.Add((entry.Item, role, entry.TypeKey));
            }
        }

        // PMB-4.5: un solo conteo agrupado para toda la lista (evita N+1).
        var usageCounts = await UsageCountsAsync(
            visible.Select(x => x.Item.Id), userId, cancellationToken);
        var result = new List<ContentItemDto>();
        foreach (var (item, role, key) in visible)
        {
            result.Add(await ToDtoAsync(
                item, role, userId, cancellationToken,
                usageCounts.GetValueOrDefault(item.Id), key));
        }

        return result;
    }

    public async Task<ContentItemDto> GetAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanView(item, userId, role, grants))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }
        return await ToDtoAsync(item, role, userId, cancellationToken);
    }

    public async Task<ContentItemDto> CreateAsync(
        Guid userId, CreateContentItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var typeId = request.TypeId ?? throw ApiException.BadRequest("Falta el campo 'typeId'.");
        var type = await db.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == typeId, cancellationToken)
            ?? throw ApiException.NotFound("Tipo de contenido no encontrado.");

        var name = InputGuard.RequiredText(request.Name, "name", maxLength: 200);
        var scope = ParseScope(request.Scope);
        var (ownerUserId, campaignId) = await ResolveScopeAsync(scope, request.CampaignId, userId, cancellationToken);

        var properties = request.Properties as JsonObject
            ?? throw ApiException.BadRequest("El campo 'properties' debe ser un objeto JSON.");
        ContentSchema.Validate(ParseNode(type.SchemaJson), properties);
        if (type.Key == "ability")
        {
            AbilitySchema.Validate(properties);
        }
        if (properties["choiceGroups"] is JsonArray choiceGroups)
        {
            ChoiceGroupSchema.Validate(choiceGroups);
        }
        var inheritance = NormalizeInheritance(request.Inheritance);

        var itemId = Guid.NewGuid();
        var parentId = request.ParentId;

        var now = DateTime.UtcNow;
        var item = new ContentItem
        {
            Id = itemId,
            System = type.System,
            TypeId = typeId,
            Name = name,
            Summary = Truncate(request.Summary, 500),
            Description = request.Description,
            ParentId = parentId,
            TagsJson = ToJsonArray(request.Tags),
            ImageId = request.ImageId,
            Focal = Truncate(request.Focal, 20),
            PropertiesJson = properties.ToJsonString(),
            InheritanceJson = ToJsonObject(inheritance),
            Scope = scope,
            OwnerUserId = ownerUserId,
            CampaignId = campaignId,
            IsPrivate = request.IsPrivate ?? false,
            PrivateFieldsJson = ToJsonArray(request.PrivateFields),
            WorkflowState = ContentWorkflowState.Draft,
            CurrentRevision = 0,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        if (parentId is Guid parent)
        {
            await ValidateParentAsync(item, parent, type, userId, cancellationToken);
        }

        db.ContentItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(
            item, await RoleInCampaignAsync(campaignId, userId, cancellationToken),
            userId, cancellationToken, usageCount: 0);
    }

    public async Task<ContentItemDto> UpdateAsync(
        Guid id, Guid userId, UpdateContentItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanEdit(item, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }

        var type = await db.ContentTypes.AsNoTracking()
            .FirstAsync(x => x.Id == item.TypeId, cancellationToken);

        if (request.Name is not null)
        {
            item.Name = InputGuard.RequiredText(request.Name, "name", maxLength: 200);
        }
        if (request.Summary is not null)
        {
            item.Summary = Truncate(request.Summary, 500);
        }
        if (request.Description is not null)
        {
            item.Description = request.Description;
        }
        if (request.Tags is not null)
        {
            item.TagsJson = ToJsonArray(request.Tags);
        }
        if (request.ImageId is not null)
        {
            item.ImageId = request.ImageId;
        }
        if (request.Focal is not null)
        {
            item.Focal = Truncate(request.Focal, 20);
        }
        if (request.IsPrivate.HasValue && request.IsPrivate.Value != item.IsPrivate)
        {
            item.IsPrivate = request.IsPrivate.Value;
            if (request.IsPrivate.Value)
            {
                // La audiencia del item se reduce: sus descendientes
                // directos no pueden quedar con una audiencia mayor.
                await EnsureDescendantsContainedAsync(item, cancellationToken);
            }
            else
            {
                // La audiencia del item crece: su cadena debe seguirla
                // conteniendo (p. ej. deja de ser privado bajo un padre dm).
                await EnsureChainContainedAsync(item, cancellationToken);
            }
        }
        if (request.PrivateFields is not null)
        {
            item.PrivateFieldsJson = ToJsonArray(request.PrivateFields);
        }
        if (request.Properties is not null)
        {
            var properties = request.Properties as JsonObject
                ?? throw ApiException.BadRequest("El campo 'properties' debe ser un objeto JSON.");
            ContentSchema.Validate(ParseNode(type.SchemaJson), properties);
            if (type.Key == "ability")
            {
                AbilitySchema.Validate(properties);
            }
            if (properties["choiceGroups"] is JsonArray choiceGroups)
            {
                ChoiceGroupSchema.Validate(choiceGroups);
            }
            item.PropertiesJson = properties.ToJsonString();
        }
        if (request.Inheritance is not null)
        {
            item.InheritanceJson = ToJsonObject(NormalizeInheritance(request.Inheritance));
        }
        if (request.ParentId is not null)
        {
            if (request.ParentId.Value != Guid.Empty)
            {
                await ValidateParentAsync(item, request.ParentId.Value, type,
                    userId, cancellationToken);
                item.ParentId = request.ParentId;
            }
            else
            {
                item.ParentId = null;
            }
        }

        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(item, role, userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanEdit(item, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }

        // Un padre con hijos no se borra (PMB-4.3): dejaría un parentId
        // colgante e invisible en el árbol. Primero se reorganiza.
        if (await db.ContentItems.AsNoTracking().AnyAsync(
            x => x.ParentId == id, cancellationToken))
        {
            throw ApiException.Conflict(
                "No se puede eliminar: tiene contenido hijo. " +
                "Elimina o reorganiza primero sus hijos.");
        }

        db.ContentItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContentItemDto> PublishAsync(
        Guid id, Guid userId, PublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanEdit(item, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }

        // Publicar amplía la audiencia del hijo (borrador → miembros de la
        // campaña): toda su cadena debe seguirla conteniendo.
        item.WorkflowState = ContentWorkflowState.Published;
        try
        {
            await EnsureChainContainedAsync(item, cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == 400)
        {
            throw ApiException.Conflict(
                "No se puede publicar: la audiencia del hijo superaría la de " +
                "su padre. Publica primero el padre o haz el hijo más restrictivo.");
        }

        var chain = await BuildChainAsync(item, cancellationToken);
        var payload = new JsonObject
        {
            ["name"] = item.Name,
            ["summary"] = item.Summary,
            ["description"] = item.Description,
            ["parentId"] = item.ParentId?.ToString(),
            ["tags"] = ParseNode(item.TagsJson),
            ["properties"] = ParseNode(item.PropertiesJson),
            ["inheritance"] = ParseNode(item.InheritanceJson),
            ["effective"] = ContentResolver.ResolveEffective(chain),
        };

        var number = item.CurrentRevision + 1;
        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Number = number,
            PayloadJson = payload.ToJsonString(),
            Message = Truncate(request.Message, 500),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        });
        item.CurrentRevision = number;
        item.WorkflowState = ContentWorkflowState.Published;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(item, role, userId, cancellationToken);
    }

    public async Task<List<ContentRevisionDto>> ListRevisionsAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanView(item, userId, role, grants))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }

        var revisions = await db.ContentRevisions.AsNoTracking()
            .Where(x => x.ItemId == id)
            .OrderByDescending(x => x.Number)
            .ToListAsync(cancellationToken);

        // Secreçía del DM (ADR-031 §6): el payload de una revisión contiene
        // las propiedades completas del item — hacia quien no es DM ni autor
        // se eliminan los campos privados (nunca se filtran por esta vía).
        var isDm = role is CampaignRole.GM or CampaignRole.Owner;
        var keepPrivate = item.CreatedByUserId == userId || isDm;
        var privateFields = ParseArray(item.PrivateFieldsJson);

        // ADR-031 §6: los campos privados de los ANCESTROS también son
        // secretos — el payload incluye 'effective' con toda la cadena.
        var strip = new List<string>();
        if (!isDm)
        {
            strip.AddRange(await AncestorPrivateFieldsAsync(item.Id, cancellationToken));
        }
        if (!keepPrivate)
        {
            strip.AddRange(privateFields);
        }
        return revisions
            .Select(r => new ContentRevisionDto(
                r.Id, r.Number,
                strip.Count == 0
                    ? ParseNode(r.PayloadJson)
                    : StripPrivate(ParseNode(r.PayloadJson), [.. strip]),
                r.Message,
                r.CreatedByUserId, r.CreatedAt))
            .ToList();
    }

    // --- Usos (ADR-031 §7) ---------------------------------------------

    public async Task<ItemUsageDto> CreateUsageAsync(
        Guid actorId, Guid userId, CreateItemUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await db.Actors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken)
            ?? throw ApiException.NotFound("Ficha no encontrada.");
        await RequireCampaignMemberAsync(actor.CampaignId, userId, cancellationToken);

        var itemId = request.ItemId ?? throw ApiException.BadRequest("Falta el campo 'itemId'.");
        var item = await db.ContentItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw ApiException.NotFound("Contenido no encontrado.");

        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanView(item, userId, role, grants))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }
        if (item.WorkflowState != ContentWorkflowState.Published || item.CurrentRevision < 1)
        {
            throw ApiException.Conflict(
                "Solo se puede usar contenido publicado (con al menos una revisión).");
        }

        // Snapshot: propiedades efectivas (herencia resuelta) del momento.
        var chain = await BuildChainAsync(item, cancellationToken);
        var usage = new ItemUsage
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            ItemId = item.Id,
            RevisionNumber = item.CurrentRevision,
            SnapshotJson = ContentResolver.ResolveEffective(chain).ToJsonString(),
            CreatedAt = DateTime.UtcNow,
        };

        // ADR-031 §6: la respuesta del uso es un DTO; hacia quien no es DM
        // ni autor se eliminan los campos privados de la hoja y de cualquier
        // ancestro de la cadena.
        var isDm = role is CampaignRole.GM or CampaignRole.Owner;
        var strip = new List<string>();
        if (!isDm)
        {
            strip.AddRange(await AncestorPrivateFieldsAsync(item.Id, cancellationToken));
            if (item.CreatedByUserId != userId)
            {
                strip.AddRange(ParseArray(item.PrivateFieldsJson));
            }
        }

        db.ItemUsages.Add(usage);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(usage, [.. strip]);
    }

    public async Task<List<ItemUsageDto>> ListUsagesAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await db.Actors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken)
            ?? throw ApiException.NotFound("Ficha no encontrada.");
        await RequireCampaignMemberAsync(actor.CampaignId, userId, cancellationToken);

        var usages = await db.ItemUsages.AsNoTracking()
            .Where(x => x.ActorId == actorId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // Secreçía del DM: el snapshot materializa propiedades efectivas
        // (posiblemente con campos privados del DM). Solo el DM de la
        // campaña (o el autor del contenido) ve esos valores; el resto
        // recibe el snapshot ya filtrado — también el dueño del personaje,
        // que nunca tuvo acceso a ellos.
        var role = await RoleInCampaignAsync(actor.CampaignId, userId, cancellationToken);
        var isDm = role is CampaignRole.GM or CampaignRole.Owner;
        var privateByItem = new Dictionary<Guid, string[]>();
        var ancestorByItem = new Dictionary<Guid, string[]>();
        var authorByItem = new Dictionary<Guid, Guid?>();
        foreach (var itemId in usages.Select(u => u.ItemId).Distinct())
        {
            var item = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
            privateByItem[itemId] = item is null ? [] : ParseArray(item.PrivateFieldsJson);
            // ADR-031 §6: el secreto de un ancestro también se filtra.
            ancestorByItem[itemId] = item is null || isDm
                ? []
                : await AncestorPrivateFieldsAsync(itemId, cancellationToken);
            authorByItem[itemId] = item?.CreatedByUserId;
        }

        return usages
            .Select(u =>
            {
                // Por uso: DM de la campaña o autor del contenido; el resto
                // recibe el snapshot filtrado (hoja + ancestros).
                var keepPrivate = isDm || authorByItem.GetValueOrDefault(u.ItemId) == userId;
                var strip = new List<string>(
                    ancestorByItem.GetValueOrDefault(u.ItemId) ?? []);
                if (!keepPrivate)
                {
                    strip.AddRange(privateByItem.GetValueOrDefault(u.ItemId) ?? []);
                }
                return ToDto(u, [.. strip]);
            })
            .ToList();
    }

    // --- Usos por item (PMB-4.5, aditivo aprobado 2026-11-12) ----------

    /// <summary>
    /// «Ver usos» de un item: resumen sin snapshot, solo de campañas donde
    /// el solicitante es miembro (misma regla que <see cref="UsageCountsAsync"/>).
    /// Quien no puede ver el item recibe 404, igual que el resto de rutas.
    /// </summary>
    public async Task<List<ItemUsageSummaryDto>> ListUsagesForItemAsync(
        Guid itemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanView(item, userId, role, grants))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }

        var rows = await (
            from usage in db.ItemUsages.AsNoTracking()
            join actor in db.Actors.AsNoTracking() on usage.ActorId equals actor.Id
            join member in db.CampaignMembers.AsNoTracking()
                on actor.CampaignId equals member.CampaignId
            where usage.ItemId == itemId && member.UserId == userId
            orderby usage.CreatedAt descending
            select new
            {
                actor.Name,
                actor.CampaignId,
                usage.RevisionNumber,
                usage.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var campaignNames = await db.Campaigns.AsNoTracking()
            .Where(x => rows.Select(r => r.CampaignId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return rows.Select(r => new ItemUsageSummaryDto(
            r.Name,
            campaignNames.GetValueOrDefault(r.CampaignId) ?? string.Empty,
            r.RevisionNumber,
            r.CreatedAt))
            .ToList();
    }

    // --- Relaciones tipadas (PMB-4.6, aditivo aprobado 2026-11-16) -------

    /// <summary>
    /// Relaciones visibles de un item (entrantes y salientes por defecto).
    /// El extremo que el solicitante no puede ver se omite en el servidor.
    /// </summary>
    public async Task<List<ContentRelationDto>> ListRelationsAsync(
        Guid itemId, Guid userId, string? direction = null,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanView(item, userId, role, grants))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }

        var dir = direction?.Trim().ToLowerInvariant();
        if (dir is not (null or "" or "in" or "out"))
        {
            throw ApiException.BadRequest("'direction' debe ser 'in' u 'out'.");
        }

        var relations = await db.ContentRelations.AsNoTracking()
            .Where(x => x.FromItemId == itemId || x.ToItemId == itemId)
            .OrderBy(x => x.Kind).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var otherIds = relations
            .Select(x => x.FromItemId == itemId ? x.ToItemId : x.FromItemId)
            .Distinct().ToList();
        var others = (await db.ContentItems.AsNoTracking()
            .Where(x => otherIds.Contains(x.Id))
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Id);

        var result = new List<ContentRelationDto>();
        foreach (var relation in relations)
        {
            if (dir == "in" && relation.ToItemId != itemId)
            {
                continue;
            }
            if (dir == "out" && relation.FromItemId != itemId)
            {
                continue;
            }

            var otherId = relation.FromItemId == itemId
                ? relation.ToItemId : relation.FromItemId;
            if (others.TryGetValue(otherId, out var other)
                && await IsVisibleAsync(other.Id, userId, cancellationToken))
            {
                result.Add(ToDto(relation));
            }
        }

        return result;
    }

    /// <summary>Crea una relación; el servidor valida kind, visibilidad y permisos.</summary>
    public async Task<ContentRelationDto> CreateRelationAsync(
        Guid fromItemId, Guid userId, CreateContentRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var from = await FindAsync(fromItemId, cancellationToken);
        var role = await RoleInCampaignAsync(from.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(from.Id, cancellationToken);
        if (!CanEdit(from, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }

        var toItemId = request.ToItemId
            ?? throw ApiException.BadRequest("Falta el campo 'toItemId'.");
        if (toItemId == fromItemId)
        {
            throw ApiException.BadRequest("Un elemento no puede relacionarse consigo mismo.");
        }
        if (!await IsVisibleAsync(toItemId, userId, cancellationToken))
        {
            throw ApiException.NotFound("Contenido no encontrado.");
        }

        var kind = InputGuard.RequiredText(request.Kind, "kind", maxLength: 50);
        var type = await db.ContentTypes.AsNoTracking()
            .FirstAsync(x => x.Id == from.TypeId, cancellationToken);
        var allowed = ParseNode(type.SchemaJson)?["allowedRelations"] as JsonArray;
        if (allowed is { Count: > 0 } && !allowed.Any(node =>
            node is JsonValue value
            && value.TryGetValue<string>(out var allowedKind)
            && allowedKind == kind))
        {
            throw ApiException.BadRequest(
                $"El tipo '{type.Key}' no permite relaciones '{kind}'.");
        }

        var label = string.IsNullOrWhiteSpace(request.Label)
            ? null : InputGuard.RequiredText(request.Label, "label", maxLength: 200);
        if (await db.ContentRelations.AnyAsync(
            x => x.FromItemId == fromItemId && x.Kind == kind && x.ToItemId == toItemId,
            cancellationToken))
        {
            throw ApiException.Conflict("Esa relación ya existe.");
        }

        var relation = new ContentRelation
        {
            Id = Guid.NewGuid(),
            FromItemId = fromItemId,
            Kind = kind,
            ToItemId = toItemId,
            Label = label,
        };
        db.ContentRelations.Add(relation);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(relation);
    }

    public async Task DeleteRelationAsync(
        Guid fromItemId, Guid relationId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var from = await FindAsync(fromItemId, cancellationToken);
        var role = await RoleInCampaignAsync(from.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(from.Id, cancellationToken);
        if (!CanEdit(from, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }

        var relation = await db.ContentRelations.FirstOrDefaultAsync(
            x => x.Id == relationId && x.FromItemId == fromItemId, cancellationToken)
            ?? throw ApiException.NotFound("Relación no encontrada.");
        db.ContentRelations.Remove(relation);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ContentRelationDto ToDto(ContentRelation relation) => new(
        relation.Id, relation.FromItemId, relation.Kind, relation.ToItemId,
        relation.Label,
        string.IsNullOrWhiteSpace(relation.MetadataJson)
            ? null : ParseNode(relation.MetadataJson));

    // --- Flujo del DM (PMB-4.8, aditivo aprobado 2026-11-16) ------------

    public async Task<ContentItemDto> ProposeAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        await RequireEditAsync(item, userId, cancellationToken);
        if (item.Scope is ContentScope.Global or ContentScope.World)
        {
            throw ApiException.BadRequest(
                "El contenido importado no se propone: crea una copia personal.");
        }
        if (item.WorkflowState != ContentWorkflowState.Draft)
        {
            throw ApiException.Conflict("Solo se pueden proponer borradores.");
        }
        item.WorkflowState = ContentWorkflowState.Proposed;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(
            item, await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken),
            userId, cancellationToken);
    }

    public async Task<List<ContentProposalDto>> ListProposalsAsync(
        Guid? campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = db.ContentItems.AsNoTracking()
            .Where(x => x.WorkflowState == ContentWorkflowState.Proposed);
        if (campaignId.HasValue)
        {
            query = query.Where(x => x.CampaignId == campaignId.Value);
        }
        var rows = await query.ToListAsync(cancellationToken);
        var authorIds = rows.Where(x => x.CreatedByUserId.HasValue)
            .Select(x => x.CreatedByUserId!.Value).Distinct().ToList();
        var authors = await db.Users.AsNoTracking()
            .Where(x => authorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.UserName, cancellationToken);

        var result = new List<ContentProposalDto>();
        foreach (var item in rows)
        {
            // Homebrew: solo el autor o el DM de la campaña revisa propuestas.
            var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
            if (item.CreatedByUserId != userId && role is not (CampaignRole.GM or CampaignRole.Owner))
            {
                continue;
            }
            if (!await IsVisibleAsync(item.Id, userId, cancellationToken))
            {
                continue;
            }
            var revision = await db.ContentRevisions.AsNoTracking()
                .Where(x => x.ItemId == item.Id)
                .OrderByDescending(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken);
            result.Add(new ContentProposalDto(
                item.Id, item.Name,
                authors.GetValueOrDefault(item.CreatedByUserId ?? Guid.Empty) ?? "—",
                "proposed", item.CurrentRevision,
                DiffFull(item, revision), item.UpdatedAt));
        }
        return result.OrderByDescending(x => x.UpdatedAt).ToList();
    }

    /// <summary>Diff completo del homebrew frente a su última revisión publicada.</summary>
    public async Task<ContentDiffDto> GetDiffAsync(
        Guid itemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        if (item.CreatedByUserId != userId && role is not (CampaignRole.GM or CampaignRole.Owner))
        {
            throw ApiException.Forbidden("Solo el autor o el DM pueden ver este diff.");
        }
        var revision = await db.ContentRevisions.AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .OrderByDescending(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);
        return new ContentDiffDto(itemId, revision?.Number ?? 0, DiffFull(item, revision));
    }

    public async Task<ContentItemDto> ApproveAsync(
        Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        await RequireDmAsync(item, userId, cancellationToken);
        if (item.WorkflowState != ContentWorkflowState.Proposed)
        {
            throw ApiException.Conflict("Solo se pueden aprobar propuestas pendientes.");
        }
        // Publicar reutiliza la creación de revisión y la contención de audiencia.
        return await PublishAsync(id, userId, new PublishContentRequest(null), cancellationToken);
    }

    public async Task<ContentItemDto> RejectAsync(
        Guid id, Guid userId, RejectContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(id, cancellationToken);
        await RequireDmAsync(item, userId, cancellationToken);
        if (item.WorkflowState != ContentWorkflowState.Proposed)
        {
            throw ApiException.Conflict("Solo se pueden rechazar propuestas pendientes.");
        }
        // El motivo se devuelve al autor como aviso; no se persiste (decisión PMB-4.8).
        _ = request.Reason;
        item.WorkflowState = ContentWorkflowState.Draft;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(
            item, await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken),
            userId, cancellationToken);
    }

    public async Task<List<ContentGrantDto>> ListGrantsAsync(
        Guid itemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        await RequireEditAsync(item, userId, cancellationToken);
        return await db.ContentGrants.AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .OrderBy(x => x.SubjectKind).ThenBy(x => x.SubjectId)
            .Select(x => new ContentGrantDto(
                x.Id, x.ItemId, x.SubjectKind, x.SubjectId, x.Permission))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentGrantDto> AddGrantAsync(
        Guid itemId, Guid userId, CreateContentGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        await RequireEditAsync(item, userId, cancellationToken);
        var kind = InputGuard.RequiredText(request.SubjectKind, "subjectKind", 16)
            .ToLowerInvariant();
        if (kind is not ("user" or "role"))
        {
            throw ApiException.BadRequest("'subjectKind' debe ser 'user' o 'role'.");
        }
        var subject = InputGuard.RequiredText(request.SubjectId, "subjectId", 64);
        var permission = InputGuard.RequiredText(request.Permission, "permission", 16)
            .ToLowerInvariant();
        if (permission is not ("view" or "edit"))
        {
            throw ApiException.BadRequest("'permission' debe ser 'view' o 'edit'.");
        }

        // Un grant no puede ensanchar la audiencia de la jerarquía: el sujeto
        // debe poder ver todos los ascendientes antes de concederle el permiso
        // (misma contención que al enlazar/publicar, PMB-4.3).
        Guid? subjectUserId = null;
        CampaignRole? subjectRole = null;
        if (kind == "user" && Guid.TryParse(subject, out var id))
        {
            subjectUserId = id;
            subjectRole = await RoleInCampaignAsync(item.CampaignId, id, cancellationToken);
        }
        else if (kind == "role")
        {
            if (!Enum.TryParse<CampaignRole>(subject, true, out var parsedRole)
                || !Enum.IsDefined(parsedRole))
            {
                throw ApiException.BadRequest(
                    "'subjectId' debe ser un rol válido para subjectKind='role'.");
            }
            subjectRole = parsedRole;
        }

        var currentId = item.ParentId;
        var depth = 1;
        while (currentId.HasValue && ++depth <= 64)
        {
            var ancestor = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentId, cancellationToken);
            if (ancestor is null)
            {
                break; // Un padre colgante se trata igual que en EnsureChainContainedAsync.
            }
            if (!CanView(ancestor, subjectUserId ?? Guid.Empty, subjectRole,
                    await GrantsForAsync(ancestor.Id, cancellationToken)))
            {
                throw ApiException.BadRequest(
                    "El permiso no puede concederse: el sujeto no puede ver un ascendiente del elemento.");
            }
            currentId = ancestor.ParentId;
        }

        if (await db.ContentGrants.AnyAsync(x => x.ItemId == itemId
            && x.SubjectKind == kind && x.SubjectId == subject && x.Permission == permission,
            cancellationToken))
        {
            throw ApiException.Conflict("Ese permiso ya existe.");
        }
        var grant = new ContentGrant
        {
            Id = Guid.NewGuid(), ItemId = itemId,
            SubjectKind = kind, SubjectId = subject, Permission = permission,
        };
        db.ContentGrants.Add(grant);
        await db.SaveChangesAsync(cancellationToken);
        return new ContentGrantDto(
            grant.Id, grant.ItemId, grant.SubjectKind, grant.SubjectId, grant.Permission);
    }

    public async Task DeleteGrantAsync(
        Guid itemId, Guid grantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(itemId, cancellationToken);
        await RequireEditAsync(item, userId, cancellationToken);
        var grant = await db.ContentGrants.FirstOrDefaultAsync(
            x => x.Id == grantId && x.ItemId == itemId, cancellationToken)
            ?? throw ApiException.NotFound("Permiso no encontrado.");
        db.ContentGrants.Remove(grant);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireEditAsync(
        ContentItem item, Guid userId, CancellationToken cancellationToken)
    {
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        if (!CanEdit(item, userId, role, grants))
        {
            throw ApiException.Forbidden();
        }
    }

    private async Task RequireDmAsync(
        ContentItem item, Guid userId, CancellationToken cancellationToken)
    {
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        if (role is CampaignRole.GM or CampaignRole.Owner)
        {
            return;
        }
        // Los items personales no tienen campaña ni DM: su dueño decide.
        if (item.Scope == ContentScope.User && item.OwnerUserId == userId)
        {
            return;
        }
        throw ApiException.Forbidden("Solo el DM de la campaña puede revisar propuestas.");
    }

    /// <summary>Diff legible entre el borrador y su última revisión publicada.</summary>
    private static string[] Diff(ContentItem item, ContentRevision? revision)
    {
        if (revision is null)
        {
            return ["contenido nuevo (sin revisión publicada)"];
        }
        var current = ParseNode(item.PropertiesJson) as JsonObject ?? [];
        var payload = ParseNode(revision.PayloadJson) as JsonObject ?? [];
        var previous = payload["properties"] as JsonObject ?? [];
        var changes = new List<string>();
        foreach (var (key, value) in current)
        {
            if (!previous.ContainsKey(key))
            {
                changes.Add($"+ {key}");
            }
            else if (previous[key]?.ToJsonString() != value?.ToJsonString())
            {
                changes.Add($"{key}: {Trim(previous[key])} → {Trim(value)}");
            }
        }
        foreach (var key in previous.Select(x => x.Key).Where(key => !current.ContainsKey(key)))
        {
            changes.Add($"- {key}");
        }
        return changes.Count == 0 ? ["sin cambios de propiedades"] : changes.ToArray();
    }

    private static string[] DiffFull(ContentItem item, ContentRevision? revision)
    {
        var changes = new List<string>(Diff(item, revision));
        if (revision is null)
        {
            return changes.ToArray();
        }
        var payload = ParseNode(revision.PayloadJson) as JsonObject ?? [];
        changes.AddRange(Compare("name", item.Name, payload["name"]?.GetValue<string>()));
        changes.AddRange(Compare("summary", item.Summary, payload["summary"]?.GetValue<string>()));
        changes.AddRange(Compare("description", item.Description,
            payload["description"]?.GetValue<string>()));
        var inheritance = ParseInheritance(item.InheritanceJson);
        var previousInheritance = payload["inheritance"] as JsonObject ?? [];
        foreach (var key in inheritance.Keys.Concat(previousInheritance.Select(x => x.Key)).Distinct())
        {
            var currentMode = inheritance.GetValueOrDefault(key);
            var previousMode = previousInheritance[key]?.GetValue<string>();
            if (currentMode != previousMode)
            {
                changes.Add($"inheritance.{key}: {previousMode ?? "—"} → {currentMode ?? "—"}");
            }
        }
        return changes.ToArray();
    }

    private static IEnumerable<string> Compare(string field, string? current, string? previous)
    {
        if (current == previous)
        {
            yield break;
        }
        yield return $"{field}: {previous ?? "—"} → {current ?? "—"}";
    }

    private static string Trim(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "—";
        return text.Length <= 60 ? text : text[..60] + "…";
    }

    /// <summary>
    /// Conteo de usos visibles para un item (regla PMB-4.5): solo de
    /// campañas donde el usuario es miembro.
    /// </summary>
    private async Task<int> CountVisibleUsagesAsync(
        Guid itemId, Guid userId, CancellationToken cancellationToken) =>
        (await UsageCountsAsync([itemId], userId, cancellationToken))
            .GetValueOrDefault(itemId);

    /// <summary>
    /// Conteos de usos visibles en una sola consulta para varios items:
    /// se une con actores y membresías para que cada uso solo cuente en las
    /// campañas donde el solicitante es miembro (evita N+1 en la lista).
    /// </summary>
    private async Task<Dictionary<Guid, int>> UsageCountsAsync(
        IEnumerable<Guid> itemIds, Guid userId, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await (
            from usage in db.ItemUsages.AsNoTracking()
            join actor in db.Actors.AsNoTracking() on usage.ActorId equals actor.Id
            join member in db.CampaignMembers.AsNoTracking()
                on actor.CampaignId equals member.CampaignId
            where ids.Contains(usage.ItemId) && member.UserId == userId
            group usage by usage.ItemId into g
            select new { ItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ItemId, x => x.Count, cancellationToken);
    }

    // --- Permisos (ADR-031 §6) -----------------------------------------

    /// <summary>¿Puede ver <paramref name="itemId"/> el usuario? (uso interno del creador).</summary>
    public async Task<bool> IsVisibleAsync(
        Guid itemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await db.ContentItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }
        var role = await RoleInCampaignAsync(item.CampaignId, userId, cancellationToken);
        var grants = await GrantsForAsync(item.Id, cancellationToken);
        return CanView(item, userId, role, grants);
    }

    /// <summary>Permisos explícitos del item (usuario/rol → view/edit).</summary>
    private async Task<List<ContentGrant>> GrantsForAsync(
        Guid itemId, CancellationToken cancellationToken) =>
        await db.ContentGrants.AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Igual que <see cref="GrantsForAsync"/> pero para varios items en una
    /// sola consulta. Los grants se entregan sin proyección ni filtro extra.
    /// </summary>
    private async Task<Dictionary<Guid, List<ContentGrant>>> GrantsForItemsAsync(
        IEnumerable<Guid> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var grants = await db.ContentGrants.AsNoTracking()
            .Where(x => ids.Contains(x.ItemId))
            .ToListAsync(cancellationToken);
        return grants.GroupBy(x => x.ItemId)
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private static bool HasGrant(
        IReadOnlyCollection<ContentGrant> grants, Guid userId, CampaignRole? role,
        string permission) =>
        grants.Any(g => g.Permission == permission
            && ((g.SubjectKind == "user" && Guid.TryParse(g.SubjectId, out var id) && id == userId)
                || (g.SubjectKind == "role"
                    && role?.ToString().Equals(
                        g.SubjectId, StringComparison.OrdinalIgnoreCase) == true)));

    private static bool CanView(
        ContentItem item, Guid userId, CampaignRole? role,
        IReadOnlyCollection<ContentGrant> grants)
    {
        if (item.CreatedByUserId == userId)
        {
            return true;
        }

        // Lo privado del DM y el ámbito dm: solo DM (o el autor).
        if (item.IsPrivate && role is not (CampaignRole.GM or CampaignRole.Owner))
        {
            return false;
        }

        var visible = item.Scope switch
        {
            ContentScope.User => item.OwnerUserId == userId,
            ContentScope.Dm => role is CampaignRole.GM or CampaignRole.Owner,
            ContentScope.Global => item.WorkflowState == ContentWorkflowState.Published,
            ContentScope.World => false, // ADR-032: uso futuro
            _ => role.HasValue
                && (item.WorkflowState == ContentWorkflowState.Published
                    || role is CampaignRole.GM or CampaignRole.Owner),
        };

        return visible || HasGrant(grants, userId, role, "view");
    }

    private static bool CanEdit(
        ContentItem item, Guid userId, CampaignRole? role,
        IReadOnlyCollection<ContentGrant> grants)
    {
        if (item.CreatedByUserId == userId)
        {
            return true;
        }

        var editable = item.Scope switch
        {
            ContentScope.User => item.OwnerUserId == userId,
            ContentScope.Dm => role is CampaignRole.GM or CampaignRole.Owner,
            ContentScope.Campaign => role is CampaignRole.GM or CampaignRole.Owner,
            _ => false, // global/world: solo la importación de paquetes (P.4/P.7)
        };

        return editable || HasGrant(grants, userId, role, "edit");
    }

    private async Task<CampaignRole?> RoleInCampaignAsync(
        Guid? campaignId, Guid userId, CancellationToken cancellationToken)
    {
        if (!campaignId.HasValue)
        {
            return null;
        }
        var member = await db.CampaignMembers.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CampaignId == campaignId && x.UserId == userId, cancellationToken);
        return member?.Role;
    }

    /// <summary>
    /// Mismo resultado que <see cref="RoleInCampaignAsync"/> para cada
    /// campaña, resuelto en una sola consulta. Las campañas sin membresía
    /// quedan ausentes del diccionario (equivalen a un rol `null`).
    /// </summary>
    private async Task<Dictionary<Guid, CampaignRole>> RolesInCampaignsAsync(
        IEnumerable<Guid?> campaignIds, Guid userId, CancellationToken cancellationToken)
    {
        var ids = campaignIds.Where(x => x.HasValue).Select(x => x!.Value)
            .Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var memberships = await db.CampaignMembers.AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId) && x.UserId == userId)
            .Select(x => new { x.CampaignId, x.Role })
            .ToListAsync(cancellationToken);
        var roles = new Dictionary<Guid, CampaignRole>();
        foreach (var membership in memberships)
        {
            roles.TryAdd(membership.CampaignId, membership.Role);
        }
        return roles;
    }

    private async Task RequireCampaignMemberAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.CampaignMembers.AsNoTracking().AnyAsync(
            x => x.CampaignId == campaignId && x.UserId == userId, cancellationToken))
        {
            throw ApiException.Forbidden("No eres miembro de la campaña.");
        }
    }

    // --- Utilidades -----------------------------------------------------

    private async Task<ContentItem> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ContentItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Contenido no encontrado.");

    private async Task ValidateParentAsync(
        ContentItem child, Guid parentId, ContentType type, Guid userId,
        CancellationToken cancellationToken)
    {
        var ancestors = new List<ContentItem>();
        await ContentResolver.EnsureNoCycleAsync(child.Id, parentId, async (id, ct) =>
        {
            var parent = await db.ContentItems.AsNoTracking()
                .Include(x => x.Type).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (parent is null || !CanView(parent, userId,
                await RoleInCampaignAsync(parent.CampaignId, userId, ct),
                await GrantsForAsync(id, ct)))
            {
                throw ApiException.NotFound("Elemento padre no encontrado.");
            }
            if (!ContentResolver.CanParent(type, parent.Type))
            {
                throw ApiException.BadRequest("El padre debe ser del mismo tipo o una categoría del mismo sistema.");
            }
            ancestors.Add(parent);
            return parent.ParentId;
        }, cancellationToken);

        // Contención de audiencia (PMB-4.3): la cadena nueva debe ser
        // visible para la audiencia del hijo y de todo su subárbol (el
        // hijo aún no está guardado al crear: se añade en memoria).
        var subtree = new List<ContentItem> { child };
        subtree.AddRange(await DescendantsOfAsync(child.Id, cancellationToken));
        foreach (var ancestor in ancestors)
        {
            foreach (var node in subtree)
            {
                await EnsurePairContainedAsync(node, ancestor, cancellationToken);
            }
        }
    }

    /// <summary>
    /// La audiencia de <paramref name="child"/> debe estar contenida en la
    /// de <paramref name="ancestor"/>; si no, el enlace o la publicación se
    /// rechazan (el ámbito y la secrecía no pueden filtrarse hacia abajo
    /// por la jerarquía).
    /// </summary>
    private async Task EnsurePairContainedAsync(
        ContentItem child, ContentItem ancestor, CancellationToken cancellationToken)
    {
        var contained = ContentResolver.AudienceContained(child, ancestor);
        if (contained == false)
        {
            throw ApiException.BadRequest(
                "El padre no es visible para toda la audiencia del hijo: " +
                "un hijo nunca puede ser menos restrictivo que su padre.");
        }
        if (contained is null)
        {
            // Clase 'owner': decide el dueño concreto del hijo.
            var owner = child.OwnerUserId ?? child.CreatedByUserId;
            if (owner is Guid ownerId
                && !CanView(ancestor, ownerId,
                    await RoleInCampaignAsync(ancestor.CampaignId, ownerId, cancellationToken),
                    await GrantsForAsync(ancestor.Id, cancellationToken)))
            {
                throw ApiException.BadRequest(
                    "El padre no es visible para el dueño del hijo: " +
                    "un hijo nunca puede ser menos restrictivo que su padre.");
            }
        }
    }

    /// <summary>Valida la cadena completa del item contra su audiencia actual.</summary>
    private async Task EnsureChainContainedAsync(
        ContentItem item, CancellationToken cancellationToken)
    {
        var currentId = item.ParentId;
        var depth = 1;
        while (currentId.HasValue && ++depth <= 64)
        {
            var ancestor = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentId, cancellationToken);
            if (ancestor is null)
            {
                break; // Un padre colgante lo tratan DELETE/edición aparte.
            }
            await EnsurePairContainedAsync(item, ancestor, cancellationToken);
            currentId = ancestor.ParentId;
        }
    }

    /// <summary>
    /// Valida cada descendiente contra el item (cuando el item se vuelve
    /// más restrictivo, p. ej. al marcarse privado).
    /// </summary>
    private async Task EnsureDescendantsContainedAsync(
        ContentItem item, CancellationToken cancellationToken)
    {
        foreach (var descendant in await DescendantsOfAsync(item.Id, cancellationToken))
        {
            await EnsurePairContainedAsync(descendant, item, cancellationToken);
        }
    }

    /// <summary>Descendientes del item (hasta 64 niveles, sin él).</summary>
    private async Task<List<ContentItem>> DescendantsOfAsync(
        Guid rootId, CancellationToken cancellationToken)
    {
        var result = new List<ContentItem>();
        var frontier = new List<Guid> { rootId };
        var depth = 0;
        while (frontier.Count > 0 && depth < 64)
        {
            depth++;
            var next = new List<Guid>();
            foreach (var id in frontier)
            {
                var childIds = await db.ContentItems.AsNoTracking()
                    .Where(x => x.ParentId == id)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                foreach (var childId in childIds)
                {
                    var node = await db.ContentItems.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == childId, cancellationToken);
                    if (node is not null)
                    {
                        result.Add(node);
                        next.Add(childId);
                    }
                }
            }
            frontier = next;
        }
        return result;
    }

    /// <summary>Cadena de herencia (raíz → hoja) del item.</summary>
    private async Task<List<(JsonObject Properties, Dictionary<string, string> Inheritance)>>
        BuildChainAsync(ContentItem item, CancellationToken cancellationToken)
    {
        var chain = new List<(JsonObject, Dictionary<string, string>)>();
        ContentItem? current = item;
        var depth = 0;
        while (current is not null)
        {
            if (++depth > 64)
            {
                throw ApiException.BadRequest("La jerarquía de contenido es demasiado profunda.");
            }
            chain.Insert(0, (
                ParseNode(current.PropertiesJson) as JsonObject ?? [],
                ParseInheritance(current.InheritanceJson)));
            current = current.ParentId.HasValue
                ? await db.ContentItems.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == current.ParentId, cancellationToken)
                : null;
        }
        return chain;
    }

    /// <summary>
    /// Campos privados del DM de toda la cadena de ancestros (la hoja no):
    /// el secreto de un padre también lo es de sus variantes al serializar.
    /// </summary>
    private async Task<string[]> AncestorPrivateFieldsAsync(
        Guid itemId, CancellationToken cancellationToken)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        var item = await db.ContentItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        var currentId = item?.ParentId;
        var depth = 1;
        while (currentId.HasValue && ++depth <= 64)
        {
            var ancestor = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentId, cancellationToken);
            if (ancestor is null)
            {
                break;
            }
            foreach (var field in ParseArray(ancestor.PrivateFieldsJson))
            {
                fields.Add(field);
            }
            currentId = ancestor.ParentId;
        }
        return [.. fields];
    }

    private async Task<(Guid? OwnerUserId, Guid? CampaignId)> ResolveScopeAsync(
        ContentScope scope, Guid? campaignId, Guid userId, CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case ContentScope.User:
                return (userId, null);
            case ContentScope.Campaign:
                if (!campaignId.HasValue)
                {
                    throw ApiException.BadRequest("El ámbito 'campaign' requiere 'campaignId'.");
                }
                await RequireCampaignMemberAsync(campaignId.Value, userId, cancellationToken);
                return (null, campaignId);
            case ContentScope.Dm:
                if (!campaignId.HasValue)
                {
                    throw ApiException.BadRequest("El ámbito 'dm' requiere 'campaignId'.");
                }
                var role = await RoleInCampaignAsync(campaignId, userId, cancellationToken);
                if (role is not (CampaignRole.GM or CampaignRole.Owner))
                {
                    throw ApiException.Forbidden("Solo el DM puede crear contenido con ámbito 'dm'.");
                }
                return (null, campaignId);
            default:
                throw ApiException.BadRequest(
                    "La API solo crea contenido con ámbito 'user', 'campaign' o 'dm'; " +
                    "los ámbitos 'global' y 'world' se reservan a la importación de paquetes (PMB-P.4/P.7).");
        }
    }

    private static ContentScope ParseScope(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "user" => ContentScope.User,
        "campaign" => ContentScope.Campaign,
        "dm" => ContentScope.Dm,
        "global" => ContentScope.Global,
        "world" => ContentScope.World,
        _ => throw ApiException.BadRequest("El campo 'scope' no es un ámbito válido."),
    };

    private static Dictionary<string, string> NormalizeInheritance(
        Dictionary<string, string>? inheritance)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, mode) in inheritance ?? [])
        {
            normalized[key] = mode switch
            {
                "inherit" or "override" or "extend" => mode,
                _ => throw ApiException.BadRequest(
                    $"El modo de herencia '{mode}' no es válido (inherit|override|extend)."),
            };
        }
        return normalized;
    }

    private static Dictionary<string, string> ParseInheritance(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ParseNode(json) is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                if (value is JsonValue v && v.TryGetValue<string>(out var mode))
                {
                    result[key] = mode;
                }
            }
        }
        return result;
    }

    private static JsonNode? ParseNode(string json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    private static string ToJsonArray(string[]? values) =>
        new JsonArray((values ?? []).Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()).ToJsonString();

    private static string ToJsonObject(Dictionary<string, string> values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
        {
            obj[key] = value;
        }
        return obj.ToJsonString();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, maxLength)];
    }

    // --- DTO ------------------------------------------------------------

    private ContentTypeDto ToDto(ContentType type) => new(
        type.Id, type.System, type.Key, type.DisplayName,
        ParseNode(type.SchemaJson), type.CreatedAt);

    private async Task<ContentItemDto> ToDtoAsync(
        ContentItem item, CampaignRole? viewerRole, Guid viewerUserId,
        CancellationToken cancellationToken, int? usageCount = null,
        string? typeKey = null)
    {
        var properties = ParseNode(item.PropertiesJson) as JsonObject ?? [];

        // Secreçía del DM (ADR-031 §6): se elimina al serializar hacia
        // quien no es DM ni autor. Nunca se filtra por otras vías. Las
        // claves privadas solo se exponen cuando se ven sus valores.
        var isDm = viewerRole is CampaignRole.GM or CampaignRole.Owner;
        var privateFields = ParseArray(item.PrivateFieldsJson);
        var secretsVisible = isDm || item.CreatedByUserId is null;
        if (!secretsVisible && privateFields.Length > 0)
        {
            foreach (var field in privateFields)
            {
                properties.Remove(field);
            }
        }

        // El listado ya trae la clave del tipo del join de su query; el
        // fallback cubre las rutas de un solo item.
        typeKey ??= (await db.ContentTypes.AsNoTracking()
            .Where(x => x.Id == item.TypeId)
            .Select(x => x.Key)
            .FirstOrDefaultAsync(cancellationToken)) ?? string.Empty;

        // PMB-4.5: si el llamante no trae el conteo (rutas de un solo item),
        // se calcula con la misma regla de visibilidad que el endpoint.
        var usages = usageCount
            ?? await CountVisibleUsagesAsync(item.Id, viewerUserId, cancellationToken);

        return new ContentItemDto(
            item.Id, item.System, item.TypeId, typeKey,
            item.Name, item.Summary, item.Description, item.ParentId,
            ParseArray(item.TagsJson), item.ImageId, item.Focal,
            properties, ParseInheritance(item.InheritanceJson),
            item.Scope.ToString().ToLowerInvariant(), item.CampaignId,
            item.WorkflowState.ToString().ToLowerInvariant(), item.CurrentRevision,
            item.CreatedByUserId, item.CreatedAt, item.UpdatedAt,
            item.PackKey, item.PackItemKey,
            item.IsPrivate, secretsVisible ? privateFields : [],
            usages);
    }

    private static ItemUsageDto ToDto(ItemUsage usage, string[]? privateFields = null)
    {
        var snapshot = ParseNode(usage.SnapshotJson) ?? new JsonObject();
        if (privateFields is { Length: > 0 })
        {
            snapshot = StripPrivate(snapshot, privateFields) ?? new JsonObject();
        }
        return new ItemUsageDto(
            usage.Id, usage.ActorId, usage.ItemId, usage.RevisionNumber,
            snapshot, usage.CreatedAt);
    }

    /// <summary>
    /// Elimina los campos privados del DM de un payload/snapshot al
    /// serializar (la secrecía se decide en el mapeo a DTO, ADR-031 §6).
    /// </summary>
    private static JsonNode? StripPrivate(JsonNode? node, string[] privateFields)
    {
        if (privateFields.Length == 0)
        {
            return node;
        }
        if (node is not JsonObject obj)
        {
            return node;
        }
        var clone = obj.DeepClone().AsObject();
        foreach (var field in privateFields)
        {
            clone.Remove(field);
        }
        // Los payloads de revisión llevan las propiedades anidadas.
        if (clone["properties"] is JsonObject properties)
        {
            foreach (var field in privateFields)
            {
                properties.Remove(field);
            }
        }
        if (clone["effective"] is JsonObject effective)
        {
            foreach (var field in privateFields)
            {
                effective.Remove(field);
            }
        }
        return clone;
    }

    private static string[] ParseArray(string json)
    {
        if (ParseNode(json) is not JsonArray array)
        {
            return [];
        }
        return array
            .Select(v => v?.GetValue<string>() ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToArray();
    }
}

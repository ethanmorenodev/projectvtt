using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Content;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Characters;

/// <summary>
/// Creación de personajes por datos (PMB-3.2, ADR-034): el blueprint
/// describe los pasos y este servicio los **valida y resuelve en servidor**
/// (el cliente solo guía). La previsualización devuelve por paso los
/// errores y las estadísticas derivadas del build parcial: modificadores,
/// velocidad, tamaño e idiomas resueltos con la herencia del contenido
/// (raza → variante).
/// </summary>
public sealed class CharacterBlueprintService(AppDbContext db, ContentService content)
{
    private static readonly string[] Abilities =
        ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"];

    /// <summary>Costes de compra de puntos 5e (8-15; máximo 27 puntos).</summary>
    private static readonly Dictionary<int, int> PointBuyCosts = new()
    {
        [8] = 0, [9] = 1, [10] = 2, [11] = 3,
        [12] = 4, [13] = 5, [14] = 7, [15] = 9,
    };

    // --- Blueprints -----------------------------------------------------

    public async Task<List<CharacterBlueprintDto>> ListAsync(
        string? system, CancellationToken cancellationToken = default)
    {
        var query = db.CharacterBlueprints.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(system))
        {
            query = query.Where(x => x.System == system);
        }
        var blueprints = await query.OrderBy(x => x.System).ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);
        return blueprints.Select(ToDto).ToList();
    }

    /// <summary>
    /// Registra o actualiza un blueprint por (system, key) — idempotente,
    /// para cargadores seed y futuros paquetes de sistemas.
    /// </summary>
    public async Task<CharacterBlueprintDto> EnsureAsync(
        string system, string key, string name, JsonNode steps,
        JsonNode derivedStats, JsonNode? startingEquipmentRules,
        JsonNode? equipmentSlots = null,
        CancellationToken cancellationToken = default)
    {
        var blueprint = await db.CharacterBlueprints
            .FirstOrDefaultAsync(
                x => x.System == system && x.Key == key, cancellationToken);
        if (blueprint is null)
        {
            blueprint = new CharacterBlueprint
            {
                Id = Guid.NewGuid(),
                System = system,
                Key = key,
                Name = name,
                StepsJson = steps.ToJsonString(),
                DerivedStatsJson = derivedStats.ToJsonString(),
                StartingEquipmentRulesJson = startingEquipmentRules?.ToJsonString(),
                EquipmentSlotsJson = equipmentSlots?.ToJsonString(),
                CreatedAt = DateTime.UtcNow,
            };
            db.CharacterBlueprints.Add(blueprint);
        }
        else
        {
            blueprint.Name = name;
            blueprint.StepsJson = steps.ToJsonString();
            blueprint.DerivedStatsJson = derivedStats.ToJsonString();
            blueprint.StartingEquipmentRulesJson = startingEquipmentRules?.ToJsonString();
            blueprint.EquipmentSlotsJson = equipmentSlots?.ToJsonString();
        }
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(blueprint);
    }

    // --- Huecos de equipamiento (ADR-041) -------------------------------

    public sealed record EquipmentSlotDef(
        string Id, string Label, string? Icon, string[] Accepts,
        bool Optional, bool Multiple);

    /// <summary>Huecos declarados por el blueprint del sistema.</summary>
    public async Task<List<EquipmentSlotDef>> GetEquipmentSlotsAsync(
        string system, CancellationToken cancellationToken = default)
    {
        var blueprint = await db.CharacterBlueprints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.System == system, cancellationToken)
            ?? throw ApiException.NotFound($"No existe el blueprint '{system}'.");
        if (string.IsNullOrWhiteSpace(blueprint.EquipmentSlotsJson)
            || JsonNode.Parse(blueprint.EquipmentSlotsJson) is not JsonArray slots)
        {
            return [];
        }
        return slots.OfType<JsonObject>()
            .Select(slot => new EquipmentSlotDef(
                slot["id"]?.GetValue<string>() ?? string.Empty,
                slot["label"]?.GetValue<string>() ?? string.Empty,
                slot["icon"]?.GetValue<string>(),
                (slot["accepts"] as JsonArray)?
                    .Select(value => value?.GetValue<string>() ?? string.Empty)
                    .Where(value => value.Length > 0).ToArray() ?? [],
                slot["optional"]?.GetValue<bool>() ?? true,
                slot["multiple"]?.GetValue<bool>() ?? false))
            .Where(slot => slot.Id.Length > 0)
            .ToList();
    }

    // --- Previsualización ----------------------------------------------

    public async Task<BuildPreviewResponse> PreviewAsync(
        Guid userId, BuildPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var system = InputGuard.RequiredText(request.System, "system", maxLength: 50);
        var key = string.IsNullOrWhiteSpace(request.BlueprintKey)
            ? "standard"
            : InputGuard.RequiredText(request.BlueprintKey, "blueprintKey", maxLength: 50);
        var blueprint = await db.CharacterBlueprints.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.System == system && x.Key == key, cancellationToken)
            ?? throw ApiException.NotFound(
                $"No existe el blueprint '{system}/{key}'.");

        var stepsNode = request.Steps as JsonObject
            ?? throw ApiException.BadRequest("El campo 'steps' debe ser un objeto JSON.");
        var stepDefs = ParseSteps(blueprint.StepsJson);

        var results = new List<BuildStepResult>();
        var context = new BuildContext();

        // Elecciones de bonificaciones pendientes (ADR-034): viajan en el
        // mismo borrador que el resto de pasos. Solo se aceptan habilidades
        // conocidas; las demás se ignoran sin invalidar el build.
        if (stepsNode["bonusChoices"] is JsonObject choices)
        {
            foreach (var (name, value) in choices)
            {
                if (string.IsNullOrWhiteSpace(name)
                    || value is not JsonValue choiceValue
                    || !choiceValue.TryGetValue<string>(out var chosenText))
                {
                    continue;
                }
                var chosen = Abilities.FirstOrDefault(
                    a => a.Equals(chosenText?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (chosen is not null)
                {
                    context.BonusChoices[name.Trim()] = chosen;
                }
            }
        }

        foreach (var step in stepDefs)
        {
            var errors = new List<string>();
            var value = stepsNode[step.Key] as JsonObject;
            await ValidateStepAsync(step, value, userId, request.CampaignId, context, errors, cancellationToken);
            var required = step.Optional != true && step.Skippable != true;
            if (value is null && required)
            {
                errors.Add($"Falta el paso '{step.Key}'.");
            }
            results.Add(new BuildStepResult(step.Key, errors.Count == 0, errors.ToArray()));
        }

        var derived = await ComputeDerivedAsync(context, cancellationToken);
        return new BuildPreviewResponse(
            results.All(r => r.Ok),
            results.ToArray(),
            derived);
    }

    // --- Validación por paso -------------------------------------------

    private async Task ValidateStepAsync(
        StepDef step, JsonObject? value, Guid userId, Guid? campaignId,
        BuildContext context, List<string> errors,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return;
        }

        switch (step.Kind)
        {
            case "identity":
                ValidateIdentity(step, value, context, errors);
                break;
            case "choice":
                await ValidateChoiceAsync(
                    step, value, userId, context, errors, cancellationToken);
                break;
            case "attributes":
                ValidateAttributes(value, context, errors);
                break;
            case "equipment":
            case "text":
            case "review":
                // Pasos opcionales del flujo: sin reglas por ahora.
                break;
            default:
                break;
        }
    }

    private static void ValidateIdentity(
        StepDef step, JsonObject value, BuildContext context, List<string> errors)
    {
        var name = value["name"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors.Add("El personaje necesita un nombre.");
        }
        else if (name.Length > 100)
        {
            errors.Add("El nombre no puede superar los 100 caracteres.");
        }
        context.Name = name;
    }

    private async Task ValidateChoiceAsync(
        StepDef step, JsonObject value, Guid userId,
        BuildContext context, List<string> errors,
        CancellationToken cancellationToken)
    {
        // Elecciones configurables (PMB-5.3): el contenido puede declarar un
        // ChoiceGroup con id = clave del paso. Si no lo declara, se mantiene
        // la regla heredada de la clase (CHARACTERS.md §1.2, paso 6).
        var legacyCount = step.Validation?.ContainsKey("countFromClass") == true
            || step.Validation?.ContainsKey("choiceGroupRef") == true;
        if (legacyCount)
        {
            if (!context.Choices.TryGetValue("class", out var sourceItem))
            {
                errors.Add("Elige antes una clase para repartir las habilidades.");
                return;
            }
            var sourceProperties = JsonNode.Parse(sourceItem.PropertiesJson) as JsonObject ?? [];
            var sourceGroup = ChoiceGroupSchema.Find(sourceProperties, step.Key);
            if (sourceGroup is not null)
            {
                context.GroupChoices[step.Key] = ChoiceGroupSchema.ValidateSelection(
                    sourceGroup, value["chosen"] as JsonArray, errors);
                return;
            }
            // Contenido sin ChoiceGroups: regla heredada de `skillChoices`.
            var chosen = (value["chosen"] as JsonArray)?
                .Select(v => v?.GetValue<string>() ?? string.Empty)
                .Where(v => v.Length > 0)
                .ToList() ?? [];
            if (chosen.Count != chosen.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                errors.Add("Hay habilidades repetidas en la elección.");
            }
            var skillChoices = (sourceProperties["skillChoices"])?.GetValue<int>() ?? 0;
            if (chosen.Count != skillChoices)
            {
                errors.Add(
                    $"Debes elegir {skillChoices} habilidades (has elegido {chosen.Count}).");
            }
            return;
        }

        if (step.Validation?.ContainsKey("exactlyOne") == true
            && value["itemId"] is not JsonValue)
        {
            errors.Add($"Debes elegir exactamente una opción ({step.ContentType}).");
            return;
        }

        if (value["itemId"] is not JsonValue idValue
            || !idValue.TryGetValue<string>(out var idText)
            || !Guid.TryParse(idText, out var itemId))
        {
            return;
        }

        var item = await db.ContentItems.AsNoTracking()
            .Include(x => x.Type)
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null || step.ContentType is not null && item.Type.Key != step.ContentType)
        {
            errors.Add("La opción elegida no es de un tipo válido para este paso.");
            return;
        }
        if (!await content.IsVisibleAsync(itemId, userId, cancellationToken))
        {
            errors.Add("La opción elegida no está disponible para ti.");
            return;
        }

        // ChoiceGroup del contenido (PMB-5.3): gana sobre la regla heredada.
        var properties = JsonNode.Parse(item.PropertiesJson) as JsonObject ?? [];
        var group = ChoiceGroupSchema.Find(properties, step.Key);
        if (group is not null)
        {
            context.GroupChoices[step.Key] = ChoiceGroupSchema.ValidateSelection(
                group, value["chosen"] as JsonArray, errors);
            return;
        }
        if (legacyCount)
        {
            if (!context.Choices.TryGetValue("class", out var classItem))
            {
                errors.Add("Elige antes una clase para repartir las habilidades.");
                return;
            }
            var skillChoices = (JsonNode.Parse(classItem.PropertiesJson)?["skillChoices"])
                ?.GetValue<int>() ?? 0;
            var chosen = (value["chosen"] as JsonArray)?
                .Select(v => v?.GetValue<string>() ?? string.Empty)
                .Where(v => v.Length > 0).ToList() ?? [];
            if (chosen.Count != skillChoices)
            {
                errors.Add(
                    $"Debes elegir {skillChoices} habilidades (has elegido {chosen.Count}).");
            }
            return;
        }

        // Variante opcional (p. ej. subraza): debe colgar del item elegido.
        if (value["variantItemId"] is JsonValue variantValue
            && variantValue.TryGetValue<string>(out var variantText)
            && Guid.TryParse(variantText, out var variantId))
        {
            var variant = await db.ContentItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
            if (variant is null || variant.ParentId != itemId)
            {
                errors.Add("La variante elegida no pertenece a la opción seleccionada.");
                return;
            }
            if (!await content.IsVisibleAsync(variantId, userId, cancellationToken))
            {
                errors.Add("La variante elegida no está disponible para ti.");
                return;
            }
            context.Variant = variant;
        }

        context.Choices[step.Key] = item;
    }

    private static void ValidateAttributes(
        JsonObject value, BuildContext context, List<string> errors)
    {
        var method = value["method"]?.GetValue<string>() ?? "manual";
        var values = value["values"] as JsonObject;
        if (values is null)
        {
            errors.Add("Faltan los valores de atributos.");
            return;
        }

        var scores = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ability in Abilities)
        {
            if (values[ability] is not JsonValue node
                || !node.TryGetValue<int>(out var score))
            {
                errors.Add($"Falta el atributo '{ability}'.");
                continue;
            }
            scores[ability] = score;
        }
        if (scores.Count != Abilities.Length)
        {
            return;
        }

        switch (method)
        {
            case "manual":
                foreach (var (ability, score) in scores)
                {
                    if (score is < 3 or > 20)
                    {
                        errors.Add($"'{ability}' debe estar entre 3 y 20.");
                    }
                }
                break;
            case "pointBuy":
                var total = 0;
                foreach (var (ability, score) in scores)
                {
                    if (!PointBuyCosts.TryGetValue(score, out var cost))
                    {
                        errors.Add($"'{ability}' debe estar entre 8 y 15 en compra de puntos.");
                        continue;
                    }
                    total += cost;
                }
                if (total > 27)
                {
                    errors.Add($"Compra de puntos supera el presupuesto (27): {total}.");
                }
                break;
            default:
                errors.Add($"Método de atributos no válido: '{method}'.");
                break;
        }

        context.Abilities = scores;
    }

    // --- Estadísticas derivadas ----------------------------------------

    private async Task<JsonNode> ComputeDerivedAsync(
        BuildContext context, CancellationToken cancellationToken)
    {
        var abilities = new JsonObject();
        foreach (var ability in Abilities)
        {
            abilities[ability] = new JsonObject
            {
                ["score"] = context.Abilities.GetValueOrDefault(ability),
                ["modifier"] = Modifier(context.Abilities.GetValueOrDefault(ability)),
            };
        }

        var effective = new JsonObject();
        if (context.Choices.TryGetValue("race", out var race))
        {
            var chainItems = new List<ContentItem> { race };
            if (context.Variant is not null)
            {
                chainItems.Add(context.Variant);
            }
            else if (race.ParentId is not null)
            {
                // Una subraza elegida directamente: incluir su cadena.
                var parent = await db.ContentItems.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == race.ParentId, cancellationToken);
                if (parent is not null)
                {
                    chainItems.Insert(0, parent);
                }
            }

            var chain = chainItems
                .Select(i => (
                    JsonNode.Parse(i.PropertiesJson) as JsonObject ?? new JsonObject(),
                    ParseInheritance(i.InheritanceJson)))
                .ToList();
            effective = ContentResolver.ResolveEffective(chain);

            // Bonificaciones raciales → puntuaciones totales.
            if (effective["abilityBonuses"] is JsonArray bonuses)
            {
                foreach (var bonus in bonuses)
                {
                    var abilityName = bonus?["ability"]?.GetValue<string>()?.Trim();
                    var amount = bonus?["bonus"]?.GetValue<int>() ?? 0;
                    var mapped = Abilities.FirstOrDefault(
                        a => a.Equals(abilityName, StringComparison.OrdinalIgnoreCase));
                    if (mapped is not null)
                    {
                        var total = context.Abilities.GetValueOrDefault(mapped) + amount;
                        abilities[mapped] = new JsonObject
                        {
                            ["score"] = total,
                            ["modifier"] = Modifier(total),
                        };
                        context.PendingBonuses.Add(new JsonObject
                        {
                            ["ability"] = mapped,
                            ["bonus"] = amount,
                            ["applied"] = true,
                        });
                    }
                    else if (abilityName is not null)
                    {
                        // Bonificación elegible (p. ej. semielfo): ofrece las
                        // habilidades conocidas y solo se aplica si el usuario
                        // ha elegido una de ellas — nunca en silencio.
                        var pending = new JsonObject
                        {
                            ["ability"] = abilityName,
                            ["bonus"] = amount,
                            ["applied"] = false,
                            ["options"] = new JsonArray(
                                Abilities.Select(a => (JsonNode)a).ToArray()),
                        };
                        if (context.BonusChoices.TryGetValue(abilityName, out var chosen))
                        {
                            var total = context.Abilities.GetValueOrDefault(chosen) + amount;
                            abilities[chosen] = new JsonObject
                            {
                                ["score"] = total,
                                ["modifier"] = Modifier(total),
                            };
                            pending["ability"] = chosen;
                            pending["applied"] = true;
                        }
                        context.PendingBonuses.Add(pending);
                    }
                }
            }
        }

        return new JsonObject
        {
            ["level"] = 1,
            ["abilities"] = abilities,
            ["speed"] = effective["speed"]?.GetValue<double>() ?? 30,
            ["size"] = effective["size"]?.GetValue<string>() ?? "Medium",
            ["languages"] = effective["languages"]?.DeepClone() ?? new JsonArray(),
            ["pendingBonuses"] = new JsonArray(
                context.PendingBonuses.Select(b => b.DeepClone()).ToArray()),
        };
    }

    private static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    // --- Utilidades -----------------------------------------------------

    private sealed class BuildContext
    {
        public string? Name { get; set; }
        public Dictionary<string, ContentItem> Choices { get; } = [];
        public Dictionary<string, string[]> GroupChoices { get; } = [];
        public ContentItem? Variant { get; set; }
        public Dictionary<string, int> Abilities { get; set; } = [];
        /// <summary>Bonificación pendiente (nombre del contenido) → habilidad elegida.</summary>
        public Dictionary<string, string> BonusChoices { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<JsonObject> PendingBonuses { get; } = [];
    }

    private sealed record StepDef(
        string Key,
        string Kind,
        string? ContentType,
        bool? Optional,
        bool? Skippable,
        JsonObject? Validation);

    private static List<StepDef> ParseSteps(string json)
    {
        var steps = new List<StepDef>();
        if (JsonNode.Parse(json) is not JsonArray array)
        {
            return steps;
        }
        foreach (var entry in array)
        {
            if (entry is not JsonObject obj)
            {
                continue;
            }
            var key = obj["key"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            steps.Add(new StepDef(
                key,
                obj["kind"]?.GetValue<string>() ?? "text",
                obj["contentType"]?.GetValue<string>(),
                obj["optional"]?.GetValue<bool>(),
                obj["skippable"]?.GetValue<bool>(),
                obj["validation"] as JsonObject));
        }
        return steps;
    }

    private static Dictionary<string, string> ParseInheritance(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (JsonNode.Parse(json) is JsonObject obj)
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

    private static CharacterBlueprintDto ToDto(CharacterBlueprint blueprint) => new(
        blueprint.Id, blueprint.System, blueprint.Key, blueprint.Name,
        JsonNode.Parse(blueprint.StepsJson) ?? new JsonObject(),
        JsonNode.Parse(blueprint.DerivedStatsJson) ?? new JsonObject(),
        blueprint.StartingEquipmentRulesJson is null
            ? null
            : JsonNode.Parse(blueprint.StartingEquipmentRulesJson));
}

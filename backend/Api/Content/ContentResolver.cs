using System.Text.Json.Nodes;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Content;

/// <summary>
/// Resolución de herencia (ADR-031 §5): las propiedades efectivas de un
/// item se calculan de la raíz a la hoja aplicando el modo por campo
/// (`inherit` usa el padre, `override` sustituye, `extend` acumula listas y
/// números). El resultado se materializa en el snapshot de
/// <c>ItemUsage</c> cuando un personaje usa el contenido.
/// </summary>
public static class ContentResolver
{
    /// <summary>
    /// Propiedades efectivas de <paramref name="chain"/> (raíz primero).
    /// </summary>
    public static JsonObject ResolveEffective(
        IEnumerable<(JsonObject Properties, Dictionary<string, string> Inheritance)> chain)
    {
        var effective = new JsonObject();

        foreach (var (properties, inheritance) in chain)
        {
            foreach (var (key, value) in properties)
            {
                var mode = inheritance.TryGetValue(key, out var raw)
                    ? ParseMode(raw)
                    : InheritanceMode.Inherit;

                switch (mode)
                {
                    case InheritanceMode.Override:
                        effective[key] = value?.DeepClone();
                        break;
                    case InheritanceMode.Extend:
                        effective[key] = Extend(effective[key], value);
                        break;
                    default: // Inherit: el padre manda si tiene el campo
                        if (!effective.ContainsKey(key))
                        {
                            effective[key] = value?.DeepClone();
                        }
                        break;
                }
            }
        }

        return effective;
    }

    /// <summary>
    /// Comprueba que <paramref name="parentId"/> no introduce un ciclo al
    /// colgar de él <paramref name="itemId"/> (§5: ciclos prohibidos).
    /// </summary>
    public static async Task EnsureNoCycleAsync(
        Guid itemId, Guid? parentId,
        Func<Guid, CancellationToken, Task<Guid?>> parentOf,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { itemId };
        var current = parentId;
        var depth = 1; // El limite incluye al hijo, como BuildChainAsync.
        while (current.HasValue)
        {
            if (!visited.Add(current.Value))
            {
                throw ApiException.BadRequest("La jerarquía de contenido no puede formar ciclos.");
            }
            if (++depth > 64)
            {
                throw ApiException.BadRequest("La jerarquía de contenido es demasiado profunda.");
            }
            current = await parentOf(current.Value, cancellationToken);
        }
    }

    public static bool CanParent(ContentType childType, ContentType parentType) =>
        parentType.System == childType.System
        && (parentType.Id == childType.Id || parentType.Key == "category");

    // --- Contención de audiencia (PMB-4.3) ------------------------------
    // Los valores efectivos de un hijo se materializan para la audiencia
    // del hijo, así que toda su cadena debe ser visible para esa misma
    // audiencia: un hijo nunca puede ser menos restrictivo que su padre.

    /// <summary>
    /// Clase de audiencia del item (de mayor a menor):
    /// <c>everyone</c> (global publicado) ⊃ <c>members</c> (campaña
    /// publicada y no privada) ⊃ <c>gms</c> (dm, o campaña en borrador o
    /// privada) ⊃ <c>owner</c> (personal).
    /// </summary>
    public static string AudienceClass(ContentItem item) => item.Scope switch
    {
        ContentScope.Global => "everyone",
        ContentScope.User => "owner",
        ContentScope.Dm => "gms",
        _ => item.WorkflowState == ContentWorkflowState.Published && !item.IsPrivate
            ? "members"
            : "gms",
    };

    /// <summary>
    /// ¿La audiencia del hijo está contenida en la del ancestro?
    /// Devuelve <c>null</c> cuando la clase es <c>owner</c>: eso exige
    /// <c>CanView</c> concreto del dueño sobre el ancestro (lo resuelve
    /// el servicio). Las demás clases son estructurales.
    /// </summary>
    public static bool? AudienceContained(ContentItem child, ContentItem ancestor)
    {
        var ancestorPublished = ancestor.WorkflowState == ContentWorkflowState.Published;
        var sameCampaign = ancestor.CampaignId == child.CampaignId;
        return AudienceClass(child) switch
        {
            "everyone" => ancestor.Scope == ContentScope.Global && ancestorPublished,
            "members" => ancestor.Scope switch
            {
                ContentScope.Campaign => sameCampaign && ancestorPublished && !ancestor.IsPrivate,
                ContentScope.Global => ancestorPublished,
                _ => false,
            },
            "gms" => ancestor.Scope switch
            {
                // Los GM de la campaña ven también borradores y privados.
                ContentScope.Campaign or ContentScope.Dm => sameCampaign,
                ContentScope.Global => ancestorPublished,
                _ => false,
            },
            _ => null,
        };
    }

    private static InheritanceMode ParseMode(string raw) => raw switch
    {
        "override" => InheritanceMode.Override,
        "extend" => InheritanceMode.Extend,
        _ => InheritanceMode.Inherit,
    };

    private static JsonNode? Extend(JsonNode? existing, JsonNode? incoming)
    {
        if (existing is JsonArray existingArray && incoming is JsonArray incomingArray)
        {
            var merged = new JsonArray();
            foreach (var item in existingArray)
            {
                merged.Add(item?.DeepClone());
            }
            foreach (var item in incomingArray)
            {
                merged.Add(item?.DeepClone());
            }
            return merged;
        }

        if (existing is JsonValue a && a.TryGetValue<double>(out var left)
            && incoming is JsonValue b && b.TryGetValue<double>(out var right))
        {
            return JsonValue.Create(left + right);
        }

        return incoming?.DeepClone();
    }
}

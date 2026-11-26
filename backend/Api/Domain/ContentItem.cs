namespace ProjectVTT.Api.Domain;

/// <summary>
/// Instancia del modelo de contenido (ADR-031 §4): vive en un árbol por
/// tipo (categorías y variantes), con propiedades tipadas validadas contra
/// el <see cref="ContentType"/> y herencia por campo hacia sus padres. La
/// secrecía del DM se marca aquí y se aplica al serializar en servidor.
/// </summary>
public class ContentItem
{
    public Guid Id { get; set; }
    public required string System { get; set; }
    public Guid TypeId { get; set; }
    public ContentType Type { get; set; } = null!;
    public required string Name { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }

    /// <summary>Jerarquía: null = raíz del tipo (§4).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Etiquetas JSON (string[]).</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>Referencia a <see cref="ImageAsset"/> (sin duplicar archivos).</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Punto focal para el recorte de miniaturas (p. ej. "50,30").</summary>
    public string? Focal { get; set; }

    /// <summary>Propiedades JSON ({ fieldKey: valor }), validadas por TypeSchema.</summary>
    public required string PropertiesJson { get; set; }

    /// <summary>Herencia por campo JSON ({ fieldKey: inherit|override|extend }).</summary>
    public required string InheritanceJson { get; set; }

    public ContentScope Scope { get; set; }

    /// <summary>Dueño cuando el ámbito es <see cref="ContentScope.User"/>.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>Campaña cuando el ámbito es Campaign o Dm.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Mundo cuando el ámbito es World (ADR-032, uso futuro).</summary>
    public Guid? WorldId { get; set; }

    /// <summary>
    /// Procedencia de una copia independizada de un mundo (ADR-042): item
    /// original y revisión de la que se copió.
    /// </summary>
    public Guid? SourceWorldItemId { get; set; }
    public long? SourceRevision { get; set; }

    /// <summary>
    /// Paquete de origen opcional (ADR-036): la identidad importada es
    /// <c>(PackKey, PackItemKey)</c> y la procedencia se hereda del paquete.
    /// </summary>
    public string? PackKey { get; set; }

    /// <summary>Clave del item dentro de su paquete (importación idempotente).</summary>
    public string? PackItemKey { get; set; }

    /// <summary>Item privado del DM: no se serializa nunca hacia jugadores.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Campos privados del DM JSON (string[]): se eliminan al serializar.</summary>
    public string PrivateFieldsJson { get; set; } = "[]";

    public ContentWorkflowState WorkflowState { get; set; }

    /// <summary>
    /// Número de la última revisión publicada (0 = aún sin publicar). Las
    /// revisiones son inmutables (<see cref="ContentRevision"/>).
    /// </summary>
    public long CurrentRevision { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

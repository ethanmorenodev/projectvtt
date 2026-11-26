namespace ProjectVTT.Api.Domain;

/// <summary>
/// Paquete de contenido importado (ADR-036): identidad, versión y
/// procedencia. Todo item e imagen pertenece opcionalmente a un paquete y
/// hereda su licencia y atribución. La reimportación es idempotente por
/// <c>(paquete, key)</c> con resolución explícita de conflictos (nunca
/// silenciosa).
/// </summary>
public class ContentPack
{
    public Guid Id { get; set; }

    /// <summary>Clave única del paquete (p. ej. "dnd5e-srd-5.1").</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string System { get; set; }
    public string? SourceUrl { get; set; }

    /// <summary>Licencia del contenido (p. ej. "CC BY 4.0").</summary>
    public required string License { get; set; }

    public string? LicenseUrl { get; set; }

    /// <summary>Atribuciones JSON (string[]) exigidas por la licencia.</summary>
    public string AttributionJson { get; set; } = "[]";

    public DateTime ImportedAt { get; set; }
}

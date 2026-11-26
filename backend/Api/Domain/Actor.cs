namespace ProjectVTT.Api.Domain;

/// <summary>
/// Actor genérico de una campaña (personaje, PNJ, monstruo…). Los
/// atributos y recursos se guardan como JSON validado por el Ruleset de la
/// campaña (`ARCHITECTURE.md` §8): el Core no interpreta su significado.
/// <see cref="Version"/> es el testigo de concurrencia optimista, igual que
/// en <see cref="Token"/>.
/// </summary>
public class Actor
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Name { get; set; }

    /// <summary>Clave del Ruleset que valida estos datos (p. ej. "dnd5e").</summary>
    public required string RulesetKey { get; set; }

    /// <summary>Objeto JSON de atributos (enteros), validado por el Ruleset.</summary>
    public required string AttributesJson { get; set; }

    /// <summary>Objeto JSON de recursos (actual/máximo), validado por el Ruleset.</summary>
    public required string ResourcesJson { get; set; }

    public long Version { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // --- Ampliación de personaje (PMB-3.3, ADR-034; todo aditivo) -------

    /// <summary>Clase del actor: "pc" | "npc" | "creature" (por defecto "pc").</summary>
    public string Kind { get; set; } = "pc";

    /// <summary>Retrato referenciado de la biblioteca de imágenes (ADR-035).</summary>
    public Guid? PortraitId { get; set; }

    /// <summary>Biografía del personaje (texto largo).</summary>
    public string? Biography { get; set; }

    /// <summary>Personalidad del personaje.</summary>
    public string? Personality { get; set; }

    public int Level { get; set; } = 1;

    /// <summary>Snapshot tipado por sistema (JSON) del build resuelto.</summary>
    public string? SystemDataJson { get; set; }

    /// <summary>
    /// Borrador del creador (JSON): paso actual + selecciones, para
    /// "guardar y continuar después" (CHARACTERS.md §1.2).
    /// </summary>
    public string? BuildJson { get; set; }
}

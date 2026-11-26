namespace ProjectVTT.Api.Domain;

/// <summary>
/// Blueprint de creación de personajes por sistema (ADR-034, CHARACTERS.md
/// §1.1): los pasos del asistente son **datos**, no código. D&D 5e es la
/// primera implementación; un sistema nuevo añade su blueprint sin tocar
/// el motor. Las validaciones y las estadísticas derivadas se resuelven en
/// servidor (endpoint de previsualización).
/// </summary>
public class CharacterBlueprint
{
    public Guid Id { get; set; }

    /// <summary>Sistema de juego (p. ej. "dnd5e").</summary>
    public required string System { get; set; }

    /// <summary>Clave del blueprint dentro del sistema (p. ej. "standard").</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Pasos JSON (CHARACTERS.md §1.1): [{ key, title, kind, contentType?,
    /// fields?, validation?, optional, skippable }], en orden.
    /// </summary>
    public required string StepsJson { get; set; }

    /// <summary>Huecos de equipamiento del sistema (ADR-041, PMB-6.1/6.2).</summary>
    public string? EquipmentSlotsJson { get; set; }

    /// <summary>Estadísticas derivadas que calcula el motor (string[]).</summary>
    public required string DerivedStatsJson { get; set; }

    /// <summary>Reglas de equipo inicial por sistema (JSON).</summary>
    public string? StartingEquipmentRulesJson { get; set; }

    public DateTime CreatedAt { get; set; }
}

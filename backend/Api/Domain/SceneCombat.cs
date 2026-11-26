namespace ProjectVTT.Api.Domain;

/// <summary>
/// Combate activo de una Escena (Épica F, ADR-018): un único encuentro
/// por escena (1:1) con ronda y turno actual. El estado completo se
/// envía en cada evento `CombatUpdated`; los combatientes viven en la
/// tabla `combatants`.
/// </summary>
public class SceneCombat
{
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public int Round { get; set; }
    public Guid? CurrentCombatantId { get; set; }
    public Combatant? CurrentCombatant { get; set; }
    public DateTime StartedAt { get; set; }
}

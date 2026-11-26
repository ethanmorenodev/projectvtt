namespace ProjectVTT.Api.History;

/// <summary>
/// Tipos de entrada del historial (ADR-021). Solo acciones sobre el
/// estado compartido: las notas del GM y los mensajes privados NO se
/// registran (privacidad).
/// </summary>
public static class HistoryKinds
{
    public const string SceneCreated = "scene-created";
    public const string SceneDeleted = "scene-deleted";
    public const string TokenCreated = "token-created";
    public const string ActorCreated = "actor-created";
    public const string ActorUpdated = "actor-updated";
    public const string ItemCreated = "item-created";
    public const string ItemUpdated = "item-updated";
    public const string ItemDeleted = "item-deleted";
    public const string EffectApplied = "effect-applied";
    public const string EffectRemoved = "effect-removed";
    public const string RollResolved = "roll-resolved";
    public const string CombatStarted = "combat-started";
    public const string CombatEnded = "combat-ended";
}

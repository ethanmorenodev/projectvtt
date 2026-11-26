namespace ProjectVTT.Api.Content;

/// <summary>Nodo del grafo de lore (PMB-7.3): visibilidad ya aplicada.</summary>
public sealed record LoreNodeDto(
    Guid Id, string Name, string TypeKey, string? Summary, bool IsPrivate);

/// <summary>Relación tipada entre dos nodos visibles.</summary>
public sealed record LoreEdgeDto(
    Guid Id, Guid FromItemId, string Kind, Guid ToItemId, string? Label);

public sealed record LoreGraphDto(
    IReadOnlyList<LoreNodeDto> Nodes, IReadOnlyList<LoreEdgeDto> Edges);

/// <summary>Entrada de la cronología, ordenada por el contenido.</summary>
public sealed record LoreEventDto(
    Guid Id, string Name, string? Summary, string? HappenedAt, long Order);

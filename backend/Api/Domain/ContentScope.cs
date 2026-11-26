namespace ProjectVTT.Api.Domain;

/// <summary>
/// Ámbito de un item de contenido (ADR-031 §6). `global` = oficial o
/// compartido de solo lectura; `user` = personal; `world` = compartido por
/// campañas de un mundo (ADR-032); `campaign` = de una campaña; `dm` =
/// exclusivo del DM dentro de la campaña.
/// </summary>
public enum ContentScope
{
    Global,
    User,
    World,
    Campaign,
    Dm,
}

/// <summary>
/// Estado de publicación (ADR-031 §6): `Draft` solo lo ve su autor/DM;
/// `Proposed` espera revisión; `Published` está disponible según ámbito y
/// permisos.
/// </summary>
public enum ContentWorkflowState
{
    Draft,
    Proposed,
    Published,
}

/// <summary>
/// Modo de herencia por campo (ADR-031 §5): `Inherit` usa el valor del
/// padre; `Override` lo sustituye; `Extend` acumula (listas y modificadores
/// se suman).
/// </summary>
public enum InheritanceMode
{
    Inherit,
    Override,
    Extend,
}

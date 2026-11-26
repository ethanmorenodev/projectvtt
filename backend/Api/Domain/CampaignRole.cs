namespace ProjectVTT.Api.Domain;

/// <summary>
/// Rol de un miembro dentro de una campaña. El orden numérico importa:
/// un rol mayor da permisos de rol menor (Player &lt; GM &lt; Owner).
/// </summary>
public enum CampaignRole
{
    Player = 0,
    GM = 1,
    Owner = 2,
}

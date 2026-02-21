namespace ProformaFarm.Domain.Entities;

public sealed class CentroCusto
{
    public int IdCentroCusto { get; set; }
    public int IdOrganizacao { get; set; }
    public string Codigo { get; set; } = default!;
    public string Descricao { get; set; } = default!;
    public bool Ativo { get; set; }
}

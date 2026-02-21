namespace ProformaFarm.Domain.Entities;

public sealed class Cargo
{
    public int IdCargo { get; set; }
    public int IdOrganizacao { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nome { get; set; } = default!;
    public bool Ativo { get; set; }
}

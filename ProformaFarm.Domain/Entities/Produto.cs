namespace ProformaFarm.Domain.Entities;

public sealed class Produto
{
    public int IdProduto { get; set; }
    public int IdOrganizacao { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nome { get; set; } = default!;
    public bool ControlaLote { get; set; }
    public bool Ativo { get; set; }
}

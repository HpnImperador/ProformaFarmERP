namespace ProformaFarm.Domain.Entities;

public sealed class Estoque
{
    public int IdEstoque { get; set; }
    public int IdOrganizacao { get; set; }
    public int IdUnidadeOrganizacional { get; set; }
    public int IdProduto { get; set; }
    public int? IdLote { get; set; }
    public decimal QuantidadeDisponivel { get; set; }
    public decimal QuantidadeReservada { get; set; }
}

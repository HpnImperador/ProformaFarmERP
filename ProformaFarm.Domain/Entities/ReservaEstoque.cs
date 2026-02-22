using System;

namespace ProformaFarm.Domain.Entities;

public sealed class ReservaEstoque
{
    public int IdReservaEstoque { get; set; }
    public int IdOrganizacao { get; set; }
    public int IdUnidadeOrganizacional { get; set; }
    public int IdProduto { get; set; }
    public int? IdLote { get; set; }
    public decimal Quantidade { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public string Status { get; set; } = default!;
    public string? DocumentoReferencia { get; set; }
}

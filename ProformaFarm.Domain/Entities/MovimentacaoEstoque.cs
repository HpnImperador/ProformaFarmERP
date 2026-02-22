using System;

namespace ProformaFarm.Domain.Entities;

public sealed class MovimentacaoEstoque
{
    public int IdMovimentacaoEstoque { get; set; }
    public int IdOrganizacao { get; set; }
    public int IdUnidadeOrganizacional { get; set; }
    public int IdProduto { get; set; }
    public int? IdLote { get; set; }
    public string TipoMovimento { get; set; } = default!;
    public decimal Quantidade { get; set; }
    public string? DocumentoReferencia { get; set; }
    public DateTime DataMovimento { get; set; }
}

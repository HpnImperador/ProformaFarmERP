using System;

namespace ProformaFarm.Domain.Entities;

public sealed class Lote
{
    public int IdLote { get; set; }
    public int IdOrganizacao { get; set; }
    public int IdProduto { get; set; }
    public string NumeroLote { get; set; } = default!;
    public DateTime? DataFabricacao { get; set; }
    public DateTime? DataValidade { get; set; }
    public bool Bloqueado { get; set; }
}

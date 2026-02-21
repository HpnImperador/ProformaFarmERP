using System;

namespace ProformaFarm.Domain.Entities;

public sealed class UnidadeOrganizacional
{
    public int IdUnidadeOrganizacional { get; set; }
    public int IdOrganizacao { get; set; }
    public int? IdUnidadePai { get; set; }
    public string Tipo { get; set; } = default!;
    public string Codigo { get; set; } = default!;
    public string Nome { get; set; } = default!;
    public bool Ativa { get; set; }
    public DateTime DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
}

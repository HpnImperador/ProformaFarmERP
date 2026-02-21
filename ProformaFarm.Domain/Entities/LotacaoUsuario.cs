using System;

namespace ProformaFarm.Domain.Entities;

public sealed class LotacaoUsuario
{
    public int IdLotacaoUsuario { get; set; }
    public int IdUsuario { get; set; }
    public int IdUnidadeOrganizacional { get; set; }
    public int? IdCargo { get; set; }
    public DateTime DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public bool Principal { get; set; }
    public bool Ativa { get; set; }
}

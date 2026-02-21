using System;

namespace ProformaFarm.Domain.Entities;

public sealed class Organizacao
{
    public int IdOrganizacao { get; set; }
    public string RazaoSocial { get; set; } = default!;
    public string? NomeFantasia { get; set; }
    public string Cnpj { get; set; } = default!;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
}

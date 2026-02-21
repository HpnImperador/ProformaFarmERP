using System;

namespace ProformaFarm.Domain.Entities;

public sealed class Usuario
{
    public int IdUsuario { get; set; }
    public string Nome { get; set; } = default!;
    public string Login { get; set; } = default!;
    public string SenhaHash { get; set; } = default!;
    public string? SenhaSalt { get; set; }
    public bool Ativo { get; set; }

    public DateTime DataCriacao { get; set; }  // ✅ ADICIONAR
}
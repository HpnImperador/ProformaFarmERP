using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Services.Security;
using ProformaFarm.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/seed")]
public sealed class SeedController : ControllerBase
{
    private readonly ISqlConnectionFactory _factory;
    private readonly IPasswordService _passwords;
    private readonly IWebHostEnvironment _env;

    public SeedController(ISqlConnectionFactory factory, IPasswordService passwords, IWebHostEnvironment env)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _passwords = passwords ?? throw new ArgumentNullException(nameof(passwords));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>
    /// Endpoint TEMPORÁRIO para criar perfis e usuário admin com senha hash.
    /// Remover após o primeiro uso.
    /// </summary>
    [HttpPost("admin")]
    public async Task<IActionResult> SeedAdmin(
        [FromQuery] string senha = "Admin@123",
        [FromQuery] bool reset = false)
    {
        // Segurança básica: seed só em Development
        if (!_env.IsDevelopment())
            return NotFound();

        using var cn = _factory.CreateConnection();

        // 1) Cria perfis básicos (idempotente)
        const string upsertPerfis = @"
IF NOT EXISTS (SELECT 1 FROM Perfil WHERE Nome = 'ADMIN') INSERT INTO Perfil (Nome) VALUES ('ADMIN');
IF NOT EXISTS (SELECT 1 FROM Perfil WHERE Nome = 'GESTOR') INSERT INTO Perfil (Nome) VALUES ('GESTOR');
IF NOT EXISTS (SELECT 1 FROM Perfil WHERE Nome = 'CAIXA')  INSERT INTO Perfil (Nome) VALUES ('CAIXA');
IF NOT EXISTS (SELECT 1 FROM Perfil WHERE Nome = 'FISCAL') INSERT INTO Perfil (Nome) VALUES ('FISCAL');
IF NOT EXISTS (SELECT 1 FROM Perfil WHERE Nome = 'SNGPC')  INSERT INTO Perfil (Nome) VALUES ('SNGPC');
";
        await cn.ExecuteAsync(upsertPerfis);

        // 2) Verifica se admin já existe
        const string getAdmin = @"
SELECT TOP 1
  IdUsuario, Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao
FROM dbo.Usuario
WHERE Login = 'admin';
";
        var admin = await cn.QueryFirstOrDefaultAsync<Usuario>(getAdmin);

        if (admin is null)
        {
            var (hash, salt) = _passwords.HashPassword(senha);

            const string insertAdmin = @"
INSERT INTO dbo.Usuario (Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao)
OUTPUT INSERTED.IdUsuario
VALUES (@Nome, @Login, @SenhaHash, @SenhaSalt, @Ativo, @DataCriacao);
";

            var idUsuario = await cn.ExecuteScalarAsync<int>(insertAdmin, new
            {
                Nome = "Administrador",
                Login = "admin",
                SenhaHash = hash,
                SenhaSalt = salt,
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            });

            await GarantirVinculoAdminAsync(cn, idUsuario);

            return Ok(new
            {
                message = "Seed concluído: admin criado com sucesso.",
                login = "admin",
                senha,
                idUsuario
            });
        }

        // Admin já existe -> garante vínculo ADMIN e opcionalmente reseta senha
        if (reset)
        {
            var (hash, salt) = _passwords.HashPassword(senha);

            const string updateSenha = @"
UPDATE dbo.Usuario
SET SenhaHash = @SenhaHash,
    SenhaSalt = @SenhaSalt
WHERE IdUsuario = @IdUsuario;
";
            await cn.ExecuteAsync(updateSenha, new
            {
                IdUsuario = admin.IdUsuario,
                SenhaHash = hash,
                SenhaSalt = salt
            });
        }

        await GarantirVinculoAdminAsync(cn, admin.IdUsuario);

        return Ok(new
        {
            message = reset
                ? "Admin já existia. Senha foi resetada e vínculo ao perfil ADMIN garantido."
                : "Admin já existia. Vínculo ao perfil ADMIN garantido.",
            login = "admin",
            senha = reset ? senha : null,
            idUsuario = admin.IdUsuario
        });
    }

    private static Task GarantirVinculoAdminAsync(System.Data.IDbConnection cn, int idUsuario)
    {
        const string sql = @"
DECLARE @IdPerfilAdmin INT = (SELECT IdPerfil FROM dbo.Perfil WHERE Nome = 'ADMIN');
IF NOT EXISTS (
    SELECT 1 FROM dbo.UsuarioPerfil
    WHERE IdUsuario = @IdUsuario AND IdPerfil = @IdPerfilAdmin
)
    INSERT INTO dbo.UsuarioPerfil (IdUsuario, IdPerfil)
    VALUES (@IdUsuario, @IdPerfilAdmin);
";
        return cn.ExecuteAsync(sql, new { IdUsuario = idUsuario });
    }
}

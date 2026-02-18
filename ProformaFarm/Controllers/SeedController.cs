using ProformaFarm.Infrastructure.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using ProformaFarm.Application.Services.Security;
using ProformaFarm.Domain.Entities;
using ProformaFarm.Infrastructure.Data;
using ProformaFarm.Domain.Entities;
namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/seed")]
public sealed class SeedController : ControllerBase
{
    private readonly ISqlConnectionFactory _factory;
    private readonly IPasswordService _passwords;

    public SeedController(ISqlConnectionFactory factory, IPasswordService passwords)
    {
        _factory = factory;
        _passwords = passwords;
    }

    /// <summary>
    /// Endpoint TEMPORÁRIO para criar perfis e usuário admin com senha hash.
    /// Remover após o primeiro uso.
    /// </summary>
    /// <remarks>
    /// Exemplo:
    /// POST /api/seed/admin?senha=Admin@123
    /// </remarks>
    [HttpPost("admin")]
    public async Task<IActionResult> SeedAdmin([FromQuery] string senha = "Admin@123")
    {
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
        const string getAdmin = @"SELECT TOP 1 IdUsuario, Nome, Login, SenhaHash, Ativo, DataCriacao FROM Usuario WHERE Login = 'admin';";
        var admin = await cn.QueryFirstOrDefaultAsync<Usuario>(getAdmin);

        if (admin is null)
        {
            // 3) Cria hash
            var novo = new Usuario
            {
                Nome = "Administrador",
                Login = "admin",
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            };
            var (hash, salt) = _passwords.HashPassword(senha);

            // 4) Insere usuário e retorna IdUsuario
            const string insertAdmin = @"
INSERT INTO Usuario (Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao)
OUTPUT INSERTED.IdUsuario
VALUES (@Nome, @Login, @SenhaHash, @SenhaSalt, @Ativo, @DataCriacao);
";

            var idUsuario = await cn.ExecuteScalarAsync<int>(insertAdmin, new
            {
                novo.Nome,
                novo.Login,
                SenhaHash = hash,
                SenhaSalt = salt,
                Ativo = novo.Ativo,
                DataCriacao = novo.DataCriacao
            });

            // 5) Vincula ao perfil ADMIN
            const string vincularAdmin = @"
DECLARE @IdPerfilAdmin INT = (SELECT IdPerfil FROM Perfil WHERE Nome = 'ADMIN');
IF NOT EXISTS (SELECT 1 FROM UsuarioPerfil WHERE IdUsuario = @IdUsuario AND IdPerfil = @IdPerfilAdmin)
    INSERT INTO UsuarioPerfil (IdUsuario, IdPerfil) VALUES (@IdUsuario, @IdPerfilAdmin);
";
            await cn.ExecuteAsync(vincularAdmin, new { IdUsuario = idUsuario });

            return Ok(new
            {
                message = "Seed concluído: admin criado com sucesso.",
                login = "admin",
                senha = senha,
                idUsuario
            });
        }
        else
        {
            // Admin já existe -> garante vínculo ADMIN
            const string garantirVinculo = @"
DECLARE @IdPerfilAdmin INT = (SELECT IdPerfil FROM Perfil WHERE Nome = 'ADMIN');
IF NOT EXISTS (SELECT 1 FROM UsuarioPerfil WHERE IdUsuario = @IdUsuario AND IdPerfil = @IdPerfilAdmin)
    INSERT INTO UsuarioPerfil (IdUsuario, IdPerfil) VALUES (@IdUsuario, @IdPerfilAdmin);
";
            await cn.ExecuteAsync(garantirVinculo, new { IdUsuario = admin.IdUsuario });

            return Ok(new
            {
                message = "Admin já existia. Vínculo ao perfil ADMIN garantido.",
                login = "admin",
                idUsuario = admin.IdUsuario
            });
        }
    }
}

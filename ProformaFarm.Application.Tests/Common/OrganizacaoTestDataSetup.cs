using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Services.Security;

namespace ProformaFarm.Application.Tests.Common;

public static class OrganizacaoTestDataSetup
{
    private const string Login = "it_org_admin";
    private const string Senha = "It@Org123";
    private const string Nome = "IT Organizacao Admin";
    private const string Cnpj = "99999999000199";
    private const string CodigoMatriz = "IT-MATRIZ";
    private const string CodigoFilial = "IT-FILIAL-001";
    private const string CodigoCentroCusto = "IT-CC-ADM";
    private const string CodigoCargo = "IT-CARGO-GER";

    public static async Task<OrganizacaoTestDataResult> EnsureAsync(CustomWebApplicationFactory factory)
    {
        _ = factory.CreateClient();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();

        await using var tx = await cn.BeginTransactionAsync();

        // Evita corrida entre classes de teste concorrentes no mesmo banco.
        await cn.ExecuteAsync(
            "EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;",
            new { r = "PF_IT_ORG_SETUP" },
            tx);

        var passwordService = new PasswordService();
        var (hash, salt) = passwordService.HashPassword(Senha);

        var idUsuario = await UpsertUsuarioAsync(cn, tx, hash, salt);
        var idOrganizacao = await UpsertOrganizacaoAsync(cn, tx);
        var idMatriz = await UpsertUnidadeAsync(cn, tx, idOrganizacao, null, "Matriz", CodigoMatriz, "IT Matriz");
        _ = await UpsertUnidadeAsync(cn, tx, idOrganizacao, idMatriz, "Filial", CodigoFilial, "IT Filial 001");
        var idCentroCusto = await UpsertCentroCustoAsync(cn, tx, idOrganizacao);
        await UpsertUnidadeCentroCustoAsync(cn, tx, idMatriz, idCentroCusto);
        var idCargo = await UpsertCargoAsync(cn, tx, idOrganizacao);
        await UpsertLotacaoPrincipalAsync(cn, tx, idUsuario, idMatriz, idCargo);

        await tx.CommitAsync();

        return new OrganizacaoTestDataResult
        {
            IdUsuario = idUsuario,
            IdOrganizacao = idOrganizacao,
            IdUnidade = idMatriz,
            Login = Login,
            Senha = Senha
        };
    }

    private static async Task<int> UpsertUsuarioAsync(IDbConnection cn, IDbTransaction tx, string hash, string salt)
    {
        var idUsuario = await cn.ExecuteScalarAsync<int?>(
            "SELECT TOP (1) IdUsuario FROM dbo.Usuario WHERE Login = @Login;",
            new { Login },
            tx);

        if (idUsuario.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE dbo.Usuario
                  SET Nome = @Nome,
                      SenhaHash = @Hash,
                      SenhaSalt = @Salt,
                      Ativo = 1
                  WHERE IdUsuario = @IdUsuario;",
                new { Nome, Hash = hash, Salt = salt, IdUsuario = idUsuario.Value },
                tx);

            return idUsuario.Value;
        }

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Usuario (Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao)
              OUTPUT INSERTED.IdUsuario
              VALUES (@Nome, @Login, @Hash, @Salt, 1, SYSUTCDATETIME());",
            new { Nome, Login, Hash = hash, Salt = salt },
            tx);
    }

    private static async Task<int> UpsertOrganizacaoAsync(IDbConnection cn, IDbTransaction tx)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            "SELECT TOP (1) IdOrganizacao FROM dbo.Organizacao WHERE Cnpj = @Cnpj;",
            new { Cnpj },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Organizacao (RazaoSocial, NomeFantasia, Cnpj, Ativa, DataCriacao)
              OUTPUT INSERTED.IdOrganizacao
              VALUES (N'IT Organizacao Ltda', N'IT Org', @Cnpj, 1, SYSUTCDATETIME());",
            new { Cnpj },
            tx);
    }

    private static async Task<int> UpsertUnidadeAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int? idUnidadePai,
        string tipo,
        string codigo,
        string nome)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT TOP (1) IdUnidadeOrganizacional
              FROM dbo.UnidadeOrganizacional
              WHERE IdOrganizacao = @IdOrganizacao AND Codigo = @Codigo;",
            new { IdOrganizacao = idOrganizacao, Codigo = codigo },
            tx);

        if (id.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE dbo.UnidadeOrganizacional
                  SET IdUnidadePai = @IdUnidadePai,
                      Tipo = @Tipo,
                      Nome = @Nome,
                      Ativa = 1,
                      DataFim = NULL
                  WHERE IdUnidadeOrganizacional = @IdUnidade;",
                new
                {
                    IdUnidadePai = idUnidadePai,
                    Tipo = tipo,
                    Nome = nome,
                    IdUnidade = id.Value
                },
                tx);

            return id.Value;
        }

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.UnidadeOrganizacional
              (IdOrganizacao, IdUnidadePai, Tipo, Codigo, Nome, Ativa, DataInicio, DataFim)
              OUTPUT INSERTED.IdUnidadeOrganizacional
              VALUES (@IdOrganizacao, @IdUnidadePai, @Tipo, @Codigo, @Nome, 1, SYSUTCDATETIME(), NULL);",
            new
            {
                IdOrganizacao = idOrganizacao,
                IdUnidadePai = idUnidadePai,
                Tipo = tipo,
                Codigo = codigo,
                Nome = nome
            },
            tx);
    }

    private static async Task<int> UpsertCentroCustoAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT TOP (1) IdCentroCusto
              FROM dbo.CentroCusto
              WHERE IdOrganizacao = @IdOrganizacao AND Codigo = @Codigo;",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCentroCusto },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.CentroCusto (IdOrganizacao, Codigo, Descricao, Ativo)
              OUTPUT INSERTED.IdCentroCusto
              VALUES (@IdOrganizacao, @Codigo, N'IT Centro de Custo', 1);",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCentroCusto },
            tx);
    }

    private static async Task UpsertUnidadeCentroCustoAsync(IDbConnection cn, IDbTransaction tx, int idUnidade, int idCentroCusto)
    {
        if (await cn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1)
                  FROM dbo.UnidadeCentroCusto
                  WHERE IdUnidadeOrganizacional = @IdUnidade AND IdCentroCusto = @IdCentroCusto;",
                new { IdUnidade = idUnidade, IdCentroCusto = idCentroCusto },
                tx) == 0)
        {
            await cn.ExecuteAsync(
                "INSERT INTO dbo.UnidadeCentroCusto (IdUnidadeOrganizacional, IdCentroCusto, Principal) VALUES (@IdUnidade, @IdCentroCusto, 1);",
                new { IdUnidade = idUnidade, IdCentroCusto = idCentroCusto },
                tx);
        }
    }

    private static async Task<int> UpsertCargoAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT TOP (1) IdCargo
              FROM dbo.Cargo
              WHERE IdOrganizacao = @IdOrganizacao AND Codigo = @Codigo;",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCargo },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Cargo (IdOrganizacao, Codigo, Nome, Ativo)
              OUTPUT INSERTED.IdCargo
              VALUES (@IdOrganizacao, @Codigo, N'IT Gerente', 1);",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCargo },
            tx);
    }

    private static async Task UpsertLotacaoPrincipalAsync(IDbConnection cn, IDbTransaction tx, int idUsuario, int idMatriz, int idCargo)
    {
        var lotacaoAtual = await cn.QueryFirstOrDefaultAsync<(int IdLotacaoUsuario, int IdUnidadeOrganizacional)>(
            @"SELECT TOP (1) IdLotacaoUsuario, IdUnidadeOrganizacional
              FROM dbo.LotacaoUsuario
              WHERE IdUsuario = @IdUsuario AND Principal = 1 AND Ativa = 1
              ORDER BY IdLotacaoUsuario DESC;",
            new { IdUsuario = idUsuario },
            tx);

        if (lotacaoAtual.IdLotacaoUsuario != 0 && lotacaoAtual.IdUnidadeOrganizacional == idMatriz)
            return;

        if (lotacaoAtual.IdLotacaoUsuario != 0)
        {
            await cn.ExecuteAsync(
                @"UPDATE dbo.LotacaoUsuario
                  SET Principal = 0,
                      Ativa = 0,
                      DataFim = COALESCE(DataFim, SYSUTCDATETIME())
                  WHERE IdLotacaoUsuario = @IdLotacao;",
                new { IdLotacao = lotacaoAtual.IdLotacaoUsuario },
                tx);
        }

        await cn.ExecuteAsync(
            @"INSERT INTO dbo.LotacaoUsuario
              (IdUsuario, IdUnidadeOrganizacional, IdCargo, DataInicio, DataFim, Principal, Ativa)
              VALUES (@IdUsuario, @IdMatriz, @IdCargo, SYSUTCDATETIME(), NULL, 1, 1);",
            new { IdUsuario = idUsuario, IdMatriz = idMatriz, IdCargo = idCargo },
            tx);
    }
}

public sealed class OrganizacaoTestDataResult
{
    public int IdUsuario { get; init; }
    public int IdOrganizacao { get; init; }
    public int IdUnidade { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

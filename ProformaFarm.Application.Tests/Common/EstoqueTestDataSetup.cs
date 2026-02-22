using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProformaFarm.Application.Tests.Common;

public static class EstoqueTestDataSetup
{
    public static async Task<EstoqueTestDataResult> EnsureAsync(CustomWebApplicationFactory factory)
    {
        var orgSetup = await OrganizacaoTestDataSetup.EnsureAsync(factory);
        var testKey = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var codigoProduto = $"IT-PROD-{testKey}";
        var nomeProduto = $"IT Produto {testKey}";
        var numeroLote = $"IT-LOTE-{testKey}";
        var refReservaAtiva = $"IT-RES-ACTIVE-{testKey}";
        var refReservaExpirada = $"IT-RES-EXPIRED-{testKey}";

        _ = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();

                await using var tx = await cn.BeginTransactionAsync();

                await cn.ExecuteAsync(
                    "EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;",
                    new { r = "PF_IT_ESTOQUE_SETUP" },
                    tx);

                var idProduto = await UpsertProdutoAsync(cn, tx, orgSetup.IdOrganizacao, codigoProduto, nomeProduto);
                var idLote = await UpsertLoteAsync(cn, tx, orgSetup.IdOrganizacao, idProduto, numeroLote);
                var idEstoque = await UpsertEstoqueAsync(cn, tx, orgSetup.IdOrganizacao, orgSetup.IdUnidade, idProduto, idLote);
                var idReservaAtiva = await UpsertReservaAtivaAsync(cn, tx, orgSetup.IdOrganizacao, orgSetup.IdUnidade, idProduto, idLote, refReservaAtiva);
                var idReservaExpirada = await UpsertReservaExpiradaAsync(cn, tx, orgSetup.IdOrganizacao, orgSetup.IdUnidade, idProduto, idLote, refReservaExpirada);

                await tx.CommitAsync();

                return new EstoqueTestDataResult
                {
                    IdOrganizacao = orgSetup.IdOrganizacao,
                    IdUnidade = orgSetup.IdUnidade,
                    IdProduto = idProduto,
                    IdLote = idLote,
                    IdEstoque = idEstoque,
                    IdReservaAtiva = idReservaAtiva,
                    IdReservaExpirada = idReservaExpirada,
                    CodigoProduto = codigoProduto,
                    DocumentoReservaAtiva = refReservaAtiva,
                    Login = orgSetup.Login,
                    Senha = orgSetup.Senha
                };
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt));
            }
        }

        throw new InvalidOperationException("Falha ao preparar dados de estoque apos retries por deadlock.");
    }

    private static async Task<int> UpsertProdutoAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao, string codigoProduto, string nomeProduto)
    {
        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Produto (IdOrganizacao, Codigo, Nome, ControlaLote, Ativo)
              OUTPUT INSERTED.IdProduto
              VALUES (@IdOrganizacao, @Codigo, @Nome, 1, 1);",
            new { IdOrganizacao = idOrganizacao, Codigo = codigoProduto, Nome = nomeProduto },
            tx);
    }

    private static async Task<int> UpsertLoteAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao, int idProduto, string numeroLote)
    {
        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Lote (IdOrganizacao, IdProduto, NumeroLote, DataFabricacao, DataValidade, Bloqueado)
              OUTPUT INSERTED.IdLote
              VALUES (@IdOrganizacao, @IdProduto, @NumeroLote, DATEADD(DAY, -30, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 0);",
            new { IdOrganizacao = idOrganizacao, IdProduto = idProduto, NumeroLote = numeroLote },
            tx);
    }

    private static async Task<int> UpsertEstoqueAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int idUnidade,
        int idProduto,
        int idLote)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT TOP (1) IdEstoque
              FROM dbo.Estoque
              WHERE IdOrganizacao = @IdOrganizacao
                AND IdUnidadeOrganizacional = @IdUnidade
                AND IdProduto = @IdProduto
                AND ((IdLote IS NULL AND @IdLote IS NULL) OR IdLote = @IdLote);",
            new { IdOrganizacao = idOrganizacao, IdUnidade = idUnidade, IdProduto = idProduto, IdLote = idLote },
            tx);

        if (id.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE dbo.Estoque
                  SET QuantidadeDisponivel = 120,
                      QuantidadeReservada = 20
                  WHERE IdEstoque = @IdEstoque;",
                new { IdEstoque = id.Value },
                tx);
            return id.Value;
        }

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.Estoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, QuantidadeDisponivel, QuantidadeReservada)
              OUTPUT INSERTED.IdEstoque
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 120, 20);",
            new { IdOrganizacao = idOrganizacao, IdUnidade = idUnidade, IdProduto = idProduto, IdLote = idLote },
            tx);
    }

    private static async Task<int> UpsertReservaAtivaAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int idUnidade,
        int idProduto,
        int idLote,
        string documentoReferencia)
    {
        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.ReservaEstoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, ExpiraEmUtc, Status, DocumentoReferencia)
              OUTPUT INSERTED.IdReservaEstoque
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 10, DATEADD(HOUR, 2, SYSUTCDATETIME()), N'ATIVA', @DocumentoReferencia);",
            new { IdOrganizacao = idOrganizacao, IdUnidade = idUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = documentoReferencia },
            tx);
    }

    private static async Task<int> UpsertReservaExpiradaAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int idUnidade,
        int idProduto,
        int idLote,
        string documentoReferencia)
    {
        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO dbo.ReservaEstoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, ExpiraEmUtc, Status, DocumentoReferencia)
              OUTPUT INSERTED.IdReservaEstoque
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 4, DATEADD(HOUR, -2, SYSUTCDATETIME()), N'ATIVA', @DocumentoReferencia);",
            new { IdOrganizacao = idOrganizacao, IdUnidade = idUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = documentoReferencia },
            tx);
    }
}

public sealed class EstoqueTestDataResult
{
    public int IdOrganizacao { get; init; }
    public int IdUnidade { get; init; }
    public int IdProduto { get; init; }
    public int IdLote { get; init; }
    public int IdEstoque { get; init; }
    public int IdReservaAtiva { get; init; }
    public int IdReservaExpirada { get; init; }
    public string CodigoProduto { get; init; } = string.Empty;
    public string DocumentoReservaAtiva { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

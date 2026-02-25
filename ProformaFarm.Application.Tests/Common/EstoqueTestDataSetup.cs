using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Interfaces.Data;

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
        var sqlFactory = factory.Services.GetRequiredService<ISqlConnectionFactory>();
        var isPostgres = sqlFactory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || sqlFactory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);

        return isPostgres
            ? await EnsurePostgresAsync(factory, sqlFactory, orgSetup, codigoProduto, nomeProduto, numeroLote, refReservaAtiva, refReservaExpirada)
            : await EnsureSqlServerAsync(factory, orgSetup, codigoProduto, nomeProduto, numeroLote, refReservaAtiva, refReservaExpirada);
    }

    private static async Task<EstoqueTestDataResult> EnsureSqlServerAsync(
        CustomWebApplicationFactory factory,
        OrganizacaoTestDataResult orgSetup,
        string codigoProduto,
        string nomeProduto,
        string numeroLote,
        string refReservaAtiva,
        string refReservaExpirada)
    {
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

                var idProduto = await cn.ExecuteScalarAsync<int>(
                    @"INSERT INTO dbo.Produto (IdOrganizacao, Codigo, Nome, ControlaLote, Ativo)
                      OUTPUT INSERTED.IdProduto
                      VALUES (@IdOrganizacao, @Codigo, @Nome, 1, 1);",
                    new { IdOrganizacao = orgSetup.IdOrganizacao, Codigo = codigoProduto, Nome = nomeProduto },
                    tx);

                var idLote = await cn.ExecuteScalarAsync<int>(
                    @"INSERT INTO dbo.Lote (IdOrganizacao, IdProduto, NumeroLote, DataFabricacao, DataValidade, Bloqueado)
                      OUTPUT INSERTED.IdLote
                      VALUES (@IdOrganizacao, @IdProduto, @NumeroLote, DATEADD(DAY, -30, SYSUTCDATETIME()), DATEADD(DAY, 365, SYSUTCDATETIME()), 0);",
                    new { IdOrganizacao = orgSetup.IdOrganizacao, IdProduto = idProduto, NumeroLote = numeroLote },
                    tx);

                var idEstoque = await cn.ExecuteScalarAsync<int>(
                    @"INSERT INTO dbo.Estoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, QuantidadeDisponivel, QuantidadeReservada)
                      OUTPUT INSERTED.IdEstoque
                      VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 120, 20);",
                    new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote },
                    tx);

                var idReservaAtiva = await cn.ExecuteScalarAsync<int>(
                    @"INSERT INTO dbo.ReservaEstoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, ExpiraEmUtc, Status, DocumentoReferencia)
                      OUTPUT INSERTED.IdReservaEstoque
                      VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 10, DATEADD(HOUR, 2, SYSUTCDATETIME()), N'ATIVA', @DocumentoReferencia);",
                    new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = refReservaAtiva },
                    tx);

                var idReservaExpirada = await cn.ExecuteScalarAsync<int>(
                    @"INSERT INTO dbo.ReservaEstoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, ExpiraEmUtc, Status, DocumentoReferencia)
                      OUTPUT INSERTED.IdReservaEstoque
                      VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 4, DATEADD(HOUR, -2, SYSUTCDATETIME()), N'ATIVA', @DocumentoReferencia);",
                    new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = refReservaExpirada },
                    tx);

                await tx.CommitAsync();

                return BuildResult(orgSetup, idProduto, idLote, idEstoque, idReservaAtiva, idReservaExpirada, codigoProduto, refReservaAtiva);
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt));
            }
        }

        throw new InvalidOperationException("Falha ao preparar dados de estoque apos retries por deadlock.");
    }

    private static async Task<EstoqueTestDataResult> EnsurePostgresAsync(
        CustomWebApplicationFactory factory,
        ISqlConnectionFactory sqlFactory,
        OrganizacaoTestDataResult orgSetup,
        string codigoProduto,
        string nomeProduto,
        string numeroLote,
        string refReservaAtiva,
        string refReservaExpirada)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
            ?? configuration.GetConnectionString("PostgresConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string PostgreSQL nao configurada para testes.");

        using var db = sqlFactory.CreateConnection();
        if (db is not DbConnection cn)
            throw new InvalidOperationException("Conexao de banco nao suportada para testes.");
        cn.ConnectionString = connectionString;

        await cn.OpenAsync();
        await using var tx = await cn.BeginTransactionAsync();

        await cn.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@r));", new { r = "PF_IT_ESTOQUE_SETUP" }, tx);

        var idProduto = await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Produto"" (""IdOrganizacao"", ""Codigo"", ""Nome"", ""ControlaLote"", ""Ativo"")
              VALUES (@IdOrganizacao, @Codigo, @Nome, TRUE, TRUE)
              RETURNING ""IdProduto"";",
            new { IdOrganizacao = orgSetup.IdOrganizacao, Codigo = codigoProduto, Nome = nomeProduto },
            tx);

        var idLote = await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Lote"" (""IdOrganizacao"", ""IdProduto"", ""NumeroLote"", ""DataFabricacao"", ""DataValidade"", ""Bloqueado"")
              VALUES (@IdOrganizacao, @IdProduto, @NumeroLote, TIMEZONE('UTC', NOW()) - INTERVAL '30 day', TIMEZONE('UTC', NOW()) + INTERVAL '365 day', FALSE)
              RETURNING ""IdLote"";",
            new { IdOrganizacao = orgSetup.IdOrganizacao, IdProduto = idProduto, NumeroLote = numeroLote },
            tx);

        var idEstoque = await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Estoque"" (""IdOrganizacao"", ""IdUnidadeOrganizacional"", ""IdProduto"", ""IdLote"", ""QuantidadeDisponivel"", ""QuantidadeReservada"")
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 120, 20)
              RETURNING ""IdEstoque"";",
            new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote },
            tx);

        var idReservaAtiva = await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""ReservaEstoque"" (""IdOrganizacao"", ""IdUnidadeOrganizacional"", ""IdProduto"", ""IdLote"", ""Quantidade"", ""ExpiraEmUtc"", ""Status"", ""DocumentoReferencia"")
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 10, TIMEZONE('UTC', NOW()) + INTERVAL '2 hour', 'ATIVA', @DocumentoReferencia)
              RETURNING ""IdReservaEstoque"";",
            new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = refReservaAtiva },
            tx);

        var idReservaExpirada = await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""ReservaEstoque"" (""IdOrganizacao"", ""IdUnidadeOrganizacional"", ""IdProduto"", ""IdLote"", ""Quantidade"", ""ExpiraEmUtc"", ""Status"", ""DocumentoReferencia"")
              VALUES (@IdOrganizacao, @IdUnidade, @IdProduto, @IdLote, 4, TIMEZONE('UTC', NOW()) - INTERVAL '2 hour', 'ATIVA', @DocumentoReferencia)
              RETURNING ""IdReservaEstoque"";",
            new { IdOrganizacao = orgSetup.IdOrganizacao, IdUnidade = orgSetup.IdUnidade, IdProduto = idProduto, IdLote = idLote, DocumentoReferencia = refReservaExpirada },
            tx);

        await tx.CommitAsync();

        return BuildResult(orgSetup, idProduto, idLote, idEstoque, idReservaAtiva, idReservaExpirada, codigoProduto, refReservaAtiva);
    }

    private static EstoqueTestDataResult BuildResult(
        OrganizacaoTestDataResult orgSetup,
        int idProduto,
        int idLote,
        int idEstoque,
        int idReservaAtiva,
        int idReservaExpirada,
        string codigoProduto,
        string refReservaAtiva)
    {
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

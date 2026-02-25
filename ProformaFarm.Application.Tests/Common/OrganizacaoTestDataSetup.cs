using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Interfaces.Data;
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

        var sqlFactory = factory.Services.GetRequiredService<ISqlConnectionFactory>();
        var isPostgres = sqlFactory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || sqlFactory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);

        return isPostgres
            ? await EnsurePostgresAsync(factory, sqlFactory)
            : await EnsureSqlServerAsync(factory);
    }

    private static async Task<OrganizacaoTestDataResult> EnsureSqlServerAsync(CustomWebApplicationFactory factory)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();

        await using var tx = await cn.BeginTransactionAsync();

        await cn.ExecuteAsync(
            "EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;",
            new { r = "PF_IT_ORG_SETUP" },
            tx);

        var passwordService = new PasswordService();
        var (hash, salt) = passwordService.HashPassword(Senha);

        var idUsuario = await UpsertUsuarioSqlServerAsync(cn, tx, hash, salt);
        var idOrganizacao = await UpsertOrganizacaoSqlServerAsync(cn, tx);
        var idMatriz = await UpsertUnidadeSqlServerAsync(cn, tx, idOrganizacao, null, "Matriz", CodigoMatriz, "IT Matriz");
        _ = await UpsertUnidadeSqlServerAsync(cn, tx, idOrganizacao, idMatriz, "Filial", CodigoFilial, "IT Filial 001");
        var idCentroCusto = await UpsertCentroCustoSqlServerAsync(cn, tx, idOrganizacao);
        await UpsertUnidadeCentroCustoSqlServerAsync(cn, tx, idMatriz, idCentroCusto);
        var idCargo = await UpsertCargoSqlServerAsync(cn, tx, idOrganizacao);
        await UpsertLotacaoPrincipalSqlServerAsync(cn, tx, idUsuario, idMatriz, idCargo);

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

    private static async Task<OrganizacaoTestDataResult> EnsurePostgresAsync(CustomWebApplicationFactory factory, ISqlConnectionFactory sqlFactory)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var pgConnection = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
            ?? configuration.GetConnectionString("PostgresConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string PostgreSQL nao configurada para testes.");

        using var db = sqlFactory.CreateConnection();
        if (db is not DbConnection cn)
            throw new InvalidOperationException("Conexao de banco nao suportada para testes.");
        cn.ConnectionString = pgConnection;

        await cn.OpenAsync();
        await using var tx = await cn.BeginTransactionAsync();

        await cn.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@r));", new { r = "PF_IT_ORG_SETUP" }, tx);

        var passwordService = new PasswordService();
        var (hash, salt) = passwordService.HashPassword(Senha);

        var idUsuario = await UpsertUsuarioPostgresAsync(cn, tx, hash, salt);
        var idOrganizacao = await UpsertOrganizacaoPostgresAsync(cn, tx);
        var idMatriz = await UpsertUnidadePostgresAsync(cn, tx, idOrganizacao, null, "Matriz", CodigoMatriz, "IT Matriz");
        _ = await UpsertUnidadePostgresAsync(cn, tx, idOrganizacao, idMatriz, "Filial", CodigoFilial, "IT Filial 001");
        var idCentroCusto = await UpsertCentroCustoPostgresAsync(cn, tx, idOrganizacao);
        await UpsertUnidadeCentroCustoPostgresAsync(cn, tx, idMatriz, idCentroCusto);
        var idCargo = await UpsertCargoPostgresAsync(cn, tx, idOrganizacao);
        await UpsertLotacaoPrincipalPostgresAsync(cn, tx, idUsuario, idMatriz, idCargo);

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

    private static async Task<int> UpsertUsuarioSqlServerAsync(IDbConnection cn, IDbTransaction tx, string hash, string salt)
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

    private static async Task<int> UpsertOrganizacaoSqlServerAsync(IDbConnection cn, IDbTransaction tx)
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

    private static async Task<int> UpsertUnidadeSqlServerAsync(
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

    private static async Task<int> UpsertCentroCustoSqlServerAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
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

    private static async Task UpsertUnidadeCentroCustoSqlServerAsync(IDbConnection cn, IDbTransaction tx, int idUnidade, int idCentroCusto)
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

    private static async Task<int> UpsertCargoSqlServerAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
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

    private static async Task UpsertLotacaoPrincipalSqlServerAsync(IDbConnection cn, IDbTransaction tx, int idUsuario, int idMatriz, int idCargo)
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

    private static async Task<int> UpsertUsuarioPostgresAsync(IDbConnection cn, IDbTransaction tx, string hash, string salt)
    {
        var idUsuario = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdUsuario"" FROM public.""Usuario"" WHERE ""Login"" = @Login LIMIT 1;",
            new { Login },
            tx);

        if (idUsuario.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE public.""Usuario""
                  SET ""Nome"" = @Nome,
                      ""SenhaHash"" = @Hash,
                      ""SenhaSalt"" = @Salt,
                      ""Ativo"" = TRUE
                  WHERE ""IdUsuario"" = @IdUsuario;",
                new { Nome, Hash = hash, Salt = salt, IdUsuario = idUsuario.Value },
                tx);
            return idUsuario.Value;
        }

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Usuario"" (""Nome"", ""Login"", ""SenhaHash"", ""SenhaSalt"", ""Ativo"", ""DataCriacao"")
              VALUES (@Nome, @Login, @Hash, @Salt, TRUE, TIMEZONE('UTC', NOW()))
              RETURNING ""IdUsuario"";",
            new { Nome, Login, Hash = hash, Salt = salt },
            tx);
    }

    private static async Task<int> UpsertOrganizacaoPostgresAsync(IDbConnection cn, IDbTransaction tx)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdOrganizacao"" FROM public.""Organizacao"" WHERE ""Cnpj"" = @Cnpj LIMIT 1;",
            new { Cnpj },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Organizacao"" (""RazaoSocial"", ""NomeFantasia"", ""Cnpj"", ""Ativa"", ""DataCriacao"")
              VALUES ('IT Organizacao Ltda', 'IT Org', @Cnpj, TRUE, TIMEZONE('UTC', NOW()))
              RETURNING ""IdOrganizacao"";",
            new { Cnpj },
            tx);
    }

    private static async Task<int> UpsertUnidadePostgresAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int? idUnidadePai,
        string tipo,
        string codigo,
        string nome)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdUnidadeOrganizacional""
              FROM public.""UnidadeOrganizacional""
              WHERE ""IdOrganizacao"" = @IdOrganizacao AND ""Codigo"" = @Codigo
              LIMIT 1;",
            new { IdOrganizacao = idOrganizacao, Codigo = codigo },
            tx);

        if (id.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE public.""UnidadeOrganizacional""
                  SET ""IdUnidadePai"" = @IdUnidadePai,
                      ""Tipo"" = @Tipo,
                      ""Nome"" = @Nome,
                      ""Ativa"" = TRUE,
                      ""DataFim"" = NULL
                  WHERE ""IdUnidadeOrganizacional"" = @IdUnidade;",
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
            @"INSERT INTO public.""UnidadeOrganizacional""
              (""IdOrganizacao"", ""IdUnidadePai"", ""Tipo"", ""Codigo"", ""Nome"", ""Ativa"", ""DataInicio"", ""DataFim"")
              VALUES (@IdOrganizacao, @IdUnidadePai, @Tipo, @Codigo, @Nome, TRUE, TIMEZONE('UTC', NOW()), NULL)
              RETURNING ""IdUnidadeOrganizacional"";",
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

    private static async Task<int> UpsertCentroCustoPostgresAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdCentroCusto""
              FROM public.""CentroCusto""
              WHERE ""IdOrganizacao"" = @IdOrganizacao AND ""Codigo"" = @Codigo
              LIMIT 1;",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCentroCusto },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""CentroCusto"" (""IdOrganizacao"", ""Codigo"", ""Descricao"", ""Ativo"")
              VALUES (@IdOrganizacao, @Codigo, 'IT Centro de Custo', TRUE)
              RETURNING ""IdCentroCusto"";",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCentroCusto },
            tx);
    }

    private static async Task UpsertUnidadeCentroCustoPostgresAsync(IDbConnection cn, IDbTransaction tx, int idUnidade, int idCentroCusto)
    {
        if (await cn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1)
                  FROM public.""UnidadeCentroCusto""
                  WHERE ""IdUnidadeOrganizacional"" = @IdUnidade AND ""IdCentroCusto"" = @IdCentroCusto;",
                new { IdUnidade = idUnidade, IdCentroCusto = idCentroCusto },
                tx) == 0)
        {
            await cn.ExecuteAsync(
                @"INSERT INTO public.""UnidadeCentroCusto"" (""IdUnidadeOrganizacional"", ""IdCentroCusto"", ""Principal"")
                  VALUES (@IdUnidade, @IdCentroCusto, TRUE);",
                new { IdUnidade = idUnidade, IdCentroCusto = idCentroCusto },
                tx);
        }
    }

    private static async Task<int> UpsertCargoPostgresAsync(IDbConnection cn, IDbTransaction tx, int idOrganizacao)
    {
        var id = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdCargo""
              FROM public.""Cargo""
              WHERE ""IdOrganizacao"" = @IdOrganizacao AND ""Codigo"" = @Codigo
              LIMIT 1;",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCargo },
            tx);

        if (id.HasValue)
            return id.Value;

        return await cn.ExecuteScalarAsync<int>(
            @"INSERT INTO public.""Cargo"" (""IdOrganizacao"", ""Codigo"", ""Nome"", ""Ativo"")
              VALUES (@IdOrganizacao, @Codigo, 'IT Gerente', TRUE)
              RETURNING ""IdCargo"";",
            new { IdOrganizacao = idOrganizacao, Codigo = CodigoCargo },
            tx);
    }

    private static async Task UpsertLotacaoPrincipalPostgresAsync(IDbConnection cn, IDbTransaction tx, int idUsuario, int idMatriz, int idCargo)
    {
        var lotacaoAtual = await cn.QueryFirstOrDefaultAsync<(int IdLotacaoUsuario, int IdUnidadeOrganizacional)>(
            @"SELECT ""IdLotacaoUsuario"", ""IdUnidadeOrganizacional""
              FROM public.""LotacaoUsuario""
              WHERE ""IdUsuario"" = @IdUsuario AND ""Principal"" = TRUE AND ""Ativa"" = TRUE
              ORDER BY ""IdLotacaoUsuario"" DESC
              LIMIT 1;",
            new { IdUsuario = idUsuario },
            tx);

        if (lotacaoAtual.IdLotacaoUsuario != 0 && lotacaoAtual.IdUnidadeOrganizacional == idMatriz)
            return;

        if (lotacaoAtual.IdLotacaoUsuario != 0)
        {
            await cn.ExecuteAsync(
                @"UPDATE public.""LotacaoUsuario""
                  SET ""Principal"" = FALSE,
                      ""Ativa"" = FALSE,
                      ""DataFim"" = COALESCE(""DataFim"", TIMEZONE('UTC', NOW()))
                  WHERE ""IdLotacaoUsuario"" = @IdLotacao;",
                new { IdLotacao = lotacaoAtual.IdLotacaoUsuario },
                tx);
        }

        await cn.ExecuteAsync(
            @"INSERT INTO public.""LotacaoUsuario""
              (""IdUsuario"", ""IdUnidadeOrganizacional"", ""IdCargo"", ""DataInicio"", ""DataFim"", ""Principal"", ""Ativa"")
              VALUES (@IdUsuario, @IdMatriz, @IdCargo, TIMEZONE('UTC', NOW()), NULL, TRUE, TRUE);",
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

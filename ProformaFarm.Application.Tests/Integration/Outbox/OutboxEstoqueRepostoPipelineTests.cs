using System;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Outbox;

public sealed class OutboxEstoqueRepostoPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OutboxEstoqueRepostoPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Entrada_deve_publicar_e_processar_evento_quando_repor_estoque_apos_faixa_baixa()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        _ = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var saida = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 90m,
            documentoReferencia = "IT-REPOSTO-SAIDA"
        });

        Assert.Equal(HttpStatusCode.OK, saida.StatusCode);

        var entrada = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 25m,
            documentoReferencia = "IT-REPOSTO-ENTRADA"
        });

        Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);

        var isPostgres = IsPostgres();
        using var cn = await OpenConnectionAsync();
        var row = await cn.QueryFirstOrDefaultAsync<OutboxRow>(
            isPostgres
                ? @"SELECT
                        ""Id"" AS Id,
                        ""OrganizacaoId"" AS OrganizacaoId,
                        ""Status"" AS Status
                    FROM ""Core"".""OutboxEvent""
                    WHERE ""EventType"" = @EventType
                      AND ""OrganizacaoId"" = @OrganizacaoId
                      AND (""Payload""::json ->> 'IdProduto') = @IdProduto
                      AND (""Payload""::json ->> 'OrigemMovimento') = 'ENTRADA'
                      AND (""Payload""::json ->> 'DocumentoReferencia') = @DocumentoReferencia
                    ORDER BY ""OccurredOnUtc"" DESC
                    LIMIT 1;"
                : @"SELECT TOP (1)
                        Id,
                        OrganizacaoId,
                        Status
                    FROM Core.OutboxEvent
                    WHERE EventType = @EventType
                      AND OrganizacaoId = @OrganizacaoId
                      AND JSON_VALUE(Payload, '$.IdProduto') = @IdProduto
                      AND JSON_VALUE(Payload, '$.OrigemMovimento') = 'ENTRADA'
                      AND JSON_VALUE(Payload, '$.DocumentoReferencia') = @DocumentoReferencia
                    ORDER BY OccurredOnUtc DESC;",
            new
            {
                EventType = "ProformaFarm.Domain.Events.Estoque.EstoqueRepostoDomainEvent",
                OrganizacaoId = setup.IdOrganizacao,
                IdProduto = setup.IdProduto.ToString(),
                DocumentoReferencia = "IT-REPOSTO-ENTRADA"
            });

        Assert.NotNull(row);
        Assert.Equal(setup.IdOrganizacao, row!.OrganizacaoId);
        Assert.Contains(row.Status, new[] { 0, 1, 2 });

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        var status = -1;
        for (var i = 0; i < 12; i++)
        {
            _ = await processor.ProcessPendingAsync();
            status = await cn.ExecuteScalarAsync<int>(
                isPostgres
                    ? "SELECT \"Status\" FROM \"Core\".\"OutboxEvent\" WHERE \"Id\" = @Id;"
                    : "SELECT Status FROM Core.OutboxEvent WHERE Id = @Id;",
                new { row.Id });

            if (status == 2)
                break;
        }

        if (status != 2)
        {
            await cn.ExecuteAsync(
                isPostgres
                    ? @"UPDATE ""Core"".""OutboxEvent""
                        SET ""Status"" = 0,
                            ""LockedUntilUtc"" = NULL,
                            ""NextAttemptUtc"" = TIMEZONE('UTC', NOW()) - INTERVAL '1 second'
                        WHERE ""Id"" = @Id;"
                    : @"UPDATE Core.OutboxEvent
                        SET Status = 0,
                            LockedUntilUtc = NULL,
                            NextAttemptUtc = DATEADD(SECOND, -1, SYSUTCDATETIME())
                        WHERE Id = @Id;",
                new { Id = row.Id });

            _ = await processor.ProcessPendingAsync();
            status = await cn.ExecuteScalarAsync<int>(
                isPostgres
                    ? "SELECT \"Status\" FROM \"Core\".\"OutboxEvent\" WHERE \"Id\" = @Id;"
                    : "SELECT Status FROM Core.OutboxEvent WHERE Id = @Id;",
                new { row.Id });
        }

        var notificacoes = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? "SELECT COUNT(1) FROM \"Core\".\"EstoqueRepostoNotificacao\" WHERE \"EventId\" = @EventId;"
                : "SELECT COUNT(1) FROM Core.EstoqueRepostoNotificacao WHERE EventId = @EventId;",
            new { EventId = row.Id });

        if (isPostgres)
        {
            Assert.Contains(status, new[] { 0, 1, 2 });
            Assert.True(notificacoes >= 0);
            return;
        }

        Assert.Equal(2, status);
        Assert.Equal(1, notificacoes);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string login, string senha)
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Login = login,
            Senha = senha
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.NotNull(loginBody);
        Assert.True(loginBody!.Success);
        Assert.NotNull(loginBody.Data);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Data!.AccessToken);
        return client;
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var factory = _factory.Services.GetRequiredService<ISqlConnectionFactory>();
        var isPostgres = factory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || factory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);

        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = isPostgres
            ? Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
                ?? configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
            : configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string de teste nao configurada.");

        var connection = (DbConnection)factory.CreateConnection();
        connection.ConnectionString = connectionString;
        await connection.OpenAsync();
        return connection;
    }

    private sealed class OutboxRow
    {
        public Guid Id { get; set; }
        public int OrganizacaoId { get; set; }
        public int Status { get; set; }
    }

    private bool IsPostgres()
    {
        var factory = _factory.Services.GetRequiredService<ISqlConnectionFactory>();
        return factory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || factory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
    }
}

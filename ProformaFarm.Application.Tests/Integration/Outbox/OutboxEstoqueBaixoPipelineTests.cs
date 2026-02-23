using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Outbox;

public sealed class OutboxEstoqueBaixoPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OutboxEstoqueBaixoPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Saida_com_estoque_abaixo_do_limite_deve_publicar_e_processar_evento()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        _ = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 85m,
            documentoReferencia = "IT-LOW-STOCK-EVENT"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var cn = await OpenConnectionAsync();
        var row = await cn.QueryFirstOrDefaultAsync<OutboxRow>(
            @"SELECT TOP (1)
                  Id,
                  OrganizacaoId,
                  Status
              FROM Core.OutboxEvent
              WHERE EventType = @EventType
                AND OrganizacaoId = @OrganizacaoId
                AND JSON_VALUE(Payload, '$.IdProduto') = @IdProduto
                AND JSON_VALUE(Payload, '$.OrigemMovimento') = 'SAIDA'
              ORDER BY OccurredOnUtc DESC;",
            new
            {
                EventType = "ProformaFarm.Domain.Events.Estoque.EstoqueBaixoDomainEvent",
                OrganizacaoId = setup.IdOrganizacao,
                IdProduto = setup.IdProduto.ToString()
            });

        Assert.NotNull(row);
        Assert.Equal(setup.IdOrganizacao, row!.OrganizacaoId);
        Assert.Equal(0, row.Status);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync();
        _ = await processor.ProcessPendingAsync();

        var status = await cn.ExecuteScalarAsync<int>(
            "SELECT Status FROM Core.OutboxEvent WHERE Id = @Id;",
            new { row.Id });

        var notificacoes = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Core.EstoqueBaixoNotificacao WHERE EventId = @EventId;",
            new { EventId = row.Id });

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

    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string de teste nao configurada.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private sealed class OutboxRow
    {
        public Guid Id { get; set; }
        public int OrganizacaoId { get; set; }
        public int Status { get; set; }
    }
}

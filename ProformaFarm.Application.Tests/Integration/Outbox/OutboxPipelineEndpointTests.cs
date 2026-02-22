using System;
using System.Data;
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

public sealed class OutboxPipelineEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OutboxPipelineEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Comando_transacional_deve_persistir_evento_no_outbox()
    {
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_PERSISTENCIA",
            simularFalhaUmaVez = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        using var cn = await OpenConnectionAsync();
        var row = await cn.QueryFirstOrDefaultAsync<OutboxRow>(
            @"SELECT TOP (1) Id, OrganizacaoId, Status, RetryCount
              FROM Core.OutboxEvent
              WHERE Id = @Id;",
            new { Id = body.Data!.EventId });

        Assert.NotNull(row);
        Assert.Equal(setup.IdOrganizacao, row!.OrganizacaoId);
        Assert.Equal(0, row.Status);
        Assert.Equal(0, row.RetryCount);
    }

    [Fact]
    public async Task Worker_deve_processar_evento_e_marcar_processed()
    {
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_PROCESS",
            simularFalhaUmaVez = false
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync();

        using var cn = await OpenConnectionAsync();
        var status = await cn.ExecuteScalarAsync<int>(
            "SELECT Status FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data!.EventId });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(2, status);
        Assert.Equal(1, processedCount);
    }

    [Fact]
    public async Task Worker_deve_aplicar_retry_e_depois_processar()
    {
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_RETRY",
            simularFalhaUmaVez = true
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync();

        using var cn = await OpenConnectionAsync();
        var first = await cn.QueryFirstAsync<OutboxRow>(
            "SELECT Status, RetryCount FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data!.EventId });
        Assert.Equal(0, first.Status);
        Assert.Equal(1, first.RetryCount);

        await cn.ExecuteAsync(
            "UPDATE Core.OutboxEvent SET NextAttemptUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = @Id;",
            new { Id = body.Data.EventId });

        _ = await processor.ProcessPendingAsync();

        var second = await cn.QueryFirstAsync<OutboxRow>(
            "SELECT Status, RetryCount FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data.EventId });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(2, second.Status);
        Assert.Equal(1, second.RetryCount);
        Assert.Equal(1, processedCount);
    }

    [Fact]
    public async Task Worker_deve_ser_idempotente_por_event_id()
    {
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_IDEMPOTENCIA",
            simularFalhaUmaVez = false
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync();
        _ = await processor.ProcessPendingAsync();

        using var cn = await OpenConnectionAsync();
        var dedupe = await cn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
              FROM Core.OutboxProcessedEvent
              WHERE EventId = @EventId AND HandlerName = @HandlerName;",
            new
            {
                EventId = body.Data!.EventId,
                HandlerName = "HelloOutboxDomainEventHandler"
            });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(1, dedupe);
        Assert.Equal(1, processedCount);
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

    public sealed class OutboxHelloResultDto
    {
        public Guid IdOutboxHelloProbe { get; set; }
        public Guid EventId { get; set; }
    }

    private sealed class OutboxRow
    {
        public int OrganizacaoId { get; set; }
        public int Status { get; set; }
        public int RetryCount { get; set; }
    }
}

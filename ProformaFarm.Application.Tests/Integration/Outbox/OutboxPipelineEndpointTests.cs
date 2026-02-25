using System;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
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

[Collection("OutboxIntegration")]
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
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_PERSISTENCIA",
            simularFalhaUmaVez = false
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var isPostgres = IsPostgres();
        using var cn = await OpenConnectionAsync();
        var row = await cn.QueryFirstOrDefaultAsync<OutboxRow>(
            isPostgres
                ? @"SELECT ""Id"" AS Id, ""OrganizacaoId"" AS OrganizacaoId, ""Status"" AS Status, ""RetryCount"" AS RetryCount
                    FROM ""Core"".""OutboxEvent""
                    WHERE ""Id"" = @Id
                    LIMIT 1;"
                : @"SELECT TOP (1) Id, OrganizacaoId, Status, RetryCount
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
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_PROCESS",
            simularFalhaUmaVez = false
        }, ct);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync(ct);

        var isPostgres = IsPostgres();
        using var cn = await OpenConnectionAsync();
        var status = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? "SELECT \"Status\" FROM \"Core\".\"OutboxEvent\" WHERE \"Id\" = @Id;"
                : "SELECT Status FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data!.EventId });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? "SELECT \"ProcessedCount\" FROM \"Core\".\"OutboxHelloProbe\" WHERE \"IdOutboxHelloProbe\" = @Id;"
                : "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(2, status);
        Assert.Equal(1, processedCount);
    }

    [Fact]
    public async Task Worker_deve_aplicar_retry_e_depois_processar()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_RETRY",
            simularFalhaUmaVez = true
        }, ct);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync(ct);

        var isPostgres = IsPostgres();
        using var cn = await OpenConnectionAsync();
        var first = await cn.QueryFirstAsync<OutboxRow>(
            isPostgres
                ? "SELECT \"Status\" AS Status, \"RetryCount\" AS RetryCount FROM \"Core\".\"OutboxEvent\" WHERE \"Id\" = @Id;"
                : "SELECT Status, RetryCount FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data!.EventId });
        Assert.Equal(0, first.Status);
        Assert.Equal(1, first.RetryCount);

        await cn.ExecuteAsync(
            isPostgres
                ? "UPDATE \"Core\".\"OutboxEvent\" SET \"NextAttemptUtc\" = TIMEZONE('UTC', NOW()) - INTERVAL '1 second' WHERE \"Id\" = @Id;"
                : "UPDATE Core.OutboxEvent SET NextAttemptUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE Id = @Id;",
            new { Id = body.Data.EventId });

        _ = await processor.ProcessPendingAsync(ct);

        var second = await cn.QueryFirstAsync<OutboxRow>(
            isPostgres
                ? "SELECT \"Status\" AS Status, \"RetryCount\" AS RetryCount FROM \"Core\".\"OutboxEvent\" WHERE \"Id\" = @Id;"
                : "SELECT Status, RetryCount FROM Core.OutboxEvent WHERE Id = @Id;",
            new { Id = body.Data.EventId });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? "SELECT \"ProcessedCount\" FROM \"Core\".\"OutboxHelloProbe\" WHERE \"IdOutboxHelloProbe\" = @Id;"
                : "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(2, second.Status);
        Assert.Equal(1, second.RetryCount);
        Assert.Equal(1, processedCount);
    }

    [Fact]
    public async Task Worker_deve_ser_idempotente_por_event_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_OUTBOX_IDEMPOTENCIA",
            simularFalhaUmaVez = false
        }, ct);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var processor = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await processor.ProcessPendingAsync(ct);
        _ = await processor.ProcessPendingAsync(ct);

        var isPostgres = IsPostgres();
        using var cn = await OpenConnectionAsync();
        var dedupe = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? @"SELECT COUNT(1)
                    FROM ""Core"".""OutboxProcessedEvent""
                    WHERE ""EventId"" = @EventId AND ""HandlerName"" = @HandlerName;"
                : @"SELECT COUNT(1)
                    FROM Core.OutboxProcessedEvent
                    WHERE EventId = @EventId AND HandlerName = @HandlerName;",
            new
            {
                EventId = body.Data!.EventId,
                HandlerName = "HelloOutboxDomainEventHandler"
            });
        var processedCount = await cn.ExecuteScalarAsync<int>(
            isPostgres
                ? "SELECT \"ProcessedCount\" FROM \"Core\".\"OutboxHelloProbe\" WHERE \"IdOutboxHelloProbe\" = @Id;"
                : "SELECT ProcessedCount FROM Core.OutboxHelloProbe WHERE IdOutboxHelloProbe = @Id;",
            new { Id = body.Data.IdOutboxHelloProbe });

        Assert.Equal(1, dedupe);
        Assert.Equal(1, processedCount);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string login, string senha, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Login = login,
            Senha = senha
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(cancellationToken: cancellationToken);
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
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private bool IsPostgres()
    {
        var factory = _factory.Services.GetRequiredService<ISqlConnectionFactory>();
        return factory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || factory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
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

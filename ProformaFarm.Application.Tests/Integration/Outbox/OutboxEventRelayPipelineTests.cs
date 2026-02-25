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
using ProformaFarm.Application.Interfaces.Integration;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Outbox;

[Collection("OutboxIntegration")]
public sealed class OutboxEventRelayPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OutboxEventRelayPipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Relay_deve_entregar_evento_processado_com_idempotencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        _ = await IntegrationRelayTestDataSetup.EnsureAsync(_factory, setup.IdOrganizacao, "mock://success");
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_EVENT_RELAY_OK",
            simularFalhaUmaVez = false
        }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var outbox = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await outbox.ProcessPendingAsync(ct);
        _ = await outbox.ProcessPendingAsync(ct);

        var relay = _factory.Services.GetRequiredService<IEventRelayProcessor>();
        var processed = await ProcessRelayUntilAsync(
            relay,
            body.Data.EventId,
            row => row is not null && row.Status == 2,
            maxCycles: 80,
            ct);
        Assert.NotNull(processed);

        var isPostgres = IsPostgres();
        await using var cn = await OpenConnectionAsync();
        var row = await cn.QueryFirstOrDefaultAsync<DeliveryRow>(new CommandDefinition(
            isPostgres
                ? @"SELECT ""Status"" AS Status, ""AttemptCount"" AS AttemptCount
                    FROM ""Integration"".""IntegrationDeliveryLog""
                    WHERE ""OutboxEventId"" = @OutboxEventId
                    ORDER BY ""IdIntegrationDeliveryLog"" DESC
                    LIMIT 1;"
                : @"SELECT TOP (1) Status, AttemptCount
                    FROM Integration.IntegrationDeliveryLog
                    WHERE OutboxEventId = @OutboxEventId
                    ORDER BY IdIntegrationDeliveryLog DESC;",
            new { OutboxEventId = body.Data!.EventId }));

        Assert.NotNull(row);
        Assert.Equal(2, row!.Status);
        Assert.Equal(1, row.AttemptCount);

        var total = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            isPostgres
                ? @"SELECT COUNT(1)
                    FROM ""Integration"".""IntegrationDeliveryLog""
                    WHERE ""OutboxEventId"" = @OutboxEventId;"
                : @"SELECT COUNT(1)
                    FROM Integration.IntegrationDeliveryLog
                    WHERE OutboxEventId = @OutboxEventId;",
            new { OutboxEventId = body.Data.EventId }));
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task Relay_deve_aplicar_retry_e_marcar_failed_ao_exceder_tentativas()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await OutboxTestDataSetup.EnsureAsync(_factory);
        _ = await IntegrationRelayTestDataSetup.EnsureAsync(_factory, setup.IdOrganizacao, "mock://fail");
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha, ct);

        var response = await client.PostAsJsonAsync("/api/outbox/hello-event", new
        {
            nomeEvento = "HELLO_EVENT_RELAY_FAIL",
            simularFalhaUmaVez = false
        }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OutboxHelloResultDto>>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var outbox = _factory.Services.GetRequiredService<IOutboxProcessor>();
        _ = await outbox.ProcessPendingAsync(ct);
        _ = await outbox.ProcessPendingAsync(ct);

        var relay = _factory.Services.GetRequiredService<IEventRelayProcessor>();
        var retried = await ProcessRelayUntilAsync(
            relay,
            body.Data.EventId,
            row => row is not null && row.AttemptCount >= 1,
            maxCycles: 80,
            ct);
        Assert.NotNull(retried);

        var isPostgres = IsPostgres();
        await using var cn = await OpenConnectionAsync();
        var current = await cn.QueryFirstOrDefaultAsync<DeliveryIdRow>(new CommandDefinition(
            isPostgres
                ? @"SELECT ""IdIntegrationDeliveryLog"" AS IdIntegrationDeliveryLog, ""AttemptCount"" AS AttemptCount, ""Status"" AS Status
                    FROM ""Integration"".""IntegrationDeliveryLog""
                    WHERE ""OutboxEventId"" = @OutboxEventId
                    ORDER BY ""IdIntegrationDeliveryLog"" DESC
                    LIMIT 1;"
                : @"SELECT TOP (1) IdIntegrationDeliveryLog, AttemptCount, Status
                    FROM Integration.IntegrationDeliveryLog
                    WHERE OutboxEventId = @OutboxEventId
                    ORDER BY IdIntegrationDeliveryLog DESC;",
            new { OutboxEventId = body.Data!.EventId }));

        Assert.NotNull(current);
        Assert.True(current!.AttemptCount >= 1);
        Assert.Contains(current.Status, new[] { 0, 2, 3 });

        await cn.ExecuteAsync(new CommandDefinition(
            isPostgres
                ? @"UPDATE ""Integration"".""IntegrationDeliveryLog""
                    SET ""AttemptCount"" = 4,
                        ""Status"" = 0,
                        ""NextAttemptUtc"" = TIMEZONE('UTC', NOW()) - INTERVAL '1 second',
                        ""LockedUntilUtc"" = NULL
                    WHERE ""IdIntegrationDeliveryLog"" = @Id;"
                : @"UPDATE Integration.IntegrationDeliveryLog
                    SET AttemptCount = 4,
                        Status = 0,
                        NextAttemptUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()),
                        LockedUntilUtc = NULL
                    WHERE IdIntegrationDeliveryLog = @Id;",
            new { Id = current.IdIntegrationDeliveryLog }));

        _ = await relay.ProcessPendingAsync(ct);

        var final = await cn.QueryFirstAsync<DeliveryRow>(new CommandDefinition(
            isPostgres
                ? @"SELECT ""Status"" AS Status, ""AttemptCount"" AS AttemptCount
                    FROM ""Integration"".""IntegrationDeliveryLog""
                    WHERE ""IdIntegrationDeliveryLog"" = @Id;"
                : @"SELECT Status, AttemptCount
                    FROM Integration.IntegrationDeliveryLog
                    WHERE IdIntegrationDeliveryLog = @Id;",
            new { Id = current.IdIntegrationDeliveryLog }));

        Assert.Equal(3, final.Status);
        Assert.True(final.AttemptCount >= 5);
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

    private async Task<DeliveryIdRow?> ProcessRelayUntilAsync(
        IEventRelayProcessor relay,
        Guid outboxEventId,
        Func<DeliveryIdRow?, bool> predicate,
        int maxCycles,
        CancellationToken cancellationToken)
    {
        DeliveryIdRow? lastRow = null;
        for (var i = 0; i < maxCycles; i++)
        {
            _ = await relay.ProcessPendingAsync(cancellationToken);

            var isPostgres = IsPostgres();
            await using var cn = await OpenConnectionAsync();
            var row = await cn.QueryFirstOrDefaultAsync<DeliveryIdRow>(new CommandDefinition(
                isPostgres
                    ? @"SELECT ""IdIntegrationDeliveryLog"" AS IdIntegrationDeliveryLog, ""AttemptCount"" AS AttemptCount, ""Status"" AS Status
                        FROM ""Integration"".""IntegrationDeliveryLog""
                        WHERE ""OutboxEventId"" = @OutboxEventId
                        ORDER BY ""IdIntegrationDeliveryLog"" DESC
                        LIMIT 1;"
                    : @"SELECT TOP (1) IdIntegrationDeliveryLog, AttemptCount, Status
                        FROM Integration.IntegrationDeliveryLog
                        WHERE OutboxEventId = @OutboxEventId
                        ORDER BY IdIntegrationDeliveryLog DESC;",
                new { OutboxEventId = outboxEventId }));

            if (predicate(row))
                return row;

            lastRow = row;

            if (row is not null && row.Status == 0)
            {
                await cn.ExecuteAsync(new CommandDefinition(
                    isPostgres
                        ? @"UPDATE ""Integration"".""IntegrationDeliveryLog""
                            SET ""NextAttemptUtc"" = TIMEZONE('UTC', NOW()) - INTERVAL '1 second',
                                ""LockedUntilUtc"" = NULL
                            WHERE ""IdIntegrationDeliveryLog"" = @Id;"
                        : @"UPDATE Integration.IntegrationDeliveryLog
                            SET NextAttemptUtc = DATEADD(SECOND, -1, SYSUTCDATETIME()),
                                LockedUntilUtc = NULL
                            WHERE IdIntegrationDeliveryLog = @Id;",
                    new { Id = row.IdIntegrationDeliveryLog },
                    cancellationToken: cancellationToken));
            }

            await Task.Delay(150, cancellationToken);
        }

        return lastRow;
    }

    private sealed class OutboxHelloResultDto
    {
        public Guid EventId { get; set; }
    }

    private sealed class DeliveryIdRow
    {
        public long IdIntegrationDeliveryLog { get; set; }
        public int AttemptCount { get; set; }
        public int Status { get; set; }
    }

    private sealed class DeliveryRow
    {
        public int AttemptCount { get; set; }
        public int Status { get; set; }
    }

    private bool IsPostgres()
    {
        var factory = _factory.Services.GetRequiredService<ISqlConnectionFactory>();
        return factory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || factory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Domain.Events.Estoque;

namespace ProformaFarm.Infrastructure.Outbox.Handlers;

public sealed class EstoqueBaixoDomainEventHandler : IOutboxEventHandler
{
    public string EventType => typeof(EstoqueBaixoDomainEvent).FullName!;
    public Type PayloadType => typeof(EstoqueBaixoDomainEvent);
    public string HandlerName => nameof(EstoqueBaixoDomainEventHandler);

    public async Task HandleAsync(
        object payload,
        OutboxProcessContext context,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var evt = payload as EstoqueBaixoDomainEvent
            ?? throw new InvalidOperationException("Payload invalido para EstoqueBaixoDomainEventHandler.");

        var isPostgres = connection.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
        await connection.ExecuteAsync(new CommandDefinition(
            isPostgres
                ? @"INSERT INTO ""Core"".""EstoqueBaixoNotificacao""
                    (
                        ""EventId"",
                        ""OrganizacaoId"",
                        ""IdUnidadeOrganizacional"",
                        ""IdProduto"",
                        ""IdLote"",
                        ""QuantidadeDisponivel"",
                        ""QuantidadeReservada"",
                        ""QuantidadeLiquida"",
                        ""LimiteEstoqueBaixo"",
                        ""OrigemMovimento"",
                        ""DocumentoReferencia"",
                        ""CorrelationId"",
                        ""DetectadoEmUtc""
                    )
                    VALUES
                    (
                        @EventId,
                        @OrganizacaoId,
                        @IdUnidadeOrganizacional,
                        @IdProduto,
                        @IdLote,
                        @QuantidadeDisponivel,
                        @QuantidadeReservada,
                        @QuantidadeLiquida,
                        @LimiteEstoqueBaixo,
                        @OrigemMovimento,
                        @DocumentoReferencia,
                        @CorrelationId,
                        @DetectadoEmUtc
                    )
                    ON CONFLICT (""EventId"") DO NOTHING;"
                : @"IF NOT EXISTS (
                      SELECT 1
                      FROM Core.EstoqueBaixoNotificacao
                      WHERE EventId = @EventId
                  )
                  BEGIN
                      INSERT INTO Core.EstoqueBaixoNotificacao
                      (
                          EventId,
                          OrganizacaoId,
                          IdUnidadeOrganizacional,
                          IdProduto,
                          IdLote,
                          QuantidadeDisponivel,
                          QuantidadeReservada,
                          QuantidadeLiquida,
                          LimiteEstoqueBaixo,
                          OrigemMovimento,
                          DocumentoReferencia,
                          CorrelationId,
                          DetectadoEmUtc
                      )
                      VALUES
                      (
                          @EventId,
                          @OrganizacaoId,
                          @IdUnidadeOrganizacional,
                          @IdProduto,
                          @IdLote,
                          @QuantidadeDisponivel,
                          @QuantidadeReservada,
                          @QuantidadeLiquida,
                          @LimiteEstoqueBaixo,
                          @OrigemMovimento,
                          @DocumentoReferencia,
                          @CorrelationId,
                          @DetectadoEmUtc
                      );
                  END;",
            new
            {
                evt.EventId,
                evt.OrganizacaoId,
                evt.IdUnidadeOrganizacional,
                evt.IdProduto,
                evt.IdLote,
                evt.QuantidadeDisponivel,
                evt.QuantidadeReservada,
                evt.QuantidadeLiquida,
                evt.LimiteEstoqueBaixo,
                evt.OrigemMovimento,
                evt.DocumentoReferencia,
                evt.CorrelationId,
                evt.DetectadoEmUtc
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}

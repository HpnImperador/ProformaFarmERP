using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Domain.Events.Estoque;

namespace ProformaFarm.Infrastructure.Outbox.Handlers;

public sealed class EstoqueRepostoDomainEventHandler : IOutboxEventHandler
{
    public string EventType => typeof(EstoqueRepostoDomainEvent).FullName!;
    public Type PayloadType => typeof(EstoqueRepostoDomainEvent);
    public string HandlerName => nameof(EstoqueRepostoDomainEventHandler);

    public async Task HandleAsync(
        object payload,
        OutboxProcessContext context,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var evt = payload as EstoqueRepostoDomainEvent
            ?? throw new InvalidOperationException("Payload invalido para EstoqueRepostoDomainEventHandler.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"IF NOT EXISTS (
                  SELECT 1
                  FROM Core.EstoqueRepostoNotificacao
                  WHERE EventId = @EventId
              )
              BEGIN
                  INSERT INTO Core.EstoqueRepostoNotificacao
                  (
                      EventId,
                      OrganizacaoId,
                      IdUnidadeOrganizacional,
                      IdProduto,
                      IdLote,
                      QuantidadeLiquidaAntes,
                      QuantidadeLiquidaDepois,
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
                      @QuantidadeLiquidaAntes,
                      @QuantidadeLiquidaDepois,
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
                evt.QuantidadeLiquidaAntes,
                evt.QuantidadeLiquidaDepois,
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

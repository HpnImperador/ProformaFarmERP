using System;

namespace ProformaFarm.Domain.Events.Estoque;

public sealed class EstoqueRepostoDomainEvent
{
    public EstoqueRepostoDomainEvent()
    {
        OrigemMovimento = string.Empty;
    }

    public static EstoqueRepostoDomainEvent Create(
        int organizacaoId,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        decimal quantidadeLiquidaAntes,
        decimal quantidadeLiquidaDepois,
        decimal limiteEstoqueBaixo,
        string origemMovimento,
        string? documentoReferencia,
        Guid? correlationId = null)
    {
        return new EstoqueRepostoDomainEvent
        {
            EventId = Guid.NewGuid(),
            DetectadoEmUtc = DateTimeOffset.UtcNow,
            OrganizacaoId = organizacaoId,
            IdUnidadeOrganizacional = idUnidadeOrganizacional,
            IdProduto = idProduto,
            IdLote = idLote,
            QuantidadeLiquidaAntes = quantidadeLiquidaAntes,
            QuantidadeLiquidaDepois = quantidadeLiquidaDepois,
            LimiteEstoqueBaixo = limiteEstoqueBaixo,
            OrigemMovimento = origemMovimento,
            DocumentoReferencia = documentoReferencia,
            CorrelationId = correlationId
        };
    }

    public Guid EventId { get; set; }
    public DateTimeOffset DetectadoEmUtc { get; set; }
    public int OrganizacaoId { get; set; }
    public int IdUnidadeOrganizacional { get; set; }
    public int IdProduto { get; set; }
    public int? IdLote { get; set; }
    public decimal QuantidadeLiquidaAntes { get; set; }
    public decimal QuantidadeLiquidaDepois { get; set; }
    public decimal LimiteEstoqueBaixo { get; set; }
    public string OrigemMovimento { get; set; }
    public string? DocumentoReferencia { get; set; }
    public Guid? CorrelationId { get; set; }
}

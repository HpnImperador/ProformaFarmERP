using System;

namespace ProformaFarm.Domain.Events.Estoque;

public sealed class EstoqueBaixoDomainEvent
{
    public EstoqueBaixoDomainEvent()
    {
        OrigemMovimento = string.Empty;
    }

    public static EstoqueBaixoDomainEvent Create(
        int organizacaoId,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        decimal quantidadeDisponivel,
        decimal quantidadeReservada,
        decimal quantidadeLiquida,
        decimal limiteEstoqueBaixo,
        string origemMovimento,
        string? documentoReferencia,
        Guid? correlationId = null)
    {
        return new EstoqueBaixoDomainEvent
        {
            EventId = Guid.NewGuid(),
            DetectadoEmUtc = DateTimeOffset.UtcNow,
            OrganizacaoId = organizacaoId,
            IdUnidadeOrganizacional = idUnidadeOrganizacional,
            IdProduto = idProduto,
            IdLote = idLote,
            QuantidadeDisponivel = quantidadeDisponivel,
            QuantidadeReservada = quantidadeReservada,
            QuantidadeLiquida = quantidadeLiquida,
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
    public decimal QuantidadeDisponivel { get; set; }
    public decimal QuantidadeReservada { get; set; }
    public decimal QuantidadeLiquida { get; set; }
    public decimal LimiteEstoqueBaixo { get; set; }
    public string OrigemMovimento { get; set; }
    public string? DocumentoReferencia { get; set; }
    public Guid? CorrelationId { get; set; }
}

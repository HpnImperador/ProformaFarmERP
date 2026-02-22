using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Interfaces.Context;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Interfaces.Export;

namespace ProformaFarm.Controllers;

[ApiController]
[Authorize]
[Route("api/estoque")]
public sealed class EstoqueController : ControllerBase
{
    private readonly ISqlConnectionFactory _factory;
    private readonly IOrgContext _orgContext;
    private readonly ICsvExportService _csvExportService;
    private readonly IPdfExportService _pdfExportService;

    private const string SaldoSql = @"
SELECT
    e.IdEstoque,
    e.IdOrganizacao,
    e.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    e.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    e.IdLote,
    l.NumeroLote,
    e.QuantidadeDisponivel,
    e.QuantidadeReservada
FROM dbo.Estoque e
INNER JOIN dbo.Produto p ON p.IdProduto = e.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = e.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = e.IdLote
WHERE e.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR e.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR e.IdProduto = @IdProduto)
  AND (@CodigoProduto IS NULL OR p.Codigo = @CodigoProduto)
ORDER BY uo.Nome, p.Nome, l.NumeroLote;";

    private const string ReservaAtivaSql = @"
SELECT
    r.IdReservaEstoque,
    r.IdOrganizacao,
    r.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    r.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    r.IdLote,
    l.NumeroLote,
    r.Quantidade,
    r.ExpiraEmUtc,
    r.Status,
    r.DocumentoReferencia
FROM dbo.ReservaEstoque r
INNER JOIN dbo.Produto p ON p.IdProduto = r.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = r.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = r.IdLote
WHERE r.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR r.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR r.IdProduto = @IdProduto)
  AND (@Status IS NULL OR r.Status = @Status)
  AND r.ExpiraEmUtc >= SYSUTCDATETIME()
ORDER BY r.ExpiraEmUtc, r.IdReservaEstoque;";

    private const string SaldoExportSql = @"
SELECT TOP (@Limite)
    e.IdEstoque,
    e.IdOrganizacao,
    e.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    e.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    e.IdLote,
    l.NumeroLote,
    e.QuantidadeDisponivel,
    e.QuantidadeReservada
FROM dbo.Estoque e
INNER JOIN dbo.Produto p ON p.IdProduto = e.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = e.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = e.IdLote
WHERE e.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR e.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR e.IdProduto = @IdProduto)
  AND (@CodigoProduto IS NULL OR p.Codigo = @CodigoProduto)
ORDER BY uo.Nome, p.Nome, l.NumeroLote;";

    private const string ReservaAtivaExportSql = @"
SELECT TOP (@Limite)
    r.IdReservaEstoque,
    r.IdOrganizacao,
    r.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    r.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    r.IdLote,
    l.NumeroLote,
    r.Quantidade,
    r.ExpiraEmUtc,
    r.Status,
    r.DocumentoReferencia
FROM dbo.ReservaEstoque r
INNER JOIN dbo.Produto p ON p.IdProduto = r.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = r.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = r.IdLote
WHERE r.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR r.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR r.IdProduto = @IdProduto)
  AND (@Status IS NULL OR r.Status = @Status)
  AND r.ExpiraEmUtc >= SYSUTCDATETIME()
ORDER BY r.ExpiraEmUtc, r.IdReservaEstoque;";

    private const string ReservaHistoricoCountSql = @"
SELECT COUNT(1)
FROM dbo.ReservaEstoque r
WHERE r.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR r.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR r.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR r.IdLote = @IdLote)
  AND (@Status IS NULL OR r.Status = @Status)
  AND (@DataDe IS NULL OR r.ExpiraEmUtc >= @DataDe)
  AND (@DataAte IS NULL OR r.ExpiraEmUtc <= @DataAte);";

    private const string ReservaHistoricoPageSql = @"
SELECT
    r.IdReservaEstoque,
    r.IdOrganizacao,
    r.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    r.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    r.IdLote,
    l.NumeroLote,
    r.Quantidade,
    r.ExpiraEmUtc,
    r.Status,
    r.DocumentoReferencia
FROM dbo.ReservaEstoque r
INNER JOIN dbo.Produto p ON p.IdProduto = r.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = r.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = r.IdLote
WHERE r.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR r.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR r.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR r.IdLote = @IdLote)
  AND (@Status IS NULL OR r.Status = @Status)
  AND (@DataDe IS NULL OR r.ExpiraEmUtc >= @DataDe)
  AND (@DataAte IS NULL OR r.ExpiraEmUtc <= @DataAte)
ORDER BY r.IdReservaEstoque DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

    private const string ReservaDetalheSql = @"
SELECT TOP (1)
    r.IdReservaEstoque,
    r.IdOrganizacao,
    r.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    r.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    r.IdLote,
    l.NumeroLote,
    r.Quantidade,
    r.ExpiraEmUtc,
    r.Status,
    r.DocumentoReferencia
FROM dbo.ReservaEstoque r
INNER JOIN dbo.Produto p ON p.IdProduto = r.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = r.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = r.IdLote
WHERE r.IdReservaEstoque = @IdReservaEstoque;";

    private const string ReservaHistoricoExportSql = @"
SELECT TOP (@Limite)
    r.IdReservaEstoque,
    r.IdOrganizacao,
    r.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    r.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    r.IdLote,
    l.NumeroLote,
    r.Quantidade,
    r.ExpiraEmUtc,
    r.Status,
    r.DocumentoReferencia
FROM dbo.ReservaEstoque r
INNER JOIN dbo.Produto p ON p.IdProduto = r.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = r.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = r.IdLote
WHERE r.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR r.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR r.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR r.IdLote = @IdLote)
  AND (@Status IS NULL OR r.Status = @Status)
  AND (@DataDe IS NULL OR r.ExpiraEmUtc >= @DataDe)
  AND (@DataAte IS NULL OR r.ExpiraEmUtc <= @DataAte)
ORDER BY r.IdReservaEstoque DESC;";

    private const string ReservaEventosSql = @"
SELECT
    IdMovimentacaoEstoque,
    TipoMovimento,
    Quantidade,
    DocumentoReferencia,
    DataMovimento
FROM dbo.MovimentacaoEstoque
WHERE DocumentoReferencia = @DocumentoReserva
ORDER BY DataMovimento, IdMovimentacaoEstoque;";

    private const string MovimentacaoHistoricoCountSql = @"
SELECT COUNT(1)
FROM dbo.MovimentacaoEstoque m
WHERE m.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR m.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR m.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR m.IdLote = @IdLote)
  AND (@TipoMovimento IS NULL OR m.TipoMovimento = @TipoMovimento)
  AND (@DataDe IS NULL OR m.DataMovimento >= @DataDe)
  AND (@DataAte IS NULL OR m.DataMovimento <= @DataAte);";

    private const string MovimentacaoHistoricoPageSql = @"
SELECT
    m.IdMovimentacaoEstoque,
    m.IdOrganizacao,
    m.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    m.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    m.IdLote,
    l.NumeroLote,
    m.TipoMovimento,
    m.Quantidade,
    m.DocumentoReferencia,
    m.DataMovimento
FROM dbo.MovimentacaoEstoque m
INNER JOIN dbo.Produto p ON p.IdProduto = m.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = m.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = m.IdLote
WHERE m.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR m.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR m.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR m.IdLote = @IdLote)
  AND (@TipoMovimento IS NULL OR m.TipoMovimento = @TipoMovimento)
  AND (@DataDe IS NULL OR m.DataMovimento >= @DataDe)
  AND (@DataAte IS NULL OR m.DataMovimento <= @DataAte)
ORDER BY m.IdMovimentacaoEstoque DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

    private const string MovimentacaoHistoricoExportSql = @"
SELECT TOP (@Limite)
    m.IdMovimentacaoEstoque,
    m.IdOrganizacao,
    m.IdUnidadeOrganizacional,
    uo.Codigo AS CodigoUnidade,
    uo.Nome AS NomeUnidade,
    m.IdProduto,
    p.Codigo AS CodigoProduto,
    p.Nome AS NomeProduto,
    m.IdLote,
    l.NumeroLote,
    m.TipoMovimento,
    m.Quantidade,
    m.DocumentoReferencia,
    m.DataMovimento
FROM dbo.MovimentacaoEstoque m
INNER JOIN dbo.Produto p ON p.IdProduto = m.IdProduto
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = m.IdUnidadeOrganizacional
LEFT JOIN dbo.Lote l ON l.IdLote = m.IdLote
WHERE m.IdOrganizacao = @IdOrganizacao
  AND (@IdUnidadeOrganizacional IS NULL OR m.IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR m.IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR m.IdLote = @IdLote)
  AND (@TipoMovimento IS NULL OR m.TipoMovimento = @TipoMovimento)
  AND (@DataDe IS NULL OR m.DataMovimento >= @DataDe)
  AND (@DataAte IS NULL OR m.DataMovimento <= @DataAte)
ORDER BY m.IdMovimentacaoEstoque DESC;";

    private const string UnidadeDaOrganizacaoSql = @"
SELECT TOP (1) IdUnidadeOrganizacional
FROM dbo.UnidadeOrganizacional
WHERE IdUnidadeOrganizacional = @IdUnidadeOrganizacional
  AND IdOrganizacao = @IdOrganizacao;";

    private const string ProdutoDaOrganizacaoSql = @"
SELECT TOP (1) IdProduto
FROM dbo.Produto
WHERE IdProduto = @IdProduto
  AND IdOrganizacao = @IdOrganizacao;";

    private const string LoteDoProdutoSql = @"
SELECT TOP (1) IdLote
FROM dbo.Lote
WHERE IdLote = @IdLote
  AND IdOrganizacao = @IdOrganizacao
  AND IdProduto = @IdProduto;";

    private const string EstoqueForUpdateSql = @"
SELECT TOP (1)
    IdEstoque,
    QuantidadeDisponivel,
    QuantidadeReservada
FROM dbo.Estoque WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE IdOrganizacao = @IdOrganizacao
  AND IdUnidadeOrganizacional = @IdUnidadeOrganizacional
  AND IdProduto = @IdProduto
  AND ((IdLote IS NULL AND @IdLote IS NULL) OR IdLote = @IdLote);";

    private const string InsertEstoqueSql = @"
INSERT INTO dbo.Estoque
    (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, QuantidadeDisponivel, QuantidadeReservada)
OUTPUT INSERTED.IdEstoque
VALUES
    (@IdOrganizacao, @IdUnidadeOrganizacional, @IdProduto, @IdLote, @QuantidadeDisponivel, @QuantidadeReservada);";

    private const string UpdateEstoqueSql = @"
UPDATE dbo.Estoque
SET QuantidadeDisponivel = @QuantidadeDisponivel
WHERE IdEstoque = @IdEstoque;";

    private const string InsertMovimentacaoSql = @"
INSERT INTO dbo.MovimentacaoEstoque
    (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, TipoMovimento, Quantidade, DocumentoReferencia, DataMovimento)
OUTPUT INSERTED.IdMovimentacaoEstoque
VALUES
    (@IdOrganizacao, @IdUnidadeOrganizacional, @IdProduto, @IdLote, @TipoMovimento, @Quantidade, @DocumentoReferencia, SYSUTCDATETIME());";

    private const string InsertReservaSql = @"
INSERT INTO dbo.ReservaEstoque
    (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, ExpiraEmUtc, Status, DocumentoReferencia)
OUTPUT INSERTED.IdReservaEstoque
VALUES
    (@IdOrganizacao, @IdUnidadeOrganizacional, @IdProduto, @IdLote, @Quantidade, @ExpiraEmUtc, @Status, @DocumentoReferencia);";

    private const string ReservaForUpdateSql = @"
SELECT TOP (1)
    IdReservaEstoque,
    IdOrganizacao,
    IdUnidadeOrganizacional,
    IdProduto,
    IdLote,
    Quantidade,
    ExpiraEmUtc,
    Status,
    DocumentoReferencia
FROM dbo.ReservaEstoque WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE IdReservaEstoque = @IdReservaEstoque;";

    private const string UpdateReservaStatusSql = @"
UPDATE dbo.ReservaEstoque
SET Status = @Status
WHERE IdReservaEstoque = @IdReservaEstoque;";

    private const string ExpiredReservasForUpdateSql = @"
SELECT
    IdReservaEstoque,
    Quantidade
FROM dbo.ReservaEstoque WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE IdOrganizacao = @IdOrganizacao
  AND IdUnidadeOrganizacional = @IdUnidadeOrganizacional
  AND IdProduto = @IdProduto
  AND ((IdLote IS NULL AND @IdLote IS NULL) OR IdLote = @IdLote)
  AND Status = 'ATIVA'
  AND ExpiraEmUtc < SYSUTCDATETIME();";

    private const string ExpiredReservasBatchForUpdateSql = @"
SELECT TOP (@MaxItens)
    IdReservaEstoque,
    IdUnidadeOrganizacional,
    IdProduto,
    IdLote,
    Quantidade
FROM dbo.ReservaEstoque WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE IdOrganizacao = @IdOrganizacao
  AND Status = 'ATIVA'
  AND ExpiraEmUtc < SYSUTCDATETIME()
  AND (@IdUnidadeOrganizacional IS NULL OR IdUnidadeOrganizacional = @IdUnidadeOrganizacional)
  AND (@IdProduto IS NULL OR IdProduto = @IdProduto)
  AND (@IdLote IS NULL OR IdLote = @IdLote)
ORDER BY ExpiraEmUtc, IdReservaEstoque;";

    public EstoqueController(
        ISqlConnectionFactory factory,
        IOrgContext orgContext,
        ICsvExportService csvExportService,
        IPdfExportService pdfExportService)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
        _pdfExportService = pdfExportService ?? throw new ArgumentNullException(nameof(pdfExportService));
    }

    [HttpGet("saldos")]
    public async Task<IActionResult> ObterSaldos(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] string? codigoProduto = null)
    {
        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<SaldoEstoqueItem>(
            SaldoSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                CodigoProduto = string.IsNullOrWhiteSpace(codigoProduto) ? null : codigoProduto.Trim()
            })).ToList();

        foreach (var item in itens)
            item.QuantidadeLiquida = item.QuantidadeDisponivel - item.QuantidadeReservada;

        var payload = new SaldosEstoqueResponse
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            Itens = itens
        };

        return Ok(ApiResponse<SaldosEstoqueResponse>.Ok(payload, "Saldos de estoque carregados com sucesso."));
    }

    [HttpGet("saldos/exportar-csv")]
    public async Task<IActionResult> ExportarSaldosCsv(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] string? codigoProduto = null,
        [FromQuery] int limite = 5000)
    {
        if (limite <= 0 || limite > 20000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 20000.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<SaldoEstoqueItem>(
            SaldoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                CodigoProduto = string.IsNullOrWhiteSpace(codigoProduto) ? null : codigoProduto.Trim(),
                Limite = limite
            })).ToList();

        foreach (var item in itens)
            item.QuantidadeLiquida = item.QuantidadeDisponivel - item.QuantidadeReservada;

        var csv = _csvExportService.BuildCsv(itens, new (string Header, Func<SaldoEstoqueItem, object?> ValueSelector)[]
        {
            ("IdEstoque", x => x.IdEstoque),
            ("IdOrganizacao", x => x.IdOrganizacao),
            ("IdUnidadeOrganizacional", x => x.IdUnidadeOrganizacional),
            ("CodigoUnidade", x => x.CodigoUnidade),
            ("NomeUnidade", x => x.NomeUnidade),
            ("IdProduto", x => x.IdProduto),
            ("CodigoProduto", x => x.CodigoProduto),
            ("NomeProduto", x => x.NomeProduto),
            ("IdLote", x => x.IdLote),
            ("NumeroLote", x => x.NumeroLote),
            ("QuantidadeDisponivel", x => x.QuantidadeDisponivel),
            ("QuantidadeReservada", x => x.QuantidadeReservada),
            ("QuantidadeLiquida", x => x.QuantidadeLiquida)
        });
        return BuildCsvFileResult(csv, "saldos");
    }

    [HttpGet("saldos/exportar-pdf")]
    public async Task<IActionResult> ExportarSaldosPdf(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] string? codigoProduto = null,
        [FromQuery] int limite = 1000)
    {
        if (limite <= 0 || limite > 10000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 10000.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<SaldoEstoqueItem>(
            SaldoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                CodigoProduto = string.IsNullOrWhiteSpace(codigoProduto) ? null : codigoProduto.Trim(),
                Limite = limite
            })).ToList();

        foreach (var item in itens)
            item.QuantidadeLiquida = item.QuantidadeDisponivel - item.QuantidadeReservada;

        var rows = itens
            .Select(x => (IReadOnlyList<string>)new[]
            {
                x.CodigoUnidade,
                x.CodigoProduto,
                x.NumeroLote ?? "-",
                x.QuantidadeDisponivel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.QuantidadeReservada.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.QuantidadeLiquida.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToList();

        var bytes = _pdfExportService.BuildSimpleReport(
            title: $"Saldos de Estoque - Organizacao {idOrganizacaoEfetiva.Value}",
            headers: new[] { "Unidade", "Produto", "Lote", "Disponivel", "Reservada", "Liquida" },
            rows: rows);

        return BuildPdfFileResult(bytes, "saldos");
    }

    [HttpGet("reservas/ativas")]
    public async Task<IActionResult> ObterReservasAtivas(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] string? status = null)
    {
        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<ReservaAtivaItem>(
            ReservaAtivaSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                Status = string.IsNullOrWhiteSpace(status) ? "ATIVA" : status.Trim()
            })).ToList();

        var payload = new ReservasAtivasResponse
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            Itens = itens
        };

        return Ok(ApiResponse<ReservasAtivasResponse>.Ok(payload, "Reservas ativas carregadas com sucesso."));
    }

    [HttpGet("reservas/ativas/exportar-csv")]
    public async Task<IActionResult> ExportarReservasAtivasCsv(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] string? status = null,
        [FromQuery] int limite = 5000)
    {
        if (limite <= 0 || limite > 20000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 20000.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<ReservaAtivaItem>(
            ReservaAtivaExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                Status = string.IsNullOrWhiteSpace(status) ? "ATIVA" : status.Trim(),
                Limite = limite
            })).ToList();

        var csv = _csvExportService.BuildCsv(itens, new (string Header, Func<ReservaAtivaItem, object?> ValueSelector)[]
        {
            ("IdReservaEstoque", x => x.IdReservaEstoque),
            ("IdOrganizacao", x => x.IdOrganizacao),
            ("IdUnidadeOrganizacional", x => x.IdUnidadeOrganizacional),
            ("CodigoUnidade", x => x.CodigoUnidade),
            ("NomeUnidade", x => x.NomeUnidade),
            ("IdProduto", x => x.IdProduto),
            ("CodigoProduto", x => x.CodigoProduto),
            ("NomeProduto", x => x.NomeProduto),
            ("IdLote", x => x.IdLote),
            ("NumeroLote", x => x.NumeroLote),
            ("Quantidade", x => x.Quantidade),
            ("ExpiraEmUtc", x => x.ExpiraEmUtc),
            ("Status", x => x.Status),
            ("DocumentoReferencia", x => x.DocumentoReferencia)
        });
        return BuildCsvFileResult(csv, "reservas_ativas");
    }

    [HttpGet("reservas")]
    public async Task<IActionResult> ObterReservasHistorico(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50)
    {
        if (pagina <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Pagina deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        if (tamanhoPagina <= 0 || tamanhoPagina > 200)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "TamanhoPagina deve estar entre 1 e 200.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        var offset = (pagina - 1) * tamanhoPagina;

        using var cn = _factory.CreateConnection();
        var filtros = new
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            IdUnidadeOrganizacional = idUnidadeOrganizacional,
            IdProduto = idProduto,
            IdLote = idLote,
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            DataDe = dataDe,
            DataAte = dataAte,
            Offset = offset,
            PageSize = tamanhoPagina
        };

        var totalItens = await cn.ExecuteScalarAsync<int>(ReservaHistoricoCountSql, filtros);
        var itens = (await cn.QueryAsync<ReservaHistoricoItem>(ReservaHistoricoPageSql, filtros)).ToList();

        var payload = new ReservasHistoricoResponse
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
            TotalItens = totalItens,
            TotalPaginas = totalItens == 0 ? 0 : (int)Math.Ceiling(totalItens / (double)tamanhoPagina),
            Itens = itens
        };

        return Ok(ApiResponse<ReservasHistoricoResponse>.Ok(payload, "Historico de reservas carregado com sucesso."));
    }

    [HttpGet("reservas/{idReservaEstoque:int}")]
    public async Task<IActionResult> ObterReservaDetalhe(int idReservaEstoque)
    {
        if (idReservaEstoque <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Id da reserva invalido.",
                code: "VALIDATION_ERROR"));
        }

        using var cn = _factory.CreateConnection();
        var detalhe = await cn.QueryFirstOrDefaultAsync<ReservaHistoricoItem>(
            ReservaDetalheSql,
            new { IdReservaEstoque = idReservaEstoque });

        if (detalhe is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                message: "Reserva nao encontrada.",
                code: "RESERVATION_NOT_FOUND"));
        }

        if (!await _orgContext.HasAccessToOrganizacaoAsync(detalhe.IdOrganizacao, HttpContext.RequestAborted))
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Usuario sem acesso a organizacao da reserva.",
                code: "ORG_FORBIDDEN"));
        }

        var eventos = (await cn.QueryAsync<ReservaEventoItem>(
            ReservaEventosSql,
            new { DocumentoReserva = $"RESERVA:{idReservaEstoque}" })).ToList();

        var payload = new ReservaDetalheResponse
        {
            Reserva = detalhe,
            Transicoes = eventos
        };

        return Ok(ApiResponse<ReservaDetalheResponse>.Ok(payload, "Detalhe da reserva carregado com sucesso."));
    }

    [HttpGet("reservas/exportar-csv")]
    public async Task<IActionResult> ExportarReservasCsv(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int limite = 5000)
    {
        if (limite <= 0 || limite > 20000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 20000.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<ReservaHistoricoItem>(
            ReservaHistoricoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote,
                Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
                DataDe = dataDe,
                DataAte = dataAte,
                Limite = limite
            })).ToList();

        var csv = _csvExportService.BuildCsv(itens, new (string Header, Func<ReservaHistoricoItem, object?> ValueSelector)[]
        {
            ("IdReservaEstoque", x => x.IdReservaEstoque),
            ("IdOrganizacao", x => x.IdOrganizacao),
            ("IdUnidadeOrganizacional", x => x.IdUnidadeOrganizacional),
            ("CodigoUnidade", x => x.CodigoUnidade),
            ("NomeUnidade", x => x.NomeUnidade),
            ("IdProduto", x => x.IdProduto),
            ("CodigoProduto", x => x.CodigoProduto),
            ("NomeProduto", x => x.NomeProduto),
            ("IdLote", x => x.IdLote),
            ("NumeroLote", x => x.NumeroLote),
            ("Quantidade", x => x.Quantidade),
            ("ExpiraEmUtc", x => x.ExpiraEmUtc),
            ("Status", x => x.Status),
            ("DocumentoReferencia", x => x.DocumentoReferencia)
        });
        return BuildCsvFileResult(csv, "reservas");
    }

    [HttpGet("reservas/exportar-pdf")]
    public async Task<IActionResult> ExportarReservasPdf(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int limite = 1000)
    {
        if (limite <= 0 || limite > 10000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 10000.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<ReservaHistoricoItem>(
            ReservaHistoricoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote,
                Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
                DataDe = dataDe,
                DataAte = dataAte,
                Limite = limite
            })).ToList();

        var rows = itens
            .Select(x => (IReadOnlyList<string>)new[]
            {
                x.CodigoUnidade,
                x.CodigoProduto,
                x.NumeroLote ?? "-",
                x.Quantidade.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.Status,
                x.ExpiraEmUtc.ToString("O"),
                x.DocumentoReferencia ?? "-"
            })
            .ToList();

        var bytes = _pdfExportService.BuildSimpleReport(
            title: $"Historico de Reservas - Organizacao {idOrganizacaoEfetiva.Value}",
            headers: new[] { "Unidade", "Produto", "Lote", "Quantidade", "Status", "ExpiraEmUtc", "Documento" },
            rows: rows);

        return BuildPdfFileResult(bytes, "reservas");
    }

    [HttpGet("movimentacoes")]
    public async Task<IActionResult> ObterMovimentacoesHistorico(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? tipoMovimento = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50)
    {
        if (pagina <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Pagina deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        if (tamanhoPagina <= 0 || tamanhoPagina > 200)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "TamanhoPagina deve estar entre 1 e 200.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        var offset = (pagina - 1) * tamanhoPagina;
        var tipoNormalizado = string.IsNullOrWhiteSpace(tipoMovimento) ? null : tipoMovimento.Trim().ToUpperInvariant();

        using var cn = _factory.CreateConnection();
        var filtros = new
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            IdUnidadeOrganizacional = idUnidadeOrganizacional,
            IdProduto = idProduto,
            IdLote = idLote,
            TipoMovimento = tipoNormalizado,
            DataDe = dataDe,
            DataAte = dataAte,
            Offset = offset,
            PageSize = tamanhoPagina
        };

        var totalItens = await cn.ExecuteScalarAsync<int>(MovimentacaoHistoricoCountSql, filtros);
        var itens = (await cn.QueryAsync<MovimentacaoHistoricoItem>(MovimentacaoHistoricoPageSql, filtros)).ToList();

        var payload = new MovimentacoesHistoricoResponse
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
            TotalItens = totalItens,
            TotalPaginas = totalItens == 0 ? 0 : (int)Math.Ceiling(totalItens / (double)tamanhoPagina),
            Itens = itens
        };

        return Ok(ApiResponse<MovimentacoesHistoricoResponse>.Ok(payload, "Historico de movimentacoes carregado com sucesso."));
    }

    [HttpGet("movimentacoes/exportar-csv")]
    public async Task<IActionResult> ExportarMovimentacoesCsv(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? tipoMovimento = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int limite = 5000)
    {
        if (limite <= 0 || limite > 20000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 20000.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        var tipoNormalizado = string.IsNullOrWhiteSpace(tipoMovimento) ? null : tipoMovimento.Trim().ToUpperInvariant();

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<MovimentacaoHistoricoItem>(
            MovimentacaoHistoricoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote,
                TipoMovimento = tipoNormalizado,
                DataDe = dataDe,
                DataAte = dataAte,
                Limite = limite
            })).ToList();

        var csv = _csvExportService.BuildCsv(itens, new (string Header, Func<MovimentacaoHistoricoItem, object?> ValueSelector)[]
        {
            ("IdMovimentacaoEstoque", x => x.IdMovimentacaoEstoque),
            ("IdOrganizacao", x => x.IdOrganizacao),
            ("IdUnidadeOrganizacional", x => x.IdUnidadeOrganizacional),
            ("CodigoUnidade", x => x.CodigoUnidade),
            ("NomeUnidade", x => x.NomeUnidade),
            ("IdProduto", x => x.IdProduto),
            ("CodigoProduto", x => x.CodigoProduto),
            ("NomeProduto", x => x.NomeProduto),
            ("IdLote", x => x.IdLote),
            ("NumeroLote", x => x.NumeroLote),
            ("TipoMovimento", x => x.TipoMovimento),
            ("Quantidade", x => x.Quantidade),
            ("DocumentoReferencia", x => x.DocumentoReferencia),
            ("DataMovimento", x => x.DataMovimento)
        });
        return BuildCsvFileResult(csv, "movimentacoes");
    }

    [HttpGet("movimentacoes/exportar-pdf")]
    public async Task<IActionResult> ExportarMovimentacoesPdf(
        [FromQuery] int? idOrganizacao = null,
        [FromQuery] int? idUnidadeOrganizacional = null,
        [FromQuery] int? idProduto = null,
        [FromQuery] int? idLote = null,
        [FromQuery] string? tipoMovimento = null,
        [FromQuery] DateTime? dataDe = null,
        [FromQuery] DateTime? dataAte = null,
        [FromQuery] int limite = 1000)
    {
        if (limite <= 0 || limite > 10000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Limite deve estar entre 1 e 10000.",
                code: "VALIDATION_ERROR"));
        }

        if (dataDe.HasValue && dataAte.HasValue && dataAte.Value < dataDe.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "DataAte deve ser maior ou igual a DataDe.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        var tipoNormalizado = string.IsNullOrWhiteSpace(tipoMovimento) ? null : tipoMovimento.Trim().ToUpperInvariant();

        using var cn = _factory.CreateConnection();
        var itens = (await cn.QueryAsync<MovimentacaoHistoricoItem>(
            MovimentacaoHistoricoExportSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote,
                TipoMovimento = tipoNormalizado,
                DataDe = dataDe,
                DataAte = dataAte,
                Limite = limite
            })).ToList();

        var rows = itens
            .Select(x => (IReadOnlyList<string>)new[]
            {
                x.CodigoUnidade,
                x.CodigoProduto,
                x.NumeroLote ?? "-",
                x.TipoMovimento,
                x.Quantidade.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.DataMovimento.ToString("O"),
                x.DocumentoReferencia ?? "-"
            })
            .ToList();

        var bytes = _pdfExportService.BuildSimpleReport(
            title: $"Historico de Movimentacoes - Organizacao {idOrganizacaoEfetiva.Value}",
            headers: new[] { "Unidade", "Produto", "Lote", "Tipo", "Quantidade", "DataMovimento", "Documento" },
            rows: rows);

        return BuildPdfFileResult(bytes, "movimentacoes");
    }

    [HttpPost("movimentacoes/entrada")]
    public async Task<IActionResult> RegistrarEntrada([FromBody] MovimentacaoEntradaRequest request)
    {
        if (request.Quantidade <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Quantidade deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        return await RegistrarMovimentacaoInternaAsync(
            idOrganizacaoInformado: request.IdOrganizacao,
            idUnidadeOrganizacional: request.IdUnidadeOrganizacional,
            idProduto: request.IdProduto,
            idLote: request.IdLote,
            quantidade: request.Quantidade,
            tipoMovimento: "ENTRADA",
            documentoReferencia: request.DocumentoReferencia,
            permitirCriarEstoque: true,
            ct: HttpContext.RequestAborted);
    }

    [HttpPost("movimentacoes/saida")]
    public async Task<IActionResult> RegistrarSaida([FromBody] MovimentacaoSaidaRequest request)
    {
        if (request.Quantidade <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Quantidade deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        return await RegistrarMovimentacaoInternaAsync(
            idOrganizacaoInformado: request.IdOrganizacao,
            idUnidadeOrganizacional: request.IdUnidadeOrganizacional,
            idProduto: request.IdProduto,
            idLote: request.IdLote,
            quantidade: request.Quantidade,
            tipoMovimento: "SAIDA",
            documentoReferencia: request.DocumentoReferencia,
            permitirCriarEstoque: false,
            ct: HttpContext.RequestAborted);
    }

    [HttpPost("movimentacoes/ajuste")]
    public async Task<IActionResult> RegistrarAjuste([FromBody] MovimentacaoAjusteRequest request)
    {
        if (request.QuantidadeDisponivel < 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Quantidade disponivel nao pode ser negativa.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(request.IdOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var escopoValido = await ValidarEscopoAsync(cn, tx, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);
        if (escopoValido is not null)
        {
            tx.Rollback();
            return escopoValido;
        }

        var estoque = await ObterEstoqueParaAtualizacaoAsync(cn, tx, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);
        if (estoque is null)
        {
            tx.Rollback();
            return NotFound(ApiResponse<object>.Fail(
                message: "Registro de estoque nao encontrado para ajuste.",
                code: "STOCK_NOT_FOUND"));
        }

        if (request.QuantidadeDisponivel < estoque.QuantidadeReservada)
        {
            tx.Rollback();
            return Conflict(ApiResponse<object>.Fail(
                message: "Ajuste nao pode deixar quantidade disponivel abaixo da reservada.",
                code: "ADJUSTMENT_BELOW_RESERVED"));
        }

        var quantidadeMovimento = request.QuantidadeDisponivel - estoque.QuantidadeDisponivel;
        await cn.ExecuteAsync(new CommandDefinition(
            UpdateEstoqueSql,
            new
            {
                IdEstoque = estoque.IdEstoque,
                QuantidadeDisponivel = request.QuantidadeDisponivel
            },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        var idMovimentacao = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            InsertMovimentacaoSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = request.IdUnidadeOrganizacional,
                IdProduto = request.IdProduto,
                IdLote = request.IdLote,
                TipoMovimento = "AJUSTE",
                Quantidade = quantidadeMovimento,
                DocumentoReferencia = string.IsNullOrWhiteSpace(request.DocumentoReferencia) ? "AJUSTE-MANUAL" : request.DocumentoReferencia.Trim()
            },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        tx.Commit();

        return Ok(ApiResponse<MovimentacaoResult>.Ok(new MovimentacaoResult
        {
            IdMovimentacaoEstoque = idMovimentacao,
            IdEstoque = estoque.IdEstoque,
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            IdUnidadeOrganizacional = request.IdUnidadeOrganizacional,
            IdProduto = request.IdProduto,
            IdLote = request.IdLote,
            TipoMovimento = "AJUSTE",
            QuantidadeMovimento = quantidadeMovimento,
            QuantidadeDisponivelAnterior = estoque.QuantidadeDisponivel,
            QuantidadeDisponivelAtual = request.QuantidadeDisponivel,
            QuantidadeReservada = estoque.QuantidadeReservada
        }, "Ajuste de estoque registrado com sucesso."));
    }

    [HttpPost("reservas")]
    public async Task<IActionResult> CriarReserva([FromBody] CriarReservaRequest request)
    {
        if (request.Quantidade <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Quantidade deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        if (request.TtlMinutos <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "TtlMinutos deve ser maior que zero.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(request.IdOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var escopoValido = await ValidarEscopoAsync(cn, tx, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);
        if (escopoValido is not null)
        {
            tx.Rollback();
            return escopoValido;
        }

        var estoque = await ObterEstoqueParaAtualizacaoAsync(cn, tx, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);
        if (estoque is null)
        {
            tx.Rollback();
            return NotFound(ApiResponse<object>.Fail(
                message: "Registro de estoque nao encontrado.",
                code: "STOCK_NOT_FOUND"));
        }

        await LiberarReservasExpiradasAsync(cn, tx, estoque, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);

        estoque = await ObterEstoqueParaAtualizacaoAsync(cn, tx, idOrganizacaoEfetiva.Value, request.IdUnidadeOrganizacional, request.IdProduto, request.IdLote, HttpContext.RequestAborted);
        if (estoque is null)
        {
            tx.Rollback();
            return NotFound(ApiResponse<object>.Fail(
                message: "Registro de estoque nao encontrado.",
                code: "STOCK_NOT_FOUND"));
        }

        var liquida = estoque.QuantidadeDisponivel - estoque.QuantidadeReservada;
        if (liquida < request.Quantidade)
        {
            tx.Rollback();
            return Conflict(ApiResponse<object>.Fail(
                message: "Saldo liquido insuficiente para reservar.",
                code: "INSUFFICIENT_STOCK"));
        }

        var novaReservada = estoque.QuantidadeReservada + request.Quantidade;
        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Estoque
              SET QuantidadeReservada = @QuantidadeReservada
              WHERE IdEstoque = @IdEstoque;",
            new { QuantidadeReservada = novaReservada, estoque.IdEstoque },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        var expiraEmUtc = DateTime.UtcNow.AddMinutes(request.TtlMinutos);
        var idReserva = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            InsertReservaSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = request.IdUnidadeOrganizacional,
                IdProduto = request.IdProduto,
                IdLote = request.IdLote,
                Quantidade = request.Quantidade,
                ExpiraEmUtc = expiraEmUtc,
                Status = "ATIVA",
                DocumentoReferencia = string.IsNullOrWhiteSpace(request.DocumentoReferencia) ? null : request.DocumentoReferencia.Trim()
            },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        tx.Commit();

        return Ok(ApiResponse<ReservaResult>.Ok(new ReservaResult
        {
            IdReservaEstoque = idReserva,
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            IdUnidadeOrganizacional = request.IdUnidadeOrganizacional,
            IdProduto = request.IdProduto,
            IdLote = request.IdLote,
            Quantidade = request.Quantidade,
            Status = "ATIVA",
            ExpiraEmUtc = expiraEmUtc
        }, "Reserva criada com sucesso."));
    }

    [HttpPost("reservas/{idReservaEstoque:int}/confirmar")]
    public async Task<IActionResult> ConfirmarReserva(int idReservaEstoque)
    {
        return await ResolverReservaAcaoAsync(idReservaEstoque, "CONFIRMADA");
    }

    [HttpPost("reservas/{idReservaEstoque:int}/cancelar")]
    public async Task<IActionResult> CancelarReserva(int idReservaEstoque)
    {
        return await ResolverReservaAcaoAsync(idReservaEstoque, "CANCELADA");
    }

    [HttpPost("reservas/expirar")]
    public async Task<IActionResult> ExpirarReservas([FromBody] ExpirarReservasRequest? request = null)
    {
        var maxItens = request?.MaxItens ?? 200;
        if (maxItens <= 0 || maxItens > 1000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "MaxItens deve estar entre 1 e 1000.",
                code: "VALIDATION_ERROR"));
        }

        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(request?.IdOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var expiradas = (await cn.QueryAsync<ReservaExpiradaRow>(new CommandDefinition(
            ExpiredReservasBatchForUpdateSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = request?.IdUnidadeOrganizacional,
                IdProduto = request?.IdProduto,
                IdLote = request?.IdLote,
                MaxItens = maxItens
            },
            tx,
            cancellationToken: HttpContext.RequestAborted))).ToList();

        if (expiradas.Count == 0)
        {
            tx.Commit();
            return Ok(ApiResponse<ExpirarReservasResult>.Ok(new ExpirarReservasResult
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                TotalProcessadas = 0,
                ReservasExpiradas = Array.Empty<int>()
            }, "Nenhuma reserva expirada encontrada para processamento."));
        }

        foreach (var reserva in expiradas)
        {
            var estoque = await ObterEstoqueParaAtualizacaoAsync(
                cn,
                tx,
                idOrganizacaoEfetiva.Value,
                reserva.IdUnidadeOrganizacional,
                reserva.IdProduto,
                reserva.IdLote,
                HttpContext.RequestAborted);

            if (estoque is not null)
            {
                var reservadaAtualizada = Math.Max(0m, estoque.QuantidadeReservada - reserva.Quantidade);
                await cn.ExecuteAsync(new CommandDefinition(
                    @"UPDATE dbo.Estoque
                      SET QuantidadeReservada = @QuantidadeReservada
                      WHERE IdEstoque = @IdEstoque;",
                    new { QuantidadeReservada = reservadaAtualizada, estoque.IdEstoque },
                    tx,
                    cancellationToken: HttpContext.RequestAborted));
            }

            await cn.ExecuteAsync(new CommandDefinition(
                UpdateReservaStatusSql,
                new { Status = "EXPIRADA", reserva.IdReservaEstoque },
                tx,
                cancellationToken: HttpContext.RequestAborted));
        }

        tx.Commit();

        return Ok(ApiResponse<ExpirarReservasResult>.Ok(new ExpirarReservasResult
        {
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            TotalProcessadas = expiradas.Count,
            ReservasExpiradas = expiradas.Select(x => x.IdReservaEstoque).ToArray()
        }, "Reservas expiradas processadas com sucesso."));
    }

    private async Task<IActionResult> RegistrarMovimentacaoInternaAsync(
        int? idOrganizacaoInformado,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        decimal quantidade,
        string tipoMovimento,
        string? documentoReferencia,
        bool permitirCriarEstoque,
        CancellationToken ct)
    {
        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacaoInformado);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"));
        }

        using var cn = _factory.CreateConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var escopoValido = await ValidarEscopoAsync(cn, tx, idOrganizacaoEfetiva.Value, idUnidadeOrganizacional, idProduto, idLote, ct);
        if (escopoValido is not null)
        {
            tx.Rollback();
            return escopoValido;
        }

        var estoque = await ObterEstoqueParaAtualizacaoAsync(cn, tx, idOrganizacaoEfetiva.Value, idUnidadeOrganizacional, idProduto, idLote, ct);
        decimal quantidadeAnterior = 0;
        decimal quantidadeReservada = 0;
        int idEstoque;

        if (estoque is null)
        {
            if (!permitirCriarEstoque)
            {
                tx.Rollback();
                return NotFound(ApiResponse<object>.Fail(
                    message: "Registro de estoque nao encontrado.",
                    code: "STOCK_NOT_FOUND"));
            }

            idEstoque = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                InsertEstoqueSql,
                new
                {
                    IdOrganizacao = idOrganizacaoEfetiva.Value,
                    IdUnidadeOrganizacional = idUnidadeOrganizacional,
                    IdProduto = idProduto,
                    IdLote = idLote,
                    QuantidadeDisponivel = quantidade,
                    QuantidadeReservada = 0m
                },
                tx,
                cancellationToken: ct));
        }
        else
        {
            quantidadeAnterior = estoque.QuantidadeDisponivel;
            quantidadeReservada = estoque.QuantidadeReservada;
            idEstoque = estoque.IdEstoque;

            var quantidadeAtual = tipoMovimento == "ENTRADA"
                ? estoque.QuantidadeDisponivel + quantidade
                : estoque.QuantidadeDisponivel - quantidade;

            if (tipoMovimento == "SAIDA")
            {
                var liquida = estoque.QuantidadeDisponivel - estoque.QuantidadeReservada;
                if (liquida < quantidade)
                {
                    tx.Rollback();
                    return Conflict(ApiResponse<object>.Fail(
                        message: "Saldo liquido insuficiente para realizar saida.",
                        code: "INSUFFICIENT_STOCK"));
                }
            }

            await cn.ExecuteAsync(new CommandDefinition(
                UpdateEstoqueSql,
                new
                {
                    IdEstoque = estoque.IdEstoque,
                    QuantidadeDisponivel = quantidadeAtual
                },
                tx,
                cancellationToken: ct));
        }

        var quantidadeAtualFinal = estoque is null
            ? quantidade
            : (tipoMovimento == "ENTRADA" ? estoque.QuantidadeDisponivel + quantidade : estoque.QuantidadeDisponivel - quantidade);

        var idMovimentacao = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            InsertMovimentacaoSql,
            new
            {
                IdOrganizacao = idOrganizacaoEfetiva.Value,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote,
                TipoMovimento = tipoMovimento,
                Quantidade = quantidade,
                DocumentoReferencia = string.IsNullOrWhiteSpace(documentoReferencia) ? null : documentoReferencia.Trim()
            },
            tx,
            cancellationToken: ct));

        tx.Commit();

        return Ok(ApiResponse<MovimentacaoResult>.Ok(new MovimentacaoResult
        {
            IdMovimentacaoEstoque = idMovimentacao,
            IdEstoque = idEstoque,
            IdOrganizacao = idOrganizacaoEfetiva.Value,
            IdUnidadeOrganizacional = idUnidadeOrganizacional,
            IdProduto = idProduto,
            IdLote = idLote,
            TipoMovimento = tipoMovimento,
            QuantidadeMovimento = quantidade,
            QuantidadeDisponivelAnterior = quantidadeAnterior,
            QuantidadeDisponivelAtual = quantidadeAtualFinal,
            QuantidadeReservada = quantidadeReservada
        }, $"Movimentacao de estoque ({tipoMovimento}) registrada com sucesso."));
    }

    private async Task<IActionResult> ResolverReservaAcaoAsync(int idReservaEstoque, string acao)
    {
        if (idReservaEstoque <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Id da reserva invalido.",
                code: "VALIDATION_ERROR"));
        }

        using var cn = _factory.CreateConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var reserva = await cn.QueryFirstOrDefaultAsync<ReservaLockRow>(new CommandDefinition(
            ReservaForUpdateSql,
            new { IdReservaEstoque = idReservaEstoque },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        if (reserva is null)
        {
            tx.Rollback();
            return NotFound(ApiResponse<object>.Fail(
                message: "Reserva nao encontrada.",
                code: "RESERVATION_NOT_FOUND"));
        }

        if (!await _orgContext.HasAccessToOrganizacaoAsync(reserva.IdOrganizacao, HttpContext.RequestAborted))
        {
            tx.Rollback();
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Usuario sem acesso a organizacao da reserva.",
                code: "ORG_FORBIDDEN"));
        }

        if (!string.Equals(reserva.Status, "ATIVA", StringComparison.OrdinalIgnoreCase))
        {
            tx.Rollback();
            return Conflict(ApiResponse<object>.Fail(
                message: "Reserva nao esta ativa para transicao.",
                code: "RESERVATION_NOT_ACTIVE"));
        }

        var estoque = await ObterEstoqueParaAtualizacaoAsync(
            cn,
            tx,
            reserva.IdOrganizacao,
            reserva.IdUnidadeOrganizacional,
            reserva.IdProduto,
            reserva.IdLote,
            HttpContext.RequestAborted);

        if (estoque is null)
        {
            tx.Rollback();
            return NotFound(ApiResponse<object>.Fail(
                message: "Registro de estoque nao encontrado para reserva.",
                code: "STOCK_NOT_FOUND"));
        }

        if (reserva.ExpiraEmUtc < DateTime.UtcNow)
        {
            var reservadaExpirada = Math.Max(0m, estoque.QuantidadeReservada - reserva.Quantidade);
            await cn.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.Estoque
                  SET QuantidadeReservada = @QuantidadeReservada
                  WHERE IdEstoque = @IdEstoque;",
                new { QuantidadeReservada = reservadaExpirada, estoque.IdEstoque },
                tx,
                cancellationToken: HttpContext.RequestAborted));

            await cn.ExecuteAsync(new CommandDefinition(
                UpdateReservaStatusSql,
                new { Status = "EXPIRADA", IdReservaEstoque = idReservaEstoque },
                tx,
                cancellationToken: HttpContext.RequestAborted));

            tx.Commit();

            return Conflict(ApiResponse<object>.Fail(
                message: "Reserva expirada.",
                code: "RESERVATION_EXPIRED"));
        }

        var novaReservada = Math.Max(0m, estoque.QuantidadeReservada - reserva.Quantidade);
        var novaDisponivel = estoque.QuantidadeDisponivel;

        if (acao == "CONFIRMADA")
        {
            var liquida = estoque.QuantidadeDisponivel - estoque.QuantidadeReservada;
            if (liquida < 0)
            {
                tx.Rollback();
                return Conflict(ApiResponse<object>.Fail(
                    message: "Saldo inconsistente para confirmacao de reserva.",
                    code: "INCONSISTENT_STOCK"));
            }

            novaDisponivel = estoque.QuantidadeDisponivel - reserva.Quantidade;
            if (novaDisponivel < 0)
            {
                tx.Rollback();
                return Conflict(ApiResponse<object>.Fail(
                    message: "Saldo insuficiente para confirmacao de reserva.",
                    code: "INSUFFICIENT_STOCK"));
            }
        }

        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Estoque
              SET QuantidadeReservada = @QuantidadeReservada,
                  QuantidadeDisponivel = @QuantidadeDisponivel
              WHERE IdEstoque = @IdEstoque;",
            new
            {
                QuantidadeReservada = novaReservada,
                QuantidadeDisponivel = novaDisponivel,
                estoque.IdEstoque
            },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        await cn.ExecuteAsync(new CommandDefinition(
            UpdateReservaStatusSql,
            new { Status = acao, IdReservaEstoque = idReservaEstoque },
            tx,
            cancellationToken: HttpContext.RequestAborted));

        if (acao == "CONFIRMADA")
        {
            _ = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                InsertMovimentacaoSql,
                new
                {
                    IdOrganizacao = reserva.IdOrganizacao,
                    IdUnidadeOrganizacional = reserva.IdUnidadeOrganizacional,
                    IdProduto = reserva.IdProduto,
                    IdLote = reserva.IdLote,
                    TipoMovimento = "SAIDA",
                    Quantidade = reserva.Quantidade,
                    DocumentoReferencia = $"RESERVA:{idReservaEstoque}"
                },
                tx,
                cancellationToken: HttpContext.RequestAborted));
        }

        tx.Commit();

        return Ok(ApiResponse<ReservaResult>.Ok(new ReservaResult
        {
            IdReservaEstoque = idReservaEstoque,
            IdOrganizacao = reserva.IdOrganizacao,
            IdUnidadeOrganizacional = reserva.IdUnidadeOrganizacional,
            IdProduto = reserva.IdProduto,
            IdLote = reserva.IdLote,
            Quantidade = reserva.Quantidade,
            Status = acao,
            ExpiraEmUtc = reserva.ExpiraEmUtc
        }, $"Reserva {acao.ToLowerInvariant()} com sucesso."));
    }

    private async Task LiberarReservasExpiradasAsync(
        IDbConnection cn,
        IDbTransaction tx,
        EstoqueLockRow estoque,
        int idOrganizacao,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        CancellationToken ct)
    {
        var expiradas = (await cn.QueryAsync<ReservaExpiradaRow>(new CommandDefinition(
            ExpiredReservasForUpdateSql,
            new
            {
                IdOrganizacao = idOrganizacao,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote
            },
            tx,
            cancellationToken: ct))).ToList();

        if (expiradas.Count == 0)
            return;

        var totalExpirado = expiradas.Sum(x => x.Quantidade);
        var reservadaAtualizada = Math.Max(0m, estoque.QuantidadeReservada - totalExpirado);

        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Estoque
              SET QuantidadeReservada = @QuantidadeReservada
              WHERE IdEstoque = @IdEstoque;",
            new { QuantidadeReservada = reservadaAtualizada, estoque.IdEstoque },
            tx,
            cancellationToken: ct));

        foreach (var reserva in expiradas)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                UpdateReservaStatusSql,
                new { Status = "EXPIRADA", IdReservaEstoque = reserva.IdReservaEstoque },
                tx,
                cancellationToken: ct));

        }
    }

    private async Task<IActionResult?> ValidarEscopoAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        CancellationToken ct)
    {
        var unidadeExiste = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            UnidadeDaOrganizacaoSql,
            new { IdUnidadeOrganizacional = idUnidadeOrganizacional, IdOrganizacao = idOrganizacao },
            tx,
            cancellationToken: ct));

        if (!unidadeExiste.HasValue)
        {
            return NotFound(ApiResponse<object>.Fail(
                message: "Unidade organizacional nao encontrada no escopo da organizacao.",
                code: "UNIT_NOT_FOUND"));
        }

        var produtoExiste = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            ProdutoDaOrganizacaoSql,
            new { IdProduto = idProduto, IdOrganizacao = idOrganizacao },
            tx,
            cancellationToken: ct));

        if (!produtoExiste.HasValue)
        {
            return NotFound(ApiResponse<object>.Fail(
                message: "Produto nao encontrado no escopo da organizacao.",
                code: "PRODUCT_NOT_FOUND"));
        }

        if (idLote.HasValue)
        {
            var loteExiste = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
                LoteDoProdutoSql,
                new { IdLote = idLote.Value, IdOrganizacao = idOrganizacao, IdProduto = idProduto },
                tx,
                cancellationToken: ct));

            if (!loteExiste.HasValue)
            {
                return NotFound(ApiResponse<object>.Fail(
                    message: "Lote nao encontrado para o produto no escopo informado.",
                    code: "LOT_NOT_FOUND"));
            }
        }

        return null;
    }


    private async Task<EstoqueLockRow?> ObterEstoqueParaAtualizacaoAsync(
        IDbConnection cn,
        IDbTransaction tx,
        int idOrganizacao,
        int idUnidadeOrganizacional,
        int idProduto,
        int? idLote,
        CancellationToken ct)
    {
        return await cn.QueryFirstOrDefaultAsync<EstoqueLockRow>(new CommandDefinition(
            EstoqueForUpdateSql,
            new
            {
                IdOrganizacao = idOrganizacao,
                IdUnidadeOrganizacional = idUnidadeOrganizacional,
                IdProduto = idProduto,
                IdLote = idLote
            },
            tx,
            cancellationToken: ct));
    }

    private async Task<int?> ResolverOrganizacaoEfetivaAsync(int? idOrganizacao)
    {
        if (idOrganizacao.HasValue)
            return idOrganizacao.Value;

        return await _orgContext.GetCurrentOrganizacaoIdAsync(HttpContext.RequestAborted);
    }

    private FileContentResult BuildCsvFileResult(string csv, string resource)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var fileName = $"{resource}_{generatedAtUtc:yyyyMMdd_HHmmss}.csv";
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);

        Response.Headers["X-Export-Format"] = "csv";
        Response.Headers["X-Export-Resource"] = resource;
        Response.Headers["X-Export-GeneratedAtUtc"] = generatedAtUtc.ToString("O");
        Response.Headers["X-Export-FileName"] = fileName;
        AppendExposedHeaders("Content-Disposition,X-Export-Format,X-Export-Resource,X-Export-GeneratedAtUtc,X-Export-FileName");

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private FileContentResult BuildPdfFileResult(byte[] bytes, string resource)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var fileName = $"{resource}_{generatedAtUtc:yyyyMMdd_HHmmss}.pdf";

        Response.Headers["X-Export-Format"] = "pdf";
        Response.Headers["X-Export-Resource"] = resource;
        Response.Headers["X-Export-GeneratedAtUtc"] = generatedAtUtc.ToString("O");
        Response.Headers["X-Export-FileName"] = fileName;
        AppendExposedHeaders("Content-Disposition,X-Export-Format,X-Export-Resource,X-Export-GeneratedAtUtc,X-Export-FileName");

        return File(bytes, "application/pdf", fileName);
    }

    private void AppendExposedHeaders(string headersToExpose)
    {
        if (!Response.Headers.TryGetValue("Access-Control-Expose-Headers", out var existing))
        {
            Response.Headers["Access-Control-Expose-Headers"] = headersToExpose;
            return;
        }

        var merged = existing.ToString();
        foreach (var header in headersToExpose.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!merged.Contains(header, StringComparison.OrdinalIgnoreCase))
                merged = string.IsNullOrWhiteSpace(merged) ? header : $"{merged},{header}";
        }

        Response.Headers["Access-Control-Expose-Headers"] = merged;
    }

    public sealed class SaldosEstoqueResponse
    {
        public int IdOrganizacao { get; set; }
        public IReadOnlyList<SaldoEstoqueItem> Itens { get; set; } = Array.Empty<SaldoEstoqueItem>();
    }

    public sealed class SaldoEstoqueItem
    {
        public int IdEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public string CodigoUnidade { get; set; } = string.Empty;
        public string NomeUnidade { get; set; } = string.Empty;
        public int IdProduto { get; set; }
        public string CodigoProduto { get; set; } = string.Empty;
        public string NomeProduto { get; set; } = string.Empty;
        public int? IdLote { get; set; }
        public string? NumeroLote { get; set; }
        public decimal QuantidadeDisponivel { get; set; }
        public decimal QuantidadeReservada { get; set; }
        public decimal QuantidadeLiquida { get; set; }
    }

    public sealed class ReservasAtivasResponse
    {
        public int IdOrganizacao { get; set; }
        public IReadOnlyList<ReservaAtivaItem> Itens { get; set; } = Array.Empty<ReservaAtivaItem>();
    }

    public sealed class ReservaAtivaItem
    {
        public int IdReservaEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public string CodigoUnidade { get; set; } = string.Empty;
        public string NomeUnidade { get; set; } = string.Empty;
        public int IdProduto { get; set; }
        public string CodigoProduto { get; set; } = string.Empty;
        public string NomeProduto { get; set; } = string.Empty;
        public int? IdLote { get; set; }
        public string? NumeroLote { get; set; }
        public decimal Quantidade { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class ReservasHistoricoResponse
    {
        public int IdOrganizacao { get; set; }
        public int Pagina { get; set; }
        public int TamanhoPagina { get; set; }
        public int TotalItens { get; set; }
        public int TotalPaginas { get; set; }
        public IReadOnlyList<ReservaHistoricoItem> Itens { get; set; } = Array.Empty<ReservaHistoricoItem>();
    }

    public sealed class ReservaDetalheResponse
    {
        public ReservaHistoricoItem Reserva { get; set; } = new();
        public IReadOnlyList<ReservaEventoItem> Transicoes { get; set; } = Array.Empty<ReservaEventoItem>();
    }

    public sealed class ReservaHistoricoItem
    {
        public int IdReservaEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public string CodigoUnidade { get; set; } = string.Empty;
        public string NomeUnidade { get; set; } = string.Empty;
        public int IdProduto { get; set; }
        public string CodigoProduto { get; set; } = string.Empty;
        public string NomeProduto { get; set; } = string.Empty;
        public int? IdLote { get; set; }
        public string? NumeroLote { get; set; }
        public decimal Quantidade { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class ReservaEventoItem
    {
        public int IdMovimentacaoEstoque { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public decimal Quantidade { get; set; }
        public string? DocumentoReferencia { get; set; }
        public DateTime DataMovimento { get; set; }
    }

    public sealed class MovimentacaoEntradaRequest
    {
        public int? IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class MovimentacaoSaidaRequest
    {
        public int? IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class MovimentacaoAjusteRequest
    {
        public int? IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal QuantidadeDisponivel { get; set; }
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class MovimentacaoResult
    {
        public int IdMovimentacaoEstoque { get; set; }
        public int IdEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public decimal QuantidadeMovimento { get; set; }
        public decimal QuantidadeDisponivelAnterior { get; set; }
        public decimal QuantidadeDisponivelAtual { get; set; }
        public decimal QuantidadeReservada { get; set; }
    }

    public sealed class MovimentacoesHistoricoResponse
    {
        public int IdOrganizacao { get; set; }
        public int Pagina { get; set; }
        public int TamanhoPagina { get; set; }
        public int TotalItens { get; set; }
        public int TotalPaginas { get; set; }
        public IReadOnlyList<MovimentacaoHistoricoItem> Itens { get; set; } = Array.Empty<MovimentacaoHistoricoItem>();
    }

    public sealed class MovimentacaoHistoricoItem
    {
        public int IdMovimentacaoEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public string CodigoUnidade { get; set; } = string.Empty;
        public string NomeUnidade { get; set; } = string.Empty;
        public int IdProduto { get; set; }
        public string CodigoProduto { get; set; } = string.Empty;
        public string NomeProduto { get; set; } = string.Empty;
        public int? IdLote { get; set; }
        public string? NumeroLote { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public decimal Quantidade { get; set; }
        public string? DocumentoReferencia { get; set; }
        public DateTime DataMovimento { get; set; }
    }

    public sealed class CriarReservaRequest
    {
        public int? IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
        public int TtlMinutos { get; set; } = 15;
        public string? DocumentoReferencia { get; set; }
    }

    public sealed class ReservaResult
    {
        public int IdReservaEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class ExpirarReservasRequest
    {
        public int? IdOrganizacao { get; set; }
        public int? IdUnidadeOrganizacional { get; set; }
        public int? IdProduto { get; set; }
        public int? IdLote { get; set; }
        public int MaxItens { get; set; } = 200;
    }

    public sealed class ExpirarReservasResult
    {
        public int IdOrganizacao { get; set; }
        public int TotalProcessadas { get; set; }
        public IReadOnlyList<int> ReservasExpiradas { get; set; } = Array.Empty<int>();
    }

    private sealed class EstoqueLockRow
    {
        public int IdEstoque { get; set; }
        public decimal QuantidadeDisponivel { get; set; }
        public decimal QuantidadeReservada { get; set; }
    }

    private sealed class ReservaLockRow
    {
        public int IdReservaEstoque { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DocumentoReferencia { get; set; }
    }

    private sealed class ReservaExpiradaRow
    {
        public int IdReservaEstoque { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
        public int IdProduto { get; set; }
        public int? IdLote { get; set; }
        public decimal Quantidade { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Interfaces.Context;
using ProformaFarm.Application.Interfaces.Data;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/organizacao")]
public sealed class OrganizacaoController : ControllerBase
{
    private readonly ISqlConnectionFactory _factory;
    private readonly IOrgContext _orgContext;

    private const string EstruturaSql = @"
DECLARE @IdOrganizacao INT = @IdOrgParam;

IF @IdOrganizacao IS NULL
BEGIN
    SELECT TOP (1) @IdOrganizacao = IdOrganizacao
    FROM dbo.Organizacao
    WHERE Ativa = 1
    ORDER BY IdOrganizacao;
END;

SELECT IdOrganizacao, RazaoSocial, NomeFantasia, Cnpj, Ativa, DataCriacao
FROM dbo.Organizacao
WHERE IdOrganizacao = @IdOrganizacao;

SELECT IdUnidadeOrganizacional, IdOrganizacao, IdUnidadePai, Tipo, Codigo, Nome, Ativa, DataInicio, DataFim
FROM dbo.UnidadeOrganizacional
WHERE IdOrganizacao = @IdOrganizacao
ORDER BY CASE WHEN IdUnidadePai IS NULL THEN 0 ELSE 1 END, IdUnidadePai, Nome;

SELECT
    lu.IdLotacaoUsuario,
    lu.IdUsuario,
    u.Login,
    lu.IdUnidadeOrganizacional,
    uo.Nome AS NomeUnidade,
    lu.IdCargo,
    c.Nome AS NomeCargo,
    lu.Principal,
    lu.Ativa,
    lu.DataInicio,
    lu.DataFim
FROM dbo.LotacaoUsuario lu
INNER JOIN dbo.Usuario u ON u.IdUsuario = lu.IdUsuario
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = lu.IdUnidadeOrganizacional
LEFT JOIN dbo.Cargo c ON c.IdCargo = lu.IdCargo
WHERE uo.IdOrganizacao = @IdOrganizacao
  AND lu.Ativa = 1
ORDER BY lu.Principal DESC, u.Login;
";

    public OrganizacaoController(ISqlConnectionFactory factory, IOrgContext orgContext)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    [Authorize]
    [HttpGet("contexto")]
    public async Task<IActionResult> ObterContexto()
    {
        var idUsuario = _orgContext.GetCurrentUsuarioId();
        var idOrganizacao = await _orgContext.GetCurrentOrganizacaoIdAsync(HttpContext.RequestAborted);
        var idUnidade = await _orgContext.GetCurrentUnidadeIdAsync(HttpContext.RequestAborted);

        if (!idUsuario.HasValue || !idOrganizacao.HasValue || !idUnidade.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"
            ));
        }

        var payload = new ContextoOrganizacionalResponse
        {
            IdUsuario = idUsuario.Value,
            IdOrganizacao = idOrganizacao.Value,
            IdUnidade = idUnidade.Value
        };

        return Ok(ApiResponse<ContextoOrganizacionalResponse>.Ok(payload, "Contexto organizacional resolvido com sucesso."));
    }

    [Authorize]
    [HttpGet("estrutura")]
    public async Task<IActionResult> ObterEstrutura([FromQuery] int? idOrganizacao = null)
    {
        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"
            ));
        }

        using var cn = _factory.CreateConnection();
        var (org, unidades, lotacoes) = await CarregarEstruturaAsync(cn, idOrganizacaoEfetiva);

        if (org is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                message: "Organizacao nao encontrada.",
                code: "NOT_FOUND"
            ));
        }

        var payload = new EstruturaOrganizacionalResponse
        {
            Organizacao = org,
            Unidades = unidades,
            LotacoesAtivas = lotacoes
        };

        return Ok(ApiResponse<EstruturaOrganizacionalResponse>.Ok(payload, "Estrutura carregada com sucesso."));
    }

    [Authorize]
    [HttpGet("estrutura/arvore")]
    public async Task<IActionResult> ObterEstruturaArvore([FromQuery] int? idOrganizacao = null)
    {
        var idOrganizacaoEfetiva = await ResolverOrganizacaoEfetivaAsync(idOrganizacao);
        if (!idOrganizacaoEfetiva.HasValue)
        {
            return StatusCode(403, ApiResponse<object>.Fail(
                message: "Contexto organizacional nao resolvido para o usuario.",
                code: "ORG_CONTEXT_NOT_FOUND"
            ));
        }

        using var cn = _factory.CreateConnection();
        var (org, unidades, lotacoes) = await CarregarEstruturaAsync(cn, idOrganizacaoEfetiva);

        if (org is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                message: "Organizacao nao encontrada.",
                code: "NOT_FOUND"
            ));
        }

        var lotacoesPorUnidade = lotacoes
            .GroupBy(x => x.IdUnidadeOrganizacional)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LotacaoItem>)g.ToList());

        var nodes = unidades.ToDictionary(
            u => u.IdUnidadeOrganizacional,
            u => new UnidadeArvoreItem
            {
                IdUnidadeOrganizacional = u.IdUnidadeOrganizacional,
                IdOrganizacao = u.IdOrganizacao,
                IdUnidadePai = u.IdUnidadePai,
                Tipo = u.Tipo,
                Codigo = u.Codigo,
                Nome = u.Nome,
                Ativa = u.Ativa,
                DataInicio = u.DataInicio,
                DataFim = u.DataFim,
                LotacoesAtivas = lotacoesPorUnidade.TryGetValue(u.IdUnidadeOrganizacional, out var ls)
                    ? ls
                    : Array.Empty<LotacaoItem>()
            });

        foreach (var node in nodes.Values.OrderBy(x => x.Nome))
        {
            if (node.IdUnidadePai.HasValue && nodes.TryGetValue(node.IdUnidadePai.Value, out var pai))
                pai.Filhos.Add(node);
        }

        var raizes = nodes.Values
            .Where(x => !x.IdUnidadePai.HasValue || !nodes.ContainsKey(x.IdUnidadePai.Value))
            .OrderBy(x => x.Nome)
            .ToList();

        var payload = new EstruturaOrganizacionalArvoreResponse
        {
            Organizacao = org,
            Raizes = raizes
        };

        return Ok(ApiResponse<EstruturaOrganizacionalArvoreResponse>.Ok(payload, "Arvore organizacional carregada com sucesso."));
    }

    private static async Task<(OrganizacaoItem? org, List<UnidadeItem> unidades, List<LotacaoItem> lotacoes)> CarregarEstruturaAsync(
        IDbConnection cn,
        int? idOrganizacao)
    {
        using var multi = await cn.QueryMultipleAsync(EstruturaSql, new { IdOrgParam = idOrganizacao });

        var org = await multi.ReadFirstOrDefaultAsync<OrganizacaoItem>();
        var unidades = (await multi.ReadAsync<UnidadeItem>()).ToList();
        var lotacoes = (await multi.ReadAsync<LotacaoItem>()).ToList();

        return (org, unidades, lotacoes);
    }

    private async Task<int?> ResolverOrganizacaoEfetivaAsync(int? idOrganizacao)
    {
        if (idOrganizacao.HasValue)
            return idOrganizacao.Value;

        return await _orgContext.GetCurrentOrganizacaoIdAsync();
    }

    public sealed class EstruturaOrganizacionalResponse
    {
        public OrganizacaoItem Organizacao { get; set; } = new();
        public IReadOnlyList<UnidadeItem> Unidades { get; set; } = Array.Empty<UnidadeItem>();
        public IReadOnlyList<LotacaoItem> LotacoesAtivas { get; set; } = Array.Empty<LotacaoItem>();
    }

    public sealed class EstruturaOrganizacionalArvoreResponse
    {
        public OrganizacaoItem Organizacao { get; set; } = new();
        public IReadOnlyList<UnidadeArvoreItem> Raizes { get; set; } = Array.Empty<UnidadeArvoreItem>();
    }

    public sealed class OrganizacaoItem
    {
        public int IdOrganizacao { get; set; }
        public string RazaoSocial { get; set; } = string.Empty;
        public string? NomeFantasia { get; set; }
        public string Cnpj { get; set; } = string.Empty;
        public bool Ativa { get; set; }
        public DateTime DataCriacao { get; set; }
    }

    public class UnidadeBaseItem
    {
        public int IdUnidadeOrganizacional { get; set; }
        public int IdOrganizacao { get; set; }
        public int? IdUnidadePai { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public bool Ativa { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }

    public sealed class UnidadeItem : UnidadeBaseItem
    {
    }

    public sealed class UnidadeArvoreItem : UnidadeBaseItem
    {
        public List<UnidadeArvoreItem> Filhos { get; set; } = new();
        public IReadOnlyList<LotacaoItem> LotacoesAtivas { get; set; } = Array.Empty<LotacaoItem>();
    }

    public sealed class LotacaoItem
    {
        public int IdLotacaoUsuario { get; set; }
        public int IdUsuario { get; set; }
        public string Login { get; set; } = string.Empty;
        public int IdUnidadeOrganizacional { get; set; }
        public string NomeUnidade { get; set; } = string.Empty;
        public int? IdCargo { get; set; }
        public string? NomeCargo { get; set; }
        public bool Principal { get; set; }
        public bool Ativa { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }

    public sealed class ContextoOrganizacionalResponse
    {
        public int IdUsuario { get; set; }
        public int IdOrganizacao { get; set; }
        public int IdUnidade { get; set; }
    }
}

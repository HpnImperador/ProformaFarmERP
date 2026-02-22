using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using ProformaFarm.Application.Interfaces.Context;
using ProformaFarm.Application.Interfaces.Data;

namespace ProformaFarm.Infrastructure.Context;

public sealed class OrgContext : IOrgContext
{
    private const string HeaderOrganizacaoId = "X-Organizacao-Id";
    private const string CacheKeyUsuarioId = "__orgctx_usuario_id";
    private const string CacheKeyHeaderProvided = "__orgctx_header_provided";
    private const string CacheKeyHeaderInvalid = "__orgctx_header_invalid";
    private const string CacheKeyHeaderOrgId = "__orgctx_header_org_id";
    private const string CacheKeyResolved = "__orgctx_resolved";
    private const string CacheKeyResolvedOrgId = "__orgctx_resolved_org_id";
    private const string CacheKeyResolvedUnidadeId = "__orgctx_resolved_unidade_id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISqlConnectionFactory _factory;

    public OrgContext(IHttpContextAccessor httpContextAccessor, ISqlConnectionFactory factory)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public int? GetCurrentUsuarioId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var items = httpContext?.Items;
        if (items is not null && items.TryGetValue(CacheKeyUsuarioId, out var cached))
            return cached as int?;

        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        var id = int.TryParse(raw, out var idUsuario) ? idUsuario : (int?)null;
        if (items is not null)
            items[CacheKeyUsuarioId] = id;

        return id;
    }

    public bool IsRequestedOrganizacaoHeaderProvided()
    {
        EnsureHeaderSnapshot();
        var items = _httpContextAccessor.HttpContext?.Items;
        return items is not null
               && items.TryGetValue(CacheKeyHeaderProvided, out var value)
               && value is bool provided
               && provided;
    }

    public bool IsRequestedOrganizacaoHeaderInvalid()
    {
        EnsureHeaderSnapshot();
        var items = _httpContextAccessor.HttpContext?.Items;
        return items is not null
               && items.TryGetValue(CacheKeyHeaderInvalid, out var value)
               && value is bool invalid
               && invalid;
    }

    public int? GetRequestedOrganizacaoId()
    {
        EnsureHeaderSnapshot();
        var items = _httpContextAccessor.HttpContext?.Items;
        if (items is not null && items.TryGetValue(CacheKeyHeaderOrgId, out var value))
            return value as int?;

        return null;
    }

    public async Task<int?> GetCurrentOrganizacaoIdAsync(CancellationToken ct = default)
    {
        await EnsureResolvedAsync(ct);

        var items = _httpContextAccessor.HttpContext?.Items;
        if (items is not null && items.TryGetValue(CacheKeyResolvedOrgId, out var value))
            return value as int?;

        return null;
    }

    public async Task<int?> GetCurrentUnidadeIdAsync(CancellationToken ct = default)
    {
        await EnsureResolvedAsync(ct);

        var items = _httpContextAccessor.HttpContext?.Items;
        if (items is not null && items.TryGetValue(CacheKeyResolvedUnidadeId, out var value))
            return value as int?;

        return null;
    }

    public async Task<bool> HasAccessToOrganizacaoAsync(int idOrganizacao, CancellationToken ct = default)
    {
        var idUsuario = GetCurrentUsuarioId();
        if (!idUsuario.HasValue)
            return false;
        if (idOrganizacao <= 0)
            return false;

        await EnsureResolvedAsync(ct);
        var orgAtual = await GetCurrentOrganizacaoIdAsync(ct);
        if (orgAtual.HasValue && orgAtual.Value == idOrganizacao)
            return true;

        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT COUNT(1)
FROM dbo.LotacaoUsuario lu
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = lu.IdUnidadeOrganizacional
WHERE lu.IdUsuario = @IdUsuario
  AND lu.Ativa = 1
  AND uo.IdOrganizacao = @IdOrganizacao;
";

        var total = await cn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { IdUsuario = idUsuario.Value, IdOrganizacao = idOrganizacao }, cancellationToken: ct));

        return total > 0;
    }

    private void EnsureHeaderSnapshot()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var items = httpContext?.Items;
        if (items is null || items.ContainsKey(CacheKeyHeaderProvided))
            return;

        var headers = httpContext?.Request?.Headers;
        if (headers is null || !headers.TryGetValue(HeaderOrganizacaoId, out var raw))
        {
            items[CacheKeyHeaderProvided] = false;
            items[CacheKeyHeaderInvalid] = false;
            items[CacheKeyHeaderOrgId] = null;
            return;
        }

        items[CacheKeyHeaderProvided] = true;
        if (int.TryParse(raw.FirstOrDefault(), out var idOrganizacao))
        {
            items[CacheKeyHeaderInvalid] = false;
            items[CacheKeyHeaderOrgId] = (int?)idOrganizacao;
            return;
        }

        items[CacheKeyHeaderInvalid] = true;
        items[CacheKeyHeaderOrgId] = null;
    }

    private async Task EnsureResolvedAsync(CancellationToken ct)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var items = httpContext?.Items;
        if (items is null)
            return;

        if (items.ContainsKey(CacheKeyResolved))
            return;

        EnsureHeaderSnapshot();

        var idUsuario = GetCurrentUsuarioId();
        if (!idUsuario.HasValue)
        {
            items[CacheKeyResolved] = true;
            items[CacheKeyResolvedOrgId] = null;
            items[CacheKeyResolvedUnidadeId] = null;
            return;
        }

        var idDoHeader = GetRequestedOrganizacaoId();

        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT TOP (1)
    uo.IdOrganizacao,
    lu.IdUnidadeOrganizacional
FROM dbo.LotacaoUsuario lu
INNER JOIN dbo.UnidadeOrganizacional uo ON uo.IdUnidadeOrganizacional = lu.IdUnidadeOrganizacional
WHERE lu.IdUsuario = @IdUsuario
  AND lu.Ativa = 1
  AND (@IdDoHeader IS NULL OR uo.IdOrganizacao = @IdDoHeader)
ORDER BY lu.Principal DESC, lu.IdLotacaoUsuario DESC;
";

        var result = await cn.QueryFirstOrDefaultAsync<ResolvedOrgRow>(
            new CommandDefinition(sql, new { IdUsuario = idUsuario.Value, IdDoHeader = idDoHeader }, cancellationToken: ct));

        items[CacheKeyResolved] = true;
        items[CacheKeyResolvedOrgId] = result?.IdOrganizacao;
        items[CacheKeyResolvedUnidadeId] = result?.IdUnidadeOrganizacional;
    }

    private sealed class ResolvedOrgRow
    {
        public int IdOrganizacao { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
    }
}

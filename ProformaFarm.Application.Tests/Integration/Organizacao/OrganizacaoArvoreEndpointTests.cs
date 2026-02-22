using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Organizacao;

public sealed class OrganizacaoArvoreEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrganizacaoArvoreEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/organizacao/estrutura/arvore");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_e_id_valido_deve_retornar_arvore_coerente()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"/api/organizacao/estrutura/arvore?idOrganizacao={setup.IdOrganizacao}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ArvorePayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var raizes = body.Data!.Raizes;
        Assert.NotEmpty(raizes);

        var matriz = FindByCodigo(raizes, "IT-MATRIZ");
        Assert.NotNull(matriz);

        var filial = FindByCodigo(matriz!.Filhos, "IT-FILIAL-001");
        Assert.NotNull(filial);

        Assert.True(ContainsAnyLotacao(raizes));
    }

    [Fact]
    public async Task Com_token_e_id_inexistente_deve_retornar_404_not_found()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync("/api/organizacao/estrutura/arvore?idOrganizacao=99999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("NOT_FOUND", body.Code);
    }

    [Fact]
    public async Task Com_header_organizacao_sem_acesso_deve_retornar_403()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "99999999");

        var response = await client.GetAsync("/api/organizacao/estrutura/arvore");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("ORG_FORBIDDEN", body.Code);
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
        Assert.False(string.IsNullOrWhiteSpace(loginBody.Data!.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Data.AccessToken);
        return client;
    }

    private static UnidadeArvoreItem? FindByCodigo(IEnumerable<UnidadeArvoreItem> nodes, string codigo)
    {
        foreach (var node in nodes)
        {
            if (node.Codigo == codigo)
                return node;

            var found = FindByCodigo(node.Filhos, codigo);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static bool ContainsAnyLotacao(IEnumerable<UnidadeArvoreItem> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.LotacoesAtivas.Any())
                return true;

            if (ContainsAnyLotacao(node.Filhos))
                return true;
        }

        return false;
    }

    public sealed class ArvorePayload
    {
        public IReadOnlyList<UnidadeArvoreItem> Raizes { get; set; } = new List<UnidadeArvoreItem>();
    }

    public sealed class UnidadeArvoreItem
    {
        public string Codigo { get; set; } = string.Empty;
        public IReadOnlyList<UnidadeArvoreItem> Filhos { get; set; } = new List<UnidadeArvoreItem>();
        public IReadOnlyList<LotacaoItem> LotacoesAtivas { get; set; } = new List<LotacaoItem>();
    }

    public sealed class LotacaoItem
    {
        public int IdLotacaoUsuario { get; set; }
        public int IdUsuario { get; set; }
    }
}

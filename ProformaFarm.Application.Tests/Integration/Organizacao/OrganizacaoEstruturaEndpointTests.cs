using System.Collections.Generic;
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

public sealed class OrganizacaoEstruturaEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrganizacaoEstruturaEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/organizacao/estrutura");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_e_id_valido_deve_retornar_200_com_payload()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"/api/organizacao/estrutura?idOrganizacao={setup.IdOrganizacao}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<EstruturaPayload>>();

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("OK", body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal(setup.IdOrganizacao, body.Data!.Organizacao.IdOrganizacao);
        Assert.NotEmpty(body.Data.Unidades);
        Assert.NotEmpty(body.Data.LotacoesAtivas);
    }

    [Fact]
    public async Task Com_token_e_id_inexistente_deve_retornar_404_not_found()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync("/api/organizacao/estrutura?idOrganizacao=99999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("NOT_FOUND", body.Code);
    }

    [Fact]
    public async Task Com_header_organizacao_invalido_deve_retornar_403()
    {
        var setup = await OrganizacaoTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "abc");

        var response = await client.GetAsync("/api/organizacao/estrutura");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("ORG_HEADER_INVALID", body.Code);
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

    public sealed class EstruturaPayload
    {
        public OrganizacaoItem Organizacao { get; set; } = new();
        public IReadOnlyList<UnidadeItem> Unidades { get; set; } = new List<UnidadeItem>();
        public IReadOnlyList<LotacaoItem> LotacoesAtivas { get; set; } = new List<LotacaoItem>();
    }

    public sealed class OrganizacaoItem
    {
        public int IdOrganizacao { get; set; }
    }

    public sealed class UnidadeItem
    {
        public int IdUnidadeOrganizacional { get; set; }
        public string Codigo { get; set; } = string.Empty;
    }

    public sealed class LotacaoItem
    {
        public int IdLotacaoUsuario { get; set; }
        public int IdUsuario { get; set; }
        public int IdUnidadeOrganizacional { get; set; }
    }
}

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

namespace ProformaFarm.Application.Tests.Integration.Estoque;

public sealed class EstoqueReservasAtivasEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueReservasAtivasEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/reservas/ativas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_retornar_200_com_apenas_reservas_vigentes()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"/api/estoque/reservas/ativas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReservasPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("OK", body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal(setup.IdOrganizacao, body.Data!.IdOrganizacao);
        Assert.NotEmpty(body.Data.Itens);

        Assert.Contains(body.Data.Itens, x => x.DocumentoReferencia == setup.DocumentoReservaAtiva);
        Assert.All(body.Data.Itens, x => Assert.Equal("ATIVA", x.Status));
    }

    [Fact]
    public async Task Com_header_organizacao_invalido_deve_retornar_403()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "abc");

        var response = await client.GetAsync("/api/estoque/reservas/ativas");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("ORG_HEADER_INVALID", body.Code);
    }

    [Fact]
    public async Task Com_header_organizacao_sem_acesso_deve_retornar_403()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "99999999");

        var response = await client.GetAsync("/api/estoque/reservas/ativas");

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

    public sealed class ReservasPayload
    {
        public int IdOrganizacao { get; set; }
        public IReadOnlyList<ReservaItem> Itens { get; set; } = new List<ReservaItem>();
    }

    public sealed class ReservaItem
    {
        public int IdReservaEstoque { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DocumentoReferencia { get; set; }
    }
}

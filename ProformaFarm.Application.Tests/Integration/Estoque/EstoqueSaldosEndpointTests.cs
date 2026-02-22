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

public sealed class EstoqueSaldosEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueSaldosEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/saldos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_retornar_200_com_saldo_do_produto_de_teste()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"/api/estoque/saldos?idOrganizacao={setup.IdOrganizacao}&codigoProduto={setup.CodigoProduto}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SaldosPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("OK", body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal(setup.IdOrganizacao, body.Data!.IdOrganizacao);
        Assert.NotEmpty(body.Data.Itens);

        var item = body.Data.Itens.Single(x => x.CodigoProduto == setup.CodigoProduto);
        Assert.Equal(120m, item.QuantidadeDisponivel);
        Assert.Equal(20m, item.QuantidadeReservada);
        Assert.Equal(100m, item.QuantidadeLiquida);
    }

    [Fact]
    public async Task Com_header_organizacao_invalido_deve_retornar_403()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "abc");

        var response = await client.GetAsync("/api/estoque/saldos");

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

        var response = await client.GetAsync("/api/estoque/saldos");

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

    public sealed class SaldosPayload
    {
        public int IdOrganizacao { get; set; }
        public IReadOnlyList<SaldoItem> Itens { get; set; } = new List<SaldoItem>();
    }

    public sealed class SaldoItem
    {
        public int IdEstoque { get; set; }
        public string CodigoProduto { get; set; } = string.Empty;
        public decimal QuantidadeDisponivel { get; set; }
        public decimal QuantidadeReservada { get; set; }
        public decimal QuantidadeLiquida { get; set; }
    }
}

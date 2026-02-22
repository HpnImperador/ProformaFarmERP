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

namespace ProformaFarm.Application.Tests.Integration.Estoque;

public sealed class EstoqueReservasExpiracaoLoteEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueReservasExpiracaoLoteEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new { maxItens = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_expirar_reservas_do_lote()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idProduto = setup.IdProduto,
            maxItens = 50
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ExpirarReservasPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.True(body.Data!.TotalProcessadas >= 1);
        Assert.Contains(setup.IdReservaExpirada, body.Data.ReservasExpiradas);
    }

    [Fact]
    public async Task Segunda_execucao_deve_ser_idempotente_para_mesmo_filtro()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        _ = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idProduto = setup.IdProduto,
            maxItens = 50
        });

        var response = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idProduto = setup.IdProduto,
            maxItens = 50
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ExpirarReservasPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(0, body.Data!.TotalProcessadas);
    }

    [Fact]
    public async Task Com_header_organizacao_invalido_deve_retornar_403()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        client.DefaultRequestHeaders.Add("X-Organizacao-Id", "abc");

        var response = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new { maxItens = 10 });

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

    public sealed class ExpirarReservasPayload
    {
        public int IdOrganizacao { get; set; }
        public int TotalProcessadas { get; set; }
        public IReadOnlyList<int> ReservasExpiradas { get; set; } = new List<int>();
    }
}

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

public sealed class EstoqueReservasOperacaoEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueReservasOperacaoEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Criar_reserva_deve_retornar_200_com_status_ativa()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/reservas", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 5m,
            ttlMinutos = 20,
            documentoReferencia = "IT-RES-NEW"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReservaPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("ATIVA", body.Data!.Status);
        Assert.True(body.Data.IdReservaEstoque > 0);
    }

    [Fact]
    public async Task Confirmar_reserva_ativa_deve_retornar_200_com_status_confirmada()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsync($"/api/estoque/reservas/{setup.IdReservaAtiva}/confirmar", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReservaPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("CONFIRMADA", body.Data!.Status);
    }

    [Fact]
    public async Task Cancelar_reserva_ativa_deve_retornar_200_com_status_cancelada()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var criar = await client.PostAsJsonAsync("/api/estoque/reservas", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 3m,
            ttlMinutos = 20,
            documentoReferencia = "IT-RES-CANCEL"
        });
        var criarBody = await criar.Content.ReadFromJsonAsync<ApiResponse<ReservaPayload>>();
        Assert.NotNull(criarBody);
        Assert.True(criarBody!.Success);

        var response = await client.PostAsync($"/api/estoque/reservas/{criarBody.Data!.IdReservaEstoque}/cancelar", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReservaPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("CANCELADA", body.Data!.Status);
    }

    [Fact]
    public async Task Confirmar_reserva_expirada_deve_retornar_409()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsync($"/api/estoque/reservas/{setup.IdReservaExpirada}/confirmar", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("RESERVATION_EXPIRED", body.Code);
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

    public sealed class ReservaPayload
    {
        public int IdReservaEstoque { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

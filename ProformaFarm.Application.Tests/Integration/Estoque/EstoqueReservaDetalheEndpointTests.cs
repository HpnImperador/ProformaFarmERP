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

public sealed class EstoqueReservaDetalheEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueReservaDetalheEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/reservas/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_retornar_detalhe_com_trilha_de_transicoes()
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
            ttlMinutos = 30,
            documentoReferencia = "IT-DET-RES"
        });
        var criarRaw = await criar.Content.ReadAsStringAsync();
        Assert.True(criar.StatusCode == HttpStatusCode.OK, $"Create reserva failed: {(int)criar.StatusCode} {criar.StatusCode} | {criarRaw}");

        var criarBody = await criar.Content.ReadFromJsonAsync<ApiResponse<ReservaPayload>>();
        Assert.NotNull(criarBody);
        Assert.True(criarBody!.Success);
        var idReserva = criarBody.Data!.IdReservaEstoque;

        var confirmar = await client.PostAsync($"/api/estoque/reservas/{idReserva}/confirmar", content: null);
        Assert.Equal(HttpStatusCode.OK, confirmar.StatusCode);

        var response = await client.GetAsync($"/api/estoque/reservas/{idReserva}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReservaDetalhePayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(idReserva, body.Data!.Reserva.IdReservaEstoque);
        Assert.Equal("CONFIRMADA", body.Data.Reserva.Status);
        Assert.NotEmpty(body.Data.Transicoes);
        Assert.Contains(body.Data.Transicoes, x => x.TipoMovimento == "SAIDA");
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
    }

    public sealed class ReservaDetalhePayload
    {
        public ReservaItem Reserva { get; set; } = new();
        public IReadOnlyList<TransicaoItem> Transicoes { get; set; } = new List<TransicaoItem>();
    }

    public sealed class ReservaItem
    {
        public int IdReservaEstoque { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class TransicaoItem
    {
        public string TipoMovimento { get; set; } = string.Empty;
    }
}

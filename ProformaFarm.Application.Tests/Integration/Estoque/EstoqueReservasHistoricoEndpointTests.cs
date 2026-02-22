using System;
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

public sealed class EstoqueReservasHistoricoEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueReservasHistoricoEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/reservas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_filtro_por_status_deve_retornar_apenas_status_informado()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        _ = await client.PostAsJsonAsync("/api/estoque/reservas/expirar", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idProduto = setup.IdProduto,
            maxItens = 50
        });

        var response = await client.GetAsync($"/api/estoque/reservas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&status=EXPIRADA&pagina=1&tamanhoPagina=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.True(body.Data!.TotalItens >= 1);
        Assert.All(body.Data.Itens, x => Assert.Equal("EXPIRADA", x.Status));
    }

    [Fact]
    public async Task Com_token_paginacao_deve_respeitar_tamanho_e_pagina()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        _ = await client.PostAsJsonAsync("/api/estoque/reservas", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 2m,
            ttlMinutos = 30,
            documentoReferencia = "IT-HIST-001"
        });

        _ = await client.PostAsJsonAsync("/api/estoque/reservas", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 2m,
            ttlMinutos = 30,
            documentoReferencia = "IT-HIST-002"
        });

        var responsePage1 = await client.GetAsync($"/api/estoque/reservas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&pagina=1&tamanhoPagina=1");
        var responsePage2 = await client.GetAsync($"/api/estoque/reservas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&pagina=2&tamanhoPagina=1");

        Assert.Equal(HttpStatusCode.OK, responsePage1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responsePage2.StatusCode);

        var bodyPage1 = await responsePage1.Content.ReadFromJsonAsync<ApiResponse<HistoricoPayload>>();
        var bodyPage2 = await responsePage2.Content.ReadFromJsonAsync<ApiResponse<HistoricoPayload>>();

        Assert.NotNull(bodyPage1);
        Assert.NotNull(bodyPage2);
        Assert.True(bodyPage1!.Success);
        Assert.True(bodyPage2!.Success);
        Assert.NotNull(bodyPage1.Data);
        Assert.NotNull(bodyPage2.Data);
        Assert.Single(bodyPage1.Data!.Itens);
        Assert.Single(bodyPage2.Data!.Itens);
        Assert.True(bodyPage1.Data.TotalItens >= 2);
        Assert.NotEqual(bodyPage1.Data.Itens[0].IdReservaEstoque, bodyPage2.Data.Itens[0].IdReservaEstoque);
    }

    [Fact]
    public async Task Com_token_filtro_periodo_deve_retornar_itens_no_intervalo()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var dataDe = DateTime.UtcNow.AddHours(-2).ToString("O");
        var dataAte = DateTime.UtcNow.AddHours(2).ToString("O");

        var response = await client.GetAsync(
            $"/api/estoque/reservas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&dataDe={Uri.EscapeDataString(dataDe)}&dataAte={Uri.EscapeDataString(dataAte)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.True(body.Data!.TotalItens >= 1);
    }

    [Fact]
    public async Task Com_token_periodo_sem_intersecao_deve_retornar_zero()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var dataDe = DateTime.UtcNow.AddDays(-10).ToString("O");
        var dataAte = DateTime.UtcNow.AddDays(-9).ToString("O");

        var response = await client.GetAsync(
            $"/api/estoque/reservas?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&dataDe={Uri.EscapeDataString(dataDe)}&dataAte={Uri.EscapeDataString(dataAte)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(0, body.Data!.TotalItens);
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

    public sealed class HistoricoPayload
    {
        public int IdOrganizacao { get; set; }
        public int Pagina { get; set; }
        public int TamanhoPagina { get; set; }
        public int TotalItens { get; set; }
        public int TotalPaginas { get; set; }
        public IReadOnlyList<HistoricoItem> Itens { get; set; } = new List<HistoricoItem>();
    }

    public sealed class HistoricoItem
    {
        public int IdReservaEstoque { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

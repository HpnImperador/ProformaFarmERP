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

public sealed class EstoqueMovimentacoesHistoricoEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueMovimentacoesHistoricoEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/movimentacoes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_filtro_por_tipo_deve_retornar_apenas_tipo_informado()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        var docEntrada = $"IT-HIST-MOV-E-{Guid.NewGuid():N}";
        var docSaida = $"IT-HIST-MOV-S-{Guid.NewGuid():N}";

        _ = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 3m,
            documentoReferencia = docEntrada
        });

        _ = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 2m,
            documentoReferencia = docSaida
        });

        var response = await client.GetAsync($"/api/estoque/movimentacoes?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&tipoMovimento=SAIDA&pagina=1&tamanhoPagina=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MovimentacoesPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.True(body.Data!.TotalItens >= 1);
        Assert.All(body.Data.Itens, x => Assert.Equal("SAIDA", x.TipoMovimento));
        Assert.Contains(body.Data.Itens, x => x.DocumentoReferencia == docSaida);
    }

    [Fact]
    public async Task Com_token_paginacao_deve_respeitar_pagina_e_tamanho()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        _ = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 1m,
            documentoReferencia = $"IT-HIST-PG-1-{Guid.NewGuid():N}"
        });

        _ = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 1m,
            documentoReferencia = $"IT-HIST-PG-2-{Guid.NewGuid():N}"
        });

        var responsePage1 = await client.GetAsync($"/api/estoque/movimentacoes?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&pagina=1&tamanhoPagina=1");
        var responsePage2 = await client.GetAsync($"/api/estoque/movimentacoes?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&pagina=2&tamanhoPagina=1");

        Assert.Equal(HttpStatusCode.OK, responsePage1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responsePage2.StatusCode);

        var bodyPage1 = await responsePage1.Content.ReadFromJsonAsync<ApiResponse<MovimentacoesPayload>>();
        var bodyPage2 = await responsePage2.Content.ReadFromJsonAsync<ApiResponse<MovimentacoesPayload>>();

        Assert.NotNull(bodyPage1);
        Assert.NotNull(bodyPage2);
        Assert.True(bodyPage1!.Success);
        Assert.True(bodyPage2!.Success);
        Assert.NotNull(bodyPage1.Data);
        Assert.NotNull(bodyPage2.Data);
        Assert.Single(bodyPage1.Data!.Itens);
        Assert.Single(bodyPage2.Data!.Itens);
        Assert.True(bodyPage1.Data.TotalItens >= 2);
        Assert.NotEqual(bodyPage1.Data.Itens[0].IdMovimentacaoEstoque, bodyPage2.Data.Itens[0].IdMovimentacaoEstoque);
    }

    [Fact]
    public async Task Com_token_periodo_invalido_deve_retornar_400()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var dataDe = DateTime.UtcNow.ToString("O");
        var dataAte = DateTime.UtcNow.AddDays(-1).ToString("O");

        var response = await client.GetAsync($"/api/estoque/movimentacoes?idOrganizacao={setup.IdOrganizacao}&dataDe={Uri.EscapeDataString(dataDe)}&dataAte={Uri.EscapeDataString(dataAte)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("VALIDATION_ERROR", body.Code);
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

    public sealed class MovimentacoesPayload
    {
        public int IdOrganizacao { get; set; }
        public int Pagina { get; set; }
        public int TamanhoPagina { get; set; }
        public int TotalItens { get; set; }
        public int TotalPaginas { get; set; }
        public IReadOnlyList<MovimentacaoItem> Itens { get; set; } = new List<MovimentacaoItem>();
    }

    public sealed class MovimentacaoItem
    {
        public int IdMovimentacaoEstoque { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public string? DocumentoReferencia { get; set; }
    }
}

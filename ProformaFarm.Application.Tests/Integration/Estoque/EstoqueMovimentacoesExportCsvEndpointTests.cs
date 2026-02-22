using System;
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

public sealed class EstoqueMovimentacoesExportCsvEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueMovimentacoesExportCsvEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/movimentacoes/exportar-csv");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_retornar_csv_com_cabecalho_e_dados()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        var documento = $"IT-MOV-CSV-{Guid.NewGuid():N}";

        _ = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 5m,
            documentoReferencia = documento
        });

        var response = await client.GetAsync($"/api/estoque/movimentacoes/exportar-csv?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&tipoMovimento=ENTRADA&limite=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.True(response.Headers.Contains("X-Export-Format"));
        Assert.True(response.Headers.Contains("X-Export-Resource"));
        Assert.True(response.Headers.Contains("X-Export-GeneratedAtUtc"));
        Assert.True(response.Headers.Contains("X-Export-FileName"));
        Assert.Contains("csv", string.Join(",", response.Headers.GetValues("X-Export-Format")));
        Assert.Contains("movimentacoes", string.Join(",", response.Headers.GetValues("X-Export-Resource")));

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("IdMovimentacaoEstoque,IdOrganizacao", csv);
        Assert.Contains(setup.CodigoProduto, csv);
        Assert.Contains("ENTRADA", csv);
        Assert.Contains(documento, csv);
    }

    [Fact]
    public async Task Com_token_periodo_invalido_deve_retornar_400()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        var dataDe = DateTime.UtcNow.ToString("O");
        var dataAte = DateTime.UtcNow.AddDays(-1).ToString("O");

        var response = await client.GetAsync($"/api/estoque/movimentacoes/exportar-csv?idOrganizacao={setup.IdOrganizacao}&dataDe={Uri.EscapeDataString(dataDe)}&dataAte={Uri.EscapeDataString(dataAte)}");

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
}

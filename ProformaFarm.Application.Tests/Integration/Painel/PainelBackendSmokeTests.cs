using System;
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

namespace ProformaFarm.Application.Tests.Integration.Painel;

public sealed class PainelBackendSmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PainelBackendSmokeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Arquivos_estaticos_do_painel_devem_ser_servidos()
    {
        using var client = _factory.CreateClient();

        var rootResponse = await client.GetAsync("/");
        var painelResponse = await client.GetAsync("/painel/");
        var mainJsResponse = await client.GetAsync("/painel/js/main.js");

        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, painelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mainJsResponse.StatusCode);

        var rootHtml = await rootResponse.Content.ReadAsStringAsync();
        var painelHtml = await painelResponse.Content.ReadAsStringAsync();
        var mainJs = await mainJsResponse.Content.ReadAsStringAsync();

        Assert.Contains("/painel/", rootHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/painel/js/main.js", painelHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bindTransacoes", mainJs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fluxo_basico_do_painel_com_login_real_deve_funcionar()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var contextoResponse = await client.GetAsync("/api/organizacao/contexto");
        Assert.Equal(HttpStatusCode.OK, contextoResponse.StatusCode);

        var saldosResponse = await client.GetAsync($"/api/estoque/saldos?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}");
        Assert.Equal(HttpStatusCode.OK, saldosResponse.StatusCode);

        var entradaResponse = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 0m,
            documentoReferencia = $"IT-PAINEL-ENT-INVALID-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, entradaResponse.StatusCode);

        var reservaResponse = await client.PostAsJsonAsync("/api/estoque/reservas", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 0m,
            ttlMinutos = 10,
            documentoReferencia = $"IT-PAINEL-RES-INVALID-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, reservaResponse.StatusCode);

        var exportResponse = await client.GetAsync($"/api/estoque/saldos/exportar-csv?idOrganizacao={setup.IdOrganizacao}&limite=10");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("csv", exportResponse.Headers.GetValues("X-Export-Format").Single());
        Assert.Equal("saldos", exportResponse.Headers.GetValues("X-Export-Resource").Single());
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

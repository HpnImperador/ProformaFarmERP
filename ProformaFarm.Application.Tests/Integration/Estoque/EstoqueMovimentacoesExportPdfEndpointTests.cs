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

public sealed class EstoqueMovimentacoesExportPdfEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueMovimentacoesExportPdfEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/estoque/movimentacoes/exportar-pdf");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Com_token_deve_retornar_pdf_com_headers_padrao()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"/api/estoque/movimentacoes/exportar-pdf?idOrganizacao={setup.IdOrganizacao}&idProduto={setup.IdProduto}&limite=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);

        Assert.True(response.Headers.Contains("X-Export-Format"));
        Assert.True(response.Headers.Contains("X-Export-Resource"));
        Assert.True(response.Headers.Contains("X-Export-GeneratedAtUtc"));
        Assert.True(response.Headers.Contains("X-Export-FileName"));
        Assert.Contains("pdf", string.Join(",", response.Headers.GetValues("X-Export-Format")));
        Assert.Contains("movimentacoes", string.Join(",", response.Headers.GetValues("X-Export-Resource")));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Painel;

public sealed class PainelContratoIntegracaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly string[] RequiredExportHeaders =
    {
        "X-Export-Format",
        "X-Export-Resource",
        "X-Export-GeneratedAtUtc",
        "X-Export-FileName"
    };

    private readonly CustomWebApplicationFactory _factory;

    public PainelContratoIntegracaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Organizacao_contexto_deve_respeitar_contrato_api_response()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync("/api/organizacao/contexto");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("OK", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("data", out var data));
        Assert.True(data.GetProperty("idUsuario").GetInt32() > 0);
        Assert.True(data.GetProperty("idOrganizacao").GetInt32() > 0);
        Assert.True(data.GetProperty("idUnidade").GetInt32() > 0);
    }

    [Theory]
    [InlineData("/api/estoque/saldos")]
    [InlineData("/api/estoque/reservas/ativas")]
    [InlineData("/api/estoque/movimentacoes?pagina=1&tamanhoPagina=10")]
    public async Task Leituras_do_painel_devem_respeitar_envelope_api_response(string path)
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);
        var url = AppendIdOrganizacao(path, setup.IdOrganizacao);

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("OK", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
    }

    [Theory]
    [InlineData("/api/estoque/saldos/exportar-csv", "csv", "saldos")]
    [InlineData("/api/estoque/saldos/exportar-pdf", "pdf", "saldos")]
    [InlineData("/api/estoque/reservas/exportar-csv", "csv", "reservas")]
    [InlineData("/api/estoque/reservas/exportar-pdf", "pdf", "reservas")]
    [InlineData("/api/estoque/movimentacoes/exportar-csv", "csv", "movimentacoes")]
    [InlineData("/api/estoque/movimentacoes/exportar-pdf", "pdf", "movimentacoes")]
    public async Task Exportacoes_devem_respeitar_headers_de_contrato(string path, string formato, string recurso)
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.GetAsync($"{path}?idOrganizacao={setup.IdOrganizacao}&limite=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        foreach (var header in RequiredExportHeaders)
            Assert.True(response.Headers.Contains(header), $"Header obrigatorio ausente: {header}");

        Assert.Equal(formato, response.Headers.GetValues("X-Export-Format").Single());
        Assert.Equal(recurso, response.Headers.GetValues("X-Export-Resource").Single());
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Export-GeneratedAtUtc").Single()));

        var fileName = response.Headers.GetValues("X-Export-FileName").Single();
        Assert.EndsWith("." + formato, fileName, StringComparison.OrdinalIgnoreCase);

        Assert.True(response.Headers.Contains("Access-Control-Expose-Headers"));
        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        foreach (var header in RequiredExportHeaders)
            Assert.Contains(header, exposed, StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendIdOrganizacao(string path, int idOrganizacao)
    {
        var separator = path.Contains('?') ? "&" : "?";
        return $"{path}{separator}idOrganizacao={idOrganizacao}";
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

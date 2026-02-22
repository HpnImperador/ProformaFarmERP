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

public sealed class EstoqueMovimentacoesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EstoqueMovimentacoesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Entrada_sem_token_deve_retornar_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idUnidadeOrganizacional = 1,
            idProduto = 1,
            quantidade = 1m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Entrada_com_token_deve_atualizar_quantidade_disponivel()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/entrada", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 10m,
            documentoReferencia = "IT-MOV-ENTRADA"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MovimentacaoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("OK", body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal("ENTRADA", body.Data!.TipoMovimento);
        Assert.Equal(120m, body.Data.QuantidadeDisponivelAnterior);
        Assert.Equal(130m, body.Data.QuantidadeDisponivelAtual);
    }

    [Fact]
    public async Task Saida_com_token_deve_atualizar_quantidade_disponivel()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 30m,
            documentoReferencia = "IT-MOV-SAIDA"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MovimentacaoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("SAIDA", body.Data!.TipoMovimento);
        Assert.Equal(120m, body.Data.QuantidadeDisponivelAnterior);
        Assert.Equal(90m, body.Data.QuantidadeDisponivelAtual);
    }

    [Fact]
    public async Task Saida_sem_saldo_liquido_suficiente_deve_retornar_409()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/saida", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidade = 101m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("INSUFFICIENT_STOCK", body.Code);
    }

    [Fact]
    public async Task Ajuste_com_token_deve_redefinir_quantidade_disponivel()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/ajuste", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidadeDisponivel = 150m,
            documentoReferencia = "IT-MOV-AJUSTE"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MovimentacaoPayload>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("AJUSTE", body.Data!.TipoMovimento);
        Assert.Equal(120m, body.Data.QuantidadeDisponivelAnterior);
        Assert.Equal(150m, body.Data.QuantidadeDisponivelAtual);
        Assert.Equal(30m, body.Data.QuantidadeMovimento);
    }

    [Fact]
    public async Task Ajuste_abaixo_da_reserva_deve_retornar_409()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(setup.Login, setup.Senha);

        var response = await client.PostAsJsonAsync("/api/estoque/movimentacoes/ajuste", new
        {
            idOrganizacao = setup.IdOrganizacao,
            idUnidadeOrganizacional = setup.IdUnidade,
            idProduto = setup.IdProduto,
            idLote = setup.IdLote,
            quantidadeDisponivel = 10m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("ADJUSTMENT_BELOW_RESERVED", body.Code);
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

    public sealed class MovimentacaoPayload
    {
        public int IdMovimentacaoEstoque { get; set; }
        public int IdEstoque { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public decimal QuantidadeMovimento { get; set; }
        public decimal QuantidadeDisponivelAnterior { get; set; }
        public decimal QuantidadeDisponivelAtual { get; set; }
    }
}

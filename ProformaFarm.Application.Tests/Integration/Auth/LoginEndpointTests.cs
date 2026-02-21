using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Auth;

public class LoginEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    // ✅ O construtor deve receber EXATAMENTE o tipo do fixture declarado na classe
    public LoginEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Body_vazio_deve_retornar_400_VALIDATION_ERROR_com_Login_e_Senha()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, string[]>>>();

        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("VALIDATION_ERROR", body.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        Assert.NotNull(body.Data);

        Assert.True(body.Data!.ContainsKey("Login"));
        Assert.True(body.Data.ContainsKey("Senha"));
    }

    [Fact]
    public async Task Sem_senha_deve_retornar_400_VALIDATION_ERROR_apenas_com_Senha()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { login = "admin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, string[]>>>();

        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("VALIDATION_ERROR", body.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        Assert.NotNull(body.Data);

        Assert.False(body.Data!.ContainsKey("Login"));
        Assert.True(body.Data.ContainsKey("Senha"));
    }
}

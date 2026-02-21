using FluentValidation.TestHelper;
using Microsoft.IdentityModel.Tokens;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Validators.Auth;

using Xunit;

namespace ProformaFarm.Tests.Validators.Auth;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Deve_ter_erro_quando_Login_vazio()
    {
        var model = new LoginRequest
        {
            Login = "",
            Senha = "123"
        };

        var result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Login);
    }

    [Fact]
    public void Deve_ter_erro_quando_Senha_vazia()
    {
        var model = new LoginRequest
        {
            Login = "admin",
            Senha = ""
        };

        var result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Senha);
    }

    [Fact]
    public void Deve_passar_quando_valido()
    {
        var model = new LoginRequest
        {
            Login = "admin",
            Senha = "123"
        };

        var result = _validator.TestValidate(model);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

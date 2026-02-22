using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ProformaFarm.API.Filters;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Interfaces.Context;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Interfaces.Export;
using ProformaFarm.Application.Options;
using ProformaFarm.Application.Services.Auth;
using ProformaFarm.Application.Services.Export;
using ProformaFarm.Application.Services.Security;
using ProformaFarm.Infrastructure.Context;
using ProformaFarm.Infrastructure.Data;
using ProformaFarm.Infrastructure.Repositories.Auth;
using ProformaFarm.Middlewares;
using System;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Config *-*-*-*-
// -------------------------

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nÃ£o configurada.");

builder.Services.AddSingleton<ISqlConnectionFactory>(
    new SqlConnectionFactory(connectionString)
);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOrgContext, OrgContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' nÃ£o encontrada.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer nÃ£o encontrado.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience nÃ£o encontrado.");

var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey nÃ£o encontrado.");

// -------------------------
// MVC
// -------------------------
builder.Services.AddControllers();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ModelStateValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // MantÃ©m desligado o 400 automÃ¡tico do ApiController
        options.SuppressModelStateInvalidFilter = true;
    });

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<
    ProformaFarm.Application.DTOs.Auth.LoginRequest>();

// -------------------------
// Data Access (Dapper)
// -------------------------
//builder.Services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(builder.Configuration));

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // DESLIGA o 400 automÃ¡tico do [ApiController]
        options.SuppressModelStateInvalidFilter = true;

        // Temporariamente apenas retorna BadRequest padrÃ£o.
        // Nos prÃ³ximos passos substituiremos pelo ApiResponse.
        options.InvalidModelStateResponseFactory = context =>
        {
            return new BadRequestResult();
        };
    });

// -------------------------
// DI - Application Services
// -------------------------
IServiceCollection serviceCollection = builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
//builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<JwtTokenService>();


builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey nÃ£o configurado.")
    .ValidateOnStart();

// -------------------------
// DI - Repositories (Application -> Infrastructure)
// -------------------------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// -------------------------
// Auth/JWT
// -------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

// -------------------------
// Swagger
// -------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Exception middleware primeiro (governanÃ§a global)


if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}


app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<OrgContextEnforcementMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Plano (pseudocÃ³digo):
// 1) Garantir que exista um tipo chamado `Program` no namespace `ProformaFarm`.
// 2) Criar uma classe parcial pÃºblica `Program` vazia para que o `WebApplicationFactory<ProformaFarm.Program>` consiga referenciÃ¡-la.
// 3) Se o projeto usar top-level statements no arquivo de entrada, marcar a classe como `partial` permite que o compilador una o tipo implÃ­cito com esta definiÃ§Ã£o.
// 4) NÃ£o alterar outras partes do cÃ³digo; este arquivo apenas expÃµe o tipo esperado pelos testes.

namespace ProformaFarm
{
    /// <summary>
    /// DeclaraÃ§Ã£o mÃ­nima de `Program` para que `WebApplicationFactory<ProformaFarm.Program>` compile.
    /// Se a aplicaÃ§Ã£o principal usar top-level statements, manter esta classe como `partial`
    /// permite combinar com a lÃ³gica real em outro arquivo sem duplicar cÃ³digo.
    /// </summary>
    public partial class Program
    {
    }
}



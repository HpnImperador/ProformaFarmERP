using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Services.Auth;
using ProformaFarm.Application.Services.Security;
using ProformaFarm.Infrastructure.Data;
using ProformaFarm.Infrastructure.Repositories.Auth;
using ProformaFarm.Middlewares;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Config
// -------------------------
var connString = builder.Configuration.GetConnectionString("ProformaFarm")
    ?? throw new InvalidOperationException("ConnectionString 'ProformaFarm' não encontrada.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer não encontrado.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience não encontrado.");

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não encontrado.");

// -------------------------
// MVC
// -------------------------
builder.Services.AddControllers();

// -------------------------
// Data Access (Dapper)
// -------------------------
builder.Services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(builder.Configuration));

// -------------------------
// DI - Application Services
// -------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

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

// Exception middleware primeiro (governança global)
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

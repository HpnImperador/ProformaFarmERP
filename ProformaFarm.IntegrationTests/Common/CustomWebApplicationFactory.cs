using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace ProformaFarm.IntegrationTests.Common;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<ProformaFarm.Program>
{
    private readonly string _testConnectionString;

    public CustomWebApplicationFactory()
        : this("Server=localhost;Database=ProformaFarm_Test;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;")
    {
    }

    private CustomWebApplicationFactory(string testConnectionString)
        => _testConnectionString = testConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _testConnectionString
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(_ => { });
    }
}

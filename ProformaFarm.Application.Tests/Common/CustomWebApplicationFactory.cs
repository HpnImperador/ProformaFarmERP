using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProformaFarm.Application.Tests.Common;

public class CustomWebApplicationFactory : WebApplicationFactory<ProformaFarm.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

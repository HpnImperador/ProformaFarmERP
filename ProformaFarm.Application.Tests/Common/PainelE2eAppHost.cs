using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Tests.Common;

public sealed class PainelE2eAppHost : IAsyncDisposable
{
    private readonly Process _process;
    public string BaseUrl { get; }

    private PainelE2eAppHost(Process process, string baseUrl)
    {
        _process = process;
        BaseUrl = baseUrl;
    }

    public static async Task<PainelE2eAppHost> StartAsync()
    {
        var root = ResolveRepositoryRoot();
        var appProject = Path.Combine(root, "ProformaFarm", "ProformaFarm.csproj");
        var baseUrl = "http://127.0.0.1:5099";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{appProject}\" --urls {baseUrl}",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Não foi possível iniciar o processo da API para E2E.");

        var host = new PainelE2eAppHost(process, baseUrl);
        await host.WaitUntilHealthyAsync();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignora falhas de encerramento forçado.
        }

        await Task.Delay(120);
        _process.Dispose();
    }

    private async Task WaitUntilHealthyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var timeoutAt = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (_process.HasExited)
            {
                var stdErr = await _process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Processo da API encerrou antes do health check. STDERR: {stdErr}");
            }

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/painel/");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Aguarda próximo retry.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Timeout aguardando a API E2E responder em /painel/.");
    }

    private static string ResolveRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "ProformaFarmERP.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada para execução dos testes E2E.");
    }
}

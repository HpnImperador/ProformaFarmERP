using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ProformaFarm.Application.Tests.Common;
using Xunit;

namespace ProformaFarm.Application.Tests.Integration.Painel;

[Trait("Category", "E2E")]
public sealed class PainelBackendE2ePlaywrightTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PainelBackendE2ePlaywrightTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Painel_deve_validar_login_consulta_renderizacao_e_download_csv()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        await ExecuteWithArtifactsAsync(
            testName: nameof(Painel_deve_validar_login_consulta_renderizacao_e_download_csv),
            testBody: async (page, hostBaseUrl) =>
            {
                await page.FillAsync("#baseUrl", hostBaseUrl);
                await page.FillAsync("#login", setup.Login);
                await page.FillAsync("#senha", setup.Senha);
                await page.ClickAsync("#btnLogin");
                await ExpectContainsAsync(page, "#authStatus", "Expira em:");

                await page.FillAsync("#idOrganizacao", setup.IdOrganizacao.ToString());
                await page.FillAsync("#idUnidade", setup.IdUnidade.ToString());
                await page.FillAsync("#idProduto", setup.IdProduto.ToString());
                await page.FillAsync("#idLote", setup.IdLote.ToString());
                await page.SelectOptionAsync("#readResource", "saldos");
                await page.ClickAsync("#btnEstoqueConsultar");
                await ExpectContainsAsync(page, "#readStatus", "Consulta concluida");
                await ExpectContainsAsync(page, "#readTableWrap", setup.CodigoProduto);

                await page.SelectOptionAsync("#exportResource", "saldos");
                await page.SelectOptionAsync("#format", "csv");
                await page.FillAsync("#limite", "20");

                var download = await page.RunAndWaitForDownloadAsync(async () =>
                {
                    await page.ClickAsync("#btnExport");
                });

                var downloadFile = Path.Combine(Path.GetTempPath(), $"pf_e2e_{Guid.NewGuid():N}.csv");
                await download.SaveAsAsync(downloadFile);
                var fileInfo = new FileInfo(downloadFile);
                Assert.True(fileInfo.Exists);
                Assert.True(fileInfo.Length > 0);
                Assert.EndsWith(".csv", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task Painel_deve_executar_entrada_saida_e_refletir_imediatamente_na_tabela()
    {
        var setup = await EstoqueTestDataSetup.EnsureAsync(_factory);
        await ExecuteWithArtifactsAsync(
            testName: nameof(Painel_deve_executar_entrada_saida_e_refletir_imediatamente_na_tabela),
            testBody: async (page, hostBaseUrl) =>
            {
                await page.FillAsync("#baseUrl", hostBaseUrl);
                await page.FillAsync("#login", setup.Login);
                await page.FillAsync("#senha", setup.Senha);
                await page.ClickAsync("#btnLogin");
                await ExpectContainsAsync(page, "#authStatus", "Expira em:");

                await page.FillAsync("#idOrganizacao", setup.IdOrganizacao.ToString());
                await page.FillAsync("#idUnidade", setup.IdUnidade.ToString());
                await page.FillAsync("#idProduto", setup.IdProduto.ToString());
                await page.FillAsync("#idLote", setup.IdLote.ToString());
                await page.SelectOptionAsync("#readResource", "movimentacoes");
                await page.FillAsync("#pagina", "1");
                await page.FillAsync("#tamanhoPagina", "20");

                var docEntrada = $"IT-E2E-ENTRADA-{Guid.NewGuid():N}".ToUpperInvariant();
                await page.SelectOptionAsync("#movAction", "entrada");
                await page.FillAsync("#movQuantidade", "1");
                await page.FillAsync("#movDocumento", docEntrada);
                await page.FillAsync("#tipoMovimento", "ENTRADA");
                await page.ClickAsync("#btnMovExecutar");

                await ExpectContainsAsync(page, "#txStatus", "Movimentacao concluida");
                await ExpectContainsAsync(page, "#readStatus", "Consulta concluida");
                await ExpectContainsAsync(page, "#readTableWrap", docEntrada);

                var docSaida = $"IT-E2E-SAIDA-{Guid.NewGuid():N}".ToUpperInvariant();
                await page.SelectOptionAsync("#movAction", "saida");
                await page.FillAsync("#movQuantidade", "1");
                await page.FillAsync("#movDocumento", docSaida);
                await page.FillAsync("#tipoMovimento", "SAIDA");
                await page.ClickAsync("#btnMovExecutar");

                await ExpectContainsAsync(page, "#txStatus", "Movimentacao concluida");
                await ExpectContainsAsync(page, "#readStatus", "Consulta concluida");
                await ExpectContainsAsync(page, "#readTableWrap", docSaida);
            });
    }

    private async Task ExecuteWithArtifactsAsync(string testName, Func<IPage, string, Task> testBody)
    {
        var safeName = string.Concat(testName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var artifactsDir = Path.Combine("TestResults", "e2e", safeName);
        Directory.CreateDirectory(artifactsDir);

        await using var host = await PainelE2eAppHost.StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "msedge"
        });

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            RecordVideoDir = artifactsDir
        });

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync($"{host.BaseUrl}/painel/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        try
        {
            await testBody(page, host.BaseUrl);
        }
        catch
        {
            var screenshotPath = Path.Combine(artifactsDir, "failure.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            throw;
        }
        finally
        {
            var tracePath = Path.Combine(artifactsDir, "trace.zip");
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        }
    }

    private static async Task ExpectContainsAsync(IPage page, string selector, string fragment)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < timeoutAt)
        {
            var text = await page.TextContentAsync(selector) ?? string.Empty;
            if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return;
            await Task.Delay(200);
        }

        var last = await page.TextContentAsync(selector) ?? string.Empty;
        throw new Xunit.Sdk.XunitException($"Elemento {selector} nao contem '{fragment}'. Ultimo valor: {last}");
    }
}

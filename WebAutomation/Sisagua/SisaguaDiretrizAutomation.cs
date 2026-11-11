using Microsoft.Playwright;

namespace WebAutomation
{
    public class SisaguaDiretrizResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> DownloadedFiles { get; set; } = [];
    }

    public class SisaguaDiretrizAutomation
    {
        private const string UrlLogin = "https://sisagua.saude.gov.br/sisagua/paginaExterna.jsf";

        private static readonly List<string> Parameters =
        [
            "Cloro Residual Livre",
            "Turbidez",
            "Coliformes Totais/E. coli"
        ];

        public async Task<SisaguaDiretrizResult> DownloadMonthlyReportsAsync(
            string email,
            string password,
            string destinationFolder,
            List<int> years,
            IProgress<string>? progress = null)
        {
            var result = new SisaguaDiretrizResult();

            try
            {
                progress?.Report("Iniciando navegador...");
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions { Headless = false });

                var context = await browser.NewContextAsync(
                    new BrowserNewContextOptions { AcceptDownloads = true });
                var page = await context.NewPageAsync();

                // Login
                progress?.Report("Fazendo login no Sisagua...");
                await page.GotoAsync(UrlLogin);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.ClickAsync("#j_idt35\\:btnEntrar");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.FillAsync("#email", email);
                await page.FillAsync("#senha", password);
                await page.ClickAsync("button:has-text('Entrar')");
                await page.WaitForURLAsync(url => !url.Contains("paginaExterna"),
                    new PageWaitForURLOptions { Timeout = 60000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Navega para diretriz
                progress?.Report("Navegando para relatórios...");
                var reportsMenu = page.Locator("span:has-text('RELATÓRIOS')");
                await reportsMenu.HoverAsync();
                await Task.Delay(1000);

                var linkDiretriz = page.Locator(
                    "a.ui-menuitem-link[href*='relDiretrizNacionalParametrosBasicos.jsf']");
                string? href = await linkDiretriz.GetAttributeAsync("href");
                string fullUrl = href!.StartsWith("http")
                    ? href : "https://sisagua.saude.gov.br" + href;
                await page.GotoAsync(fullUrl);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Abrangência
                await page.ClickAsync("#idAbrangencia_label");
                await Task.Delay(500);
                await page.ClickAsync("li[data-label='Unidade Federativa']");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Tipo Mensal
                var radioMensal = page.Locator("input[type='radio'][value='MENSAL']");
                await radioMensal.EvaluateAsync("el => el.click()");
                await Task.Delay(3000);

                // Percorre anos → parâmetros
                foreach (var year in years)
                {
                    progress?.Report($"Selecionando ano {year}...");
                    await page.ClickAsync("#j_idt129_label");
                    await Task.Delay(500);
                    await page.ClickAsync($"li[data-label='{year}']");
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await Task.Delay(2000);

                    int total = Parameters.Count;
                    for (int i = 0; i < total; i++)
                    {
                        var parameter = Parameters[i];
                        progress?.Report($"[{year}] Baixando {parameter} ({i + 1}/{total})...");

                        await page.ClickAsync("#j_idt145_label");
                        await Task.Delay(500);
                        var itemParameter = page.Locator($"li[data-label='{parameter}']");
                        await itemParameter.EvaluateAsync("el => el.click()");
                        await Task.Delay(500);

                        var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions
                        {
                            Timeout = 120000
                        });
                        await page.EvaluateAsync(
                            "document.getElementById('gerarRelatorioExcel').click()");
                        var download = await downloadTask;

                        string safeParameter = string.Concat(
                            parameter.Split(Path.GetInvalidFileNameChars()));
                        string fileName =
                            $"Relatório Diretriz Nacional(MESES) - {safeParameter} - {year}.xls";
                        string destination = Path.Combine(destinationFolder, fileName);

                        if (File.Exists(destination)) File.Delete(destination);
                        await download.SaveAsAsync(destination);
                        result.DownloadedFiles.Add(destination);
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
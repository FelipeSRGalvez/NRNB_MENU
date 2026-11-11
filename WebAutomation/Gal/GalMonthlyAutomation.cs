using Microsoft.Playwright;

namespace WebAutomation
{
    public class GalMonthlyAutomation
    {
        public async Task<bool> BaixarRelatorioMensalAsync(
            string username,
            string password,
            string module,
            string laboratory,
            string downloadFolder,
            string reference,
            string startDate,
            string endDate,
            string ibgeCode,
            IProgress<string> progress)
        {
            // Falha logo, antes de abrir o navegador: sem o solver não há como fazer login.
            if (!File.Exists(CaptchaSolverPath))
            {
                progress.Report($"Falha: captcha_solver.exe não encontrado em {AppDomain.CurrentDomain.BaseDirectory}");
                return false;
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[]
                {
                    "--ignore-certificate-errors",
                    "--ignore-ssl-errors",
                    "--allow-insecure-localhost"
                }
            });

            var context = await browser.NewContextAsync();

            // Suprime window.print() no nível do contexto, para que qualquer aba aberta por window.open()
            // já receba a substituição antes de o onload dela disparar.
            await context.AddInitScriptAsync("window.print = () => {};");

            var page = await context.NewPageAsync();

            bool loginSucceeded = false;
            int attempt = 1;
            const int maxAttempts = 100;

            while (attempt <= maxAttempts && !loginSucceeded)
            {
                try
                {
                    progress.Report($"Tentativa {attempt} — carregando página...");
                    await page.GotoAsync("https://gal.rondonia.sus.gov.br/");

                    await page.WaitForTimeoutAsync(200);
                    await page.FillAsync("[name='login']", username);
                    await page.FillAsync("[name='senha']", password);

                    await page.ClickAsync("[name='modulo']");
                    await page.WaitForTimeoutAsync(300);
                    await page.Keyboard.TypeAsync(module);
                    await page.WaitForTimeoutAsync(1000);
                    await page.Keyboard.PressAsync("Enter");
                    await page.WaitForTimeoutAsync(2500);

                    await page.Keyboard.PressAsync("Tab");
                    await page.WaitForTimeoutAsync(500);
                    await page.Keyboard.PressAsync("Control+A");
                    await page.Keyboard.PressAsync("Backspace");
                    await page.WaitForTimeoutAsync(300);
                    await page.Keyboard.TypeAsync(laboratory);
                    await page.WaitForTimeoutAsync(800);
                    await page.Keyboard.PressAsync("Enter");
                    await page.WaitForTimeoutAsync(1000);
                    progress.Report($"Tentativa {attempt} — clicando em Entrar...");

                    await page.ClickAsync("//button[text()='Entrar']");

                    var captchaImg = await page.WaitForSelectorAsync("img[src*='captcha']");
                    await page.WaitForTimeoutAsync(1000);

                    var imgBytes = await captchaImg!.ScreenshotAsync();

                    string captchaText = await SolveCaptchaAsync(imgBytes, progress);
                    progress.Report($"Tentativa {attempt} — captcha lido: {captchaText}");

                    await page.ClickAsync("[name='ext-comp-1031']");
                    await page.WaitForTimeoutAsync(300);
                    await page.Keyboard.TypeAsync(captchaText);
                    await page.WaitForTimeoutAsync(500);

                    await page.ClickAsync("xpath=//button[text()='Confirmar']");
                    await page.WaitForTimeoutAsync(5000);

                    var loginField = await page.QuerySelectorAsync("[name='login']");
                    if (loginField == null)
                    {
                        progress.Report("Login realizado com sucesso.");
                        loginSucceeded = true;
                    }
                    else
                    {
                        progress.Report($"Captcha incorreto — tentativa {attempt}");
                        attempt++;

                        await page.GotoAsync("about:blank");
                        await page.WaitForTimeoutAsync(500);
                    }
                }
                catch (CaptchaSolverException ex)
                {
                    // Falha do solver não é captcha errado: tentar de novo só repetiria a falha.
                    progress.Report($"Falha no leitor de captcha: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    // Janela do navegador fechada ou travada: qualquer nova chamada nesta página
                    // falharia de novo, então para aqui em vez de tentar novamente.
                    if (page.IsClosed || !browser.IsConnected)
                    {
                        progress.Report("Falha: o navegador do GAL foi fechado ou parou de responder durante o login.");
                        return false;
                    }

                    progress.Report($"Erro na tentativa {attempt}: {ex.Message}");
                    attempt++;

                    // Task.Delay em vez de page.WaitForTimeoutAsync: esperar numa página com defeito
                    // lançava uma segunda exceção de dentro deste catch e derrubava o menu inteiro.
                    await Task.Delay(2000);
                }
            }

            if (!loginSucceeded)
            {
                progress.Report("Falha: número máximo de tentativas atingido.");
                return false;
            }

            try
            {
                async Task ClickTree(string targetText, bool isFolder)
                {
                    string xpath = isFolder
                        ? $"//span[text()='{targetText}']/../../img[contains(@class, 'x-tree-ec-icon')]"
                        : $"//span[text()='{targetText}']";

                    var element = await page.WaitForSelectorAsync($"xpath={xpath}");
                    await page.EvaluateAsync("el => el.scrollIntoView({block: 'center'})", element);
                    await page.WaitForTimeoutAsync(1000);
                    await page.EvaluateAsync("el => el.click()", element);
                    await page.WaitForTimeoutAsync(2000);
                }

                progress.Report("Navegando na árvore...");
                await ClickTree("Ambiental", true);
                await ClickTree("Relatórios", true);
                await ClickTree("Gestão", false);

                var frame = await page.WaitForSelectorAsync("#content-panel");
                var contentFrame = await frame!.ContentFrameAsync();

                progress.Report("Selecionando relatório...");
                var gridItem = await contentFrame!.WaitForSelectorAsync(
                    "xpath=//div[contains(@class, 'x-grid3-cell-inner') and contains(text(), 'Acompanhamento')]");
                await contentFrame.EvaluateAsync(
                    "el => { var ev = new MouseEvent('dblclick', {bubbles:true}); el.dispatchEvent(ev); }",
                    gridItem);

                progress.Report("Preenchendo filtros...");
                var trigger = await contentFrame.WaitForSelectorAsync(
                    "xpath=//label[contains(text(), 'Referência')]/following::img[1]");
                await trigger!.ClickAsync();
                await contentFrame.ClickAsync($"xpath=//div[text()='{reference}']");

                var fields = new (string Label, string Value)[]
                {
                    ("Início", startDate),
                    ("Fim",    endDate),
                    ("Cód. IBGE", ibgeCode)
                };

                foreach (var (label, value) in fields)
                {
                    var field = await contentFrame.QuerySelectorAsync(
                        $"xpath=//label[contains(text(), '{label}')]/following::input[1]");
                    await contentFrame.EvaluateAsync("el => { el.value = ''; }", field);
                    await field!.FillAsync(value);
                    await page.Keyboard.PressAsync("Tab");
                    await page.WaitForTimeoutAsync(1000);
                }

                progress.Report("Gerando PDF...");
                var btnGenerate = await contentFrame.QuerySelectorAsync("xpath=//button[text()='Gerar']");
                await contentFrame.EvaluateAsync("el => el.click()", btnGenerate);

                progress.Report("Aguardando nova aba...");
                var newPage = await context.WaitForPageAsync(new BrowserContextWaitForPageOptions
                {
                    Timeout = 60000
                });

                await newPage.AddInitScriptAsync("window.print = () => {}");

                progress.Report("Aguardando conteúdo carregar...");
                await newPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = 60000
                });

                await newPage.WaitForSelectorAsync("xpath=//*[contains(text(), 'Município')]", new PageWaitForSelectorOptions
                {
                    Timeout = 60000
                });

                progress.Report("Gerando PDF...");
                var pdfBytes = await newPage.PdfAsync(new PagePdfOptions
                {
                    PrintBackground = true,
                    PreferCSSPageSize = true
                });

                string dStart = startDate.Replace("/", "");
                string dEnd   = endDate.Replace("/", "");
                string fileName = $"Relatorio_GAL_{dStart}_a_{dEnd}.pdf";
                string finalPath = Path.Combine(downloadFolder, fileName);

                await File.WriteAllBytesAsync(finalPath, pdfBytes);

                progress.Report($"PDF salvo: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                progress.Report($"Erro no fluxo pós-login: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> SolveCaptchaAsync(byte[] imgBytes, IProgress<string> progress)
        {
            string tempImg = Path.Combine(Path.GetTempPath(), "captcha_temp.png");
            await File.WriteAllBytesAsync(tempImg, imgBytes);

            progress.Report($"Imagem salva em: {tempImg} | Existe: {File.Exists(tempImg)} | Tamanho: {imgBytes.Length}");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = CaptchaSolverPath,
                Arguments = $"\"{tempImg}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            System.Diagnostics.Process process;
            try
            {
                process = System.Diagnostics.Process.Start(psi)
                    ?? throw new CaptchaSolverException("não foi possível iniciar captcha_solver.exe.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new CaptchaSolverException($"não foi possível iniciar captcha_solver.exe ({ex.Message}).");
            }

            using (process)
            {
                // Lê as duas saídas em paralelo para evitar travamento por buffer cheio.
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask  = process.StandardError.ReadToEndAsync();

                using var timeout = new CancellationTokenSource(CaptchaSolverTimeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    KillProcessTree(process);
                    throw new CaptchaSolverException(
                        $"captcha_solver.exe não respondeu em {CaptchaSolverTimeout.TotalSeconds:0} s e foi encerrado.");
                }

                string output = await outputTask;
                string error  = await errorTask;

                if (process.ExitCode != 0)
                    throw new CaptchaSolverException(
                        $"captcha_solver.exe terminou com código {process.ExitCode}. {error.Trim()}");

                if (!string.IsNullOrWhiteSpace(error))
                    progress.Report($"Aviso do solver: {error.Trim()}");

                string text = output.Trim().ToUpper();
                progress.Report($"OCR leu: '{text}'");
                return text;
            }
        }

        private static readonly TimeSpan CaptchaSolverTimeout = TimeSpan.FromSeconds(60);

        private static string CaptchaSolverPath
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captcha_solver.exe");

        private static void KillProcessTree(System.Diagnostics.Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* já encerrou */ }
        }

        // Falha do processo externo de OCR (ausente, travado, sem resposta no tempo) — não é captcha errado.
        private sealed class CaptchaSolverException(string message) : Exception(message);
    }
}

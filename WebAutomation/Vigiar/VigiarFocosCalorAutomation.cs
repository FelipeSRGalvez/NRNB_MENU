using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace WebAutomation
{
    public class VigiarFocosCalorAutomation : IAsyncDisposable
    {
        private const string BdQueimadasUrl        = "https://terrabrasilis.dpi.inpe.br/queimadas/bdqueimadas/#graficos";
        // A requisição firesByCity foi vista ainda em andamento aos 10 s (INPE lento); desistir nesse
        // momento reiniciava o navegador com a requisição pendente. 60 s dá margem para resposta lenta.
        private const int    FiresByCityTimeoutMs   = 60_000;

        private IPlaywright?       _playwright;
        private IBrowser?          _browser;
        private IPage?             _page;
        private IProgress<string>? _progress;

        public async Task InitializeAsync(IProgress<string>? progress = null)
        {
            _progress = progress;

            progress?.Report("Iniciando browser (Playwright headless)...");
            _playwright = await Playwright.CreateAsync();
            _browser    = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await _browser.NewContextAsync();
            _page = await context.NewPageAsync();

            progress?.Report("Abrindo BDQueimadas (#graficos)...");
            await _page.GotoAsync(BdQueimadasUrl);
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 30_000 });
            }
            catch (TimeoutException) { }

            progress?.Report("Configurando filtros: América do Sul → Brasil → Rondônia...");
            await _page.SelectOptionAsync("#continents", "8");
            await _page.WaitForTimeoutAsync(1500);
            await _page.SelectOptionAsync("#countries", "33");
            await _page.WaitForTimeoutAsync(1500);
            await _page.SelectOptionAsync("#states", "03311");
            await _page.SelectOptionAsync("#filter-satellite", "AQUA_M-T");

            progress?.Report("Browser pronto. Filtros configurados.");
        }

        // Fluxo por semana:
        // (1) define as datas pelo datepicker do jQuery
        // (2) clica em #filter-button
        // (3) espera firesByState — confirma que o filtro foi processado
        // (4) clica em #box-firesByCity .layer-title — expande o accordion carregado sob demanda
        // (5) captura graphicsfirescount?id=firesByCity via RouteAsync
        public async Task<string> FetchWeekJsonAsync(DateTime start, DateTime end)
        {
            if (_page == null)
                throw new InvalidOperationException("Automação do BDQueimadas não foi inicializada.");

            var tcsCity       = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var capturedUrls  = new ConcurrentBag<string>();

            void OnRequest(object? _, IRequest req) => capturedUrls.Add(req.Url);

            // O Playwright chama este handler por conta própria; ele nunca pode lançar exceção. Uma exceção
            // aqui não tem quem a capture (apareceu como "Route is already handled!" quando o
            // navegador foi reiniciado enquanto FetchAsync ainda esperava o INPE).
            Func<IRoute, Task> handler = async route =>
            {
                try
                {
                    if (!route.Request.Url.Contains("id=firesByCity"))
                    {
                        await route.ContinueAsync();
                        return;
                    }

                    var apiResponse = await route.FetchAsync();
                    string body = await apiResponse.TextAsync();
                    tcsCity.TrySetResult(body);
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status      = apiResponse.Status,
                        Body        = body,
                        ContentType = "application/json; charset=utf-8"
                    });
                }
                catch (Exception ex)
                {
                    tcsCity.TrySetException(ex);
                    try { await route.AbortAsync(); }
                    catch { /* rota já tratada, ou página/navegador já fechado */ }
                }
            };

            // A rota e o listener são sempre removidos (finally), para que uma tentativa com falha nunca
            // deixe handlers acumulados na página para a próxima tentativa.
            _page.Request += OnRequest;
            try
            {
                await _page.RouteAsync("**/graphicsfirescount**", handler);

                await _page.EvaluateAsync(
                    $"$('#filter-date-from').val('{start:dd/MM/yyyy}')" +
                    $".datepicker('setDate', new Date({start.Year},{start.Month - 1},{start.Day}))");
                await _page.EvaluateAsync(
                    $"$('#filter-date-to').val('{end:dd/MM/yyyy}')" +
                    $".datepicker('setDate', new Date({end.Year},{end.Month - 1},{end.Day}))");

                await _page.EvaluateAsync(
                    "document.getElementById('filter-button')" +
                    ".dispatchEvent(new MouseEvent('click', {bubbles:true, cancelable:true}))");

                try
                {
                    await _page.WaitForResponseAsync(
                        r => r.Url.Contains("id=firesByState"),
                        new PageWaitForResponseOptions { Timeout = 15_000 });
                }
                catch (TimeoutException) { }

                bool isOpen = await _page.EvaluateAsync<bool>(
                    "document.querySelector('#box-firesByCity div[style]').style.display !== 'none'");
                _progress?.Report($"[accordion] firesByCity aberto={isOpen}; " +
                                  (isOpen ? "aguardando refresh automático" : "clicando para abrir"));
                if (!isOpen)
                    await _page.ClickAsync("#box-firesByCity .layer-title");

                try
                {
                    return await tcsCity.Task.WaitAsync(TimeSpan.FromMilliseconds(FiresByCityTimeoutMs));
                }
                catch (TimeoutException)
                {
                    string urlList = capturedUrls.Count == 0
                        ? "(nenhuma URL disparada)"
                        : string.Join("\n  ", capturedUrls);

                    throw new InvalidOperationException(
                        $"Timeout: firesByCity não interceptado após expandir accordion.\n" +
                        $"URLs capturadas:\n  {urlList}");
                }
            }
            finally
            {
                _page.Request -= OnRequest;
                try { await _page.UnrouteAsync("**/graphicsfirescount**", handler); }
                catch { /* página/navegador pode já ter sido fechado */ }
            }
        }

        // Também usado para reiniciar a sessão entre tentativas; não pode lançar exceção se o
        // navegador já tiver travado ou sido fechado.
        public async ValueTask DisposeAsync()
        {
            try { if (_browser != null) await _browser.CloseAsync(); }
            catch { /* navegador já fechado */ }
            try { _playwright?.Dispose(); }
            catch { /* driver já encerrado */ }
            _browser    = null;
            _playwright = null;
            _page       = null;
        }
    }
}

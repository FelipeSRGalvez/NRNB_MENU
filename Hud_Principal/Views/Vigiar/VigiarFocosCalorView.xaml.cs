using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.IO;
using System.Linq;
using System.Windows;
using WebAutomation;

namespace Hud_Principal.Views
{
    public partial class VigiarFocosCalorView : BaseView
    {
        public VigiarFocosCalorView()
        {
            InitializeComponent();
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear; y >= 2021; y--)
                ListBoxAnos.Items.Add(y);
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("Vigiar — Focos de Calor", StartAsync, BtnStart);

        private async Task StartAsync()
        {
            var selectedYears = ListBoxAnos.SelectedItems
                .Cast<int>()
                .OrderBy(y => y)
                .ToList();

            if (selectedYears.Count == 0)
            {
                AppendStatus("Selecione ao menos um ano.");
                return;
            }

            var config = new ConfigurationService().Load();

            var errors = PathVerifier.Verify(
            [
                (config.Vigiar.WildfiresFolder, "Pasta de queimadas não configurada."),
                (config.Vigiar.RawFilesFolder,
                 "Pasta de arquivos brutos do Vigiar não configurada (Configurações → Vigiar → Pasta Arquivos Brutos). " +
                 "Ela guarda cada semana baixada para retomar de onde parou.")
            ]);

            if (errors.Count > 0)
            {
                AppendStatus(string.Join("\n", errors));
                return;
            }

            string masterPath = Path.Combine(config.Vigiar.WildfiresFolder, "FOCOS_CALOR_MESTRE.xlsx");

            TxtStatus.Text = string.Empty;

            string logPath = Path.Combine(Path.GetTempPath(), "nrnb_vigiar_debug.log");
            File.WriteAllText(logPath, string.Empty);

            var progress = new Progress<string>(msg =>
            {
                TxtStatus.Text += (TxtStatus.Text.Length > 0 ? "\n" : string.Empty) + msg;
                StatusScroll.ScrollToBottom();
                File.AppendAllText(logPath, msg + "\n");
            });

            // O navegador só é aberto quando alguma semana realmente precisa ser baixada, e é
            // recriado (resetSession) entre as tentativas para que uma página com defeito não repita a falha.
            await using var automation = new VigiarFocosCalorAutomation();
            bool initialized = false;

            async Task<string> FetchWeekAsync(DateTime start, DateTime end)
            {
                if (!initialized)
                {
                    await automation.InitializeAsync(progress);
                    initialized = true;
                }
                return await automation.FetchWeekJsonAsync(start, end);
            }

            async Task ResetSessionAsync()
            {
                initialized = false;
                await automation.DisposeAsync();
            }

            try
            {
                await new VigiarFocosCalorService().ExecuteAsync(
                    masterPath,
                    config.Vigiar.RawFilesFolder,
                    selectedYears,
                    FetchWeekAsync,
                    ResetSessionAsync,
                    progress);
            }
            catch (Exception ex)
            {
                AppendStatus("Coleta interrompida por erro. As semanas já baixadas foram mantidas e serão aproveitadas na próxima execução.");
                File.AppendAllText(logPath, $"Erro: {ex}\n");
                throw; // RunModuleSafelyAsync registra no log e avisa o usuário
            }
        }

        private void AppendStatus(string msg)
        {
            TxtStatus.Text += (TxtStatus.Text.Length > 0 ? "\n" : string.Empty) + msg;
            StatusScroll.ScrollToBottom();
        }
    }
}

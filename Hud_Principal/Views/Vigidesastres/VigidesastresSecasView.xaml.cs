using Conversor_de_Arquivos.Vigidesastres;
using Modulo_Seguranca;
using System.IO;
using System.Linq;
using System.Windows;

namespace Hud_Principal.Views
{
    public partial class VigidesastresSecasView : BaseView
    {
        // A lista começa em 2021. Obs.: o mapa só cobre Rondônia a partir de ago/2022;
        // 2021 e jan–jul/2022 são baixados, mas não contêm Rondônia.
        private const int FirstYear = 2021;

        public VigidesastresSecasView()
        {
            InitializeComponent();
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear; y >= FirstYear; y--)
                ListBoxAnos.Items.Add(y);
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
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

            Configuration config;
            try
            {
                config = new ConfigurationService().Load();
            }
            catch (Exception ex)
            {
                AppendStatus(ex.Message);
                return;
            }

            var errors = PathVerifier.Verify(
            [
                (config.Vigidesastres.DroughtFolder, "Pasta Secas não configurada.")
            ]);

            if (errors.Count > 0)
            {
                AppendStatus(string.Join("\n", errors));
                return;
            }

            BtnStart.IsEnabled = false;
            TxtStatus.Text = string.Empty;
            TxtSummary.Text = string.Empty;

            string logPath = Path.Combine(Path.GetTempPath(), "nrnb_vigidesastres_debug.log");
            File.WriteAllText(logPath, string.Empty);

            var progress = new Progress<string>(msg =>
            {
                AppendStatus(msg);
                File.AppendAllText(logPath, msg + "\n");
            });

            try
            {
                var summary = await new VigidesastresSecasService().ExecuteAsync(
                    config.Vigidesastres.DroughtFolder,
                    ConfigurationVigidesastres.UrlBaseSecas,
                    selectedYears,
                    progress);

                TxtSummary.Text = $"Baixados: {summary.Downloaded}   ·   Pulados: {summary.Skipped}   ·   " +
                                  $"Não publicados: {summary.NotPublished}   ·   Erros: {summary.Errors}";
            }
            catch (Exception ex)
            {
                string detalhe = $"Erro inesperado: {ex.GetType().Name}: {ex.Message}";
                AppendStatus(detalhe);
                File.AppendAllText(logPath, detalhe + "\n" + ex.StackTrace + "\n");
            }
            finally
            {
                BtnStart.IsEnabled = true;
            }
        }

        private void AppendStatus(string msg)
        {
            TxtStatus.Text += (TxtStatus.Text.Length > 0 ? "\n" : string.Empty) + msg;
            StatusScroll.ScrollToBottom();
        }
    }
}

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
        // 2021 e jan–jul/2022 são baixados, mas não contêm Rondônia (o processamento os pula).
        private const int FirstYear = 2021;

        public VigidesastresSecasView()
        {
            InitializeComponent();
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear; y >= FirstYear; y--)
                ListBoxAnos.Items.Add(y);
        }

        // Coleta (download/extração) e, em seguida, processamento para SECAS_MESTRE.xlsx.
        private async void BtnRun_Click(object sender, RoutedEventArgs e)
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

            var cfg = config.Vigidesastres;
            var errors = PathVerifier.Verify(
            [
                (cfg.RawFilesFolder,
                 "Pasta de arquivos brutos das secas não configurada (Configurações → Vigidesastres)."),
                (cfg.MasterFolder,
                 "Pasta do SECAS_MESTRE não configurada. Configure em Configurações → Vigidesastres → Pasta Secas Mestre.")
            ]);
            errors.AddRange(PathVerifier.VerifyFiles(
            [
                (cfg.MunicipalMeshFile,
                 "Arquivo da malha municipal não configurado (Configurações → Vigidesastres).")
            ]));

            if (errors.Count > 0)
            {
                AppendStatus(string.Join("\n", errors));
                return;
            }

            BtnRun.IsEnabled = false;
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
                var result = await new VigidesastresSecasPipeline().ExecuteAsync(
                    cfg.RawFilesFolder,
                    cfg.MasterFolder,
                    cfg.MunicipalMeshFile,
                    ConfigurationVigidesastres.UrlBaseSecas,
                    selectedYears,
                    progress);

                var c = result.Collection;
                string coleta = $"Coleta — Baixados: {c.Downloaded}   ·   Pulados: {c.Skipped}   ·   " +
                                $"Não publicados: {c.NotPublished}   ·   Erros: {c.Errors}";

                string processamento = result.Processing is { } p
                    ? $"Processamento — Processados: {p.Processed}   ·   Já na planilha: {p.AlreadyInMaster}   ·   " +
                      $"Sem cobertura de RO: {p.NoCoverage}   ·   Não baixados: {p.NotDownloaded}   ·   Erros: {p.Errors}"
                    : $"Processamento — não executado: {result.ProcessingSkippedReason}";

                TxtSummary.Text = coleta + "\n" + processamento;
            }
            catch (Exception ex)
            {
                string detalhe = $"Erro inesperado: {ex.GetType().Name}: {ex.Message}";
                AppendStatus(detalhe);
                File.AppendAllText(logPath, detalhe + "\n" + ex.StackTrace + "\n");
            }
            finally
            {
                BtnRun.IsEnabled = true;
            }
        }

        private void AppendStatus(string msg)
        {
            TxtStatus.Text += (TxtStatus.Text.Length > 0 ? "\n" : string.Empty) + msg;
            StatusScroll.ScrollToBottom();
        }
    }
}

using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.IO;
using System.Linq;
using System.Windows;
using WebAutomation;
using MessageBox = System.Windows.MessageBox;

namespace Hud_Principal.Views
{
    public partial class AnnualSisaguaImplementation : BaseView
    {
        public AnnualSisaguaImplementation()
        {
            InitializeComponent();

            for (int year = DateTime.Now.Year; year >= 2014; year--)
                LstYears.Items.Add(year);
        }

        private async void BtnStartImplementation_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("Sisagua — Implementação", StartImplementationAsync, BtnStartImplementation);

        private async Task StartImplementationAsync()
        {
            var selectedYears = LstYears.SelectedItems
                .Cast<int>()
                .OrderBy(y => y)
                .ToList();

            if (selectedYears.Count == 0)
            {
                MessageBox.Show("Selecione ao menos um ano.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = new ConfigurationService().Load();

            var errors = PathVerifier.Verify(new List<(string, string)>
            {
                (config.Vigiagua.RawFilesFolder, "Pasta de arquivos brutos não informada"),
                (config.Vigiagua.AnnualImplementationFolder, "Pasta de Implementação Anual não informada"),
            });

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Verificação falhou",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnStartImplementation.IsEnabled = false;
            BtnStartImplementation.Content = "Executando...";
            TxtStatus.Text = "";

            var progress = new Progress<string>(msg => TxtStatus.Text = msg);
            var automation = new SisaguaImplementationAutomation();

            var result = await automation.DownloadAnnualReportsAsync(
                config.Vigiagua.Email,
                config.Vigiagua.Password,
                config.Vigiagua.RawFilesFolder,
                selectedYears,
                progress);

            BtnStartImplementation.IsEnabled = true;
            BtnStartImplementation.Content = "Iniciar Processo";

            if (!result.Success)
            {
                TxtStatus.Text = "Erro na automação.";
                MessageBox.Show($"Erro:\n{result.ErrorMessage}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string masterPath = Path.Combine(
                config.Vigiagua.AnnualImplementationFolder, "IMPLEMENTACAO_ANUAL_MESTRE.xlsx");

            var processor = new SisaguaImplementationProcessor();
            var processingErrors = new List<string>();

            foreach (var rawPath in result.DownloadedFiles)
            {
                var proc = await processor.ProcessAsync(rawPath, masterPath, progress);

                if (!proc.Success)
                    processingErrors.Add($"{proc.Year}: {proc.ErrorMessage}");
            }

            if (processingErrors.Count > 0)
            {
                TxtStatus.Text = "Concluído com erros.";
                MessageBox.Show(string.Join("\n", processingErrors), "Erros",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                TxtStatus.Text = "Concluído com sucesso!";
            }
        }
    }
}
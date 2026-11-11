using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.IO;
using WebAutomation;
using MessageBox = System.Windows.MessageBox;

namespace Hud_Principal.Views
{
    public partial class MonthlySisaguaView : BaseView
    {
        public MonthlySisaguaView()
        {
            InitializeComponent();

            for (int year = 2014; year <= DateTime.Now.Year; year++)
                LstYears.Items.Add(year);

            LstYears.SelectedItem = DateTime.Now.Year;
        }

        private async void BtnStartMonthly_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("Sisagua — Diretriz mensal", StartMonthlyAsync, StartMonthlySisagua);

        private async Task StartMonthlyAsync()
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
                (config.Vigiagua.RawFilesFolder,        "Pasta de arquivos brutos não informada"),
                (config.Vigiagua.MonthlyDirectiveFolder, "Pasta da Diretriz Mensal não informada"),
            });

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Verificação falhou",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartMonthlySisagua.IsEnabled = false;
            StartMonthlySisagua.Content = "Executando...";
            TxtStatus.Text = "";

            var progress = new Progress<string>(msg => TxtStatus.Text = msg);
            var automation = new SisaguaDiretrizAutomation();

            var result = await automation.DownloadMonthlyReportsAsync(
                config.Vigiagua.Email,
                config.Vigiagua.Password,
                config.Vigiagua.RawFilesFolder,
                selectedYears,
                progress);

            if (!result.Success)
            {
                TxtStatus.Text = "Erro na automação.";
                MessageBox.Show($"Erro:\n{result.ErrorMessage}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartMonthlySisagua.IsEnabled = true;
                StartMonthlySisagua.Content = "Iniciar Processo";
                return;
            }

            var processor = new SisaguaDataProcessor();
            var processingErrors = new List<string>();

            foreach (var rawPath in result.DownloadedFiles)
            {
                string masterName = Path.GetFileNameWithoutExtension(rawPath)
                    .Split('-')[1].Trim() + "_MESTRE.xlsx";
                string masterPath = Path.Combine(
                    config.Vigiagua.MonthlyDirectiveFolder, masterName);

                var proc = await processor.ProcessAsync(rawPath, masterPath, progress);

                if (!proc.Success)
                    processingErrors.Add($"{proc.Parameter}: {proc.ErrorMessage}");
            }

            StartMonthlySisagua.IsEnabled = true;
            StartMonthlySisagua.Content = "Iniciar Processo";

            if (processingErrors.Count > 0)
            {
                TxtStatus.Text = "Concluído com erros.";
                MessageBox.Show(string.Join("\n", processingErrors), "Erros no processamento",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                TxtStatus.Text = "Concluído com sucesso!";
            }
        }
    }
}

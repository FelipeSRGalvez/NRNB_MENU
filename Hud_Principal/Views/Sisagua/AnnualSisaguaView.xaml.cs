using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.IO;
using System.Linq;
using System.Windows;
using WebAutomation;
using MessageBox = System.Windows.MessageBox;

namespace Hud_Principal.Views
{
    public partial class AnnualSisaguaView : BaseView
    {
        public AnnualSisaguaView()
        {
            InitializeComponent();

            for (int year = 2014; year <= DateTime.Now.Year; year++)
            {
                CmbStartYear.Items.Add(year);
                CmbEndYear.Items.Add(year);
            }

            CmbStartYear.SelectedItem = 2014;
            CmbEndYear.SelectedItem = DateTime.Now.Year;
        }

        private async void BtnStartAnnual_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("Sisagua — Diretriz anual", StartAnnualAsync, StartAnnualSisagua);

        private async Task StartAnnualAsync()
        {
            if (CmbStartYear.SelectedItem is not int startYear ||
                CmbEndYear.SelectedItem is not int endYear)
            {
                MessageBox.Show("Selecione os anos.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (startYear > endYear)
            {
                MessageBox.Show("Ano inicial não pode ser maior que o ano final.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = new ConfigurationService().Load();

            var errors = PathVerifier.Verify(new List<(string, string)>
            {
                (config.Vigiagua.RawFilesFolder,       "Pasta de arquivos brutos não informada"),
                (config.Vigiagua.AnnualDirectiveFolder, "Pasta da Diretriz Anual não informada"),
            });

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Verificação falhou",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartAnnualSisagua.IsEnabled = false;
            StartAnnualSisagua.Content = "Executando...";
            TxtStatus.Text = "";

            var progress = new Progress<string>(msg => TxtStatus.Text = msg);
            var automation = new SisaguaDiretrizAnualAutomation();

            var result = await automation.DownloadAnnualReportsAsync(
                config.Vigiagua.Email,
                config.Vigiagua.Password,
                config.Vigiagua.RawFilesFolder,
                startYear,
                endYear,
                progress);

            if (!result.Success)
            {
                TxtStatus.Text = "Erro na automação.";
                MessageBox.Show($"Erro:\n{result.ErrorMessage}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartAnnualSisagua.IsEnabled = true;
                StartAnnualSisagua.Content = "Iniciar Processo";
                return;
            }

            var processor = new SisaguaAnnualDataProcessor();
            var errors2 = new List<string>();

            foreach (var rawPath in result.DownloadedFiles)
            {
                string masterName = Path.GetFileNameWithoutExtension(rawPath)
                    .Split('-')[1].Trim() + "_ANUAL_MESTRE.xlsx";
                string masterPath = Path.Combine(
                    config.Vigiagua.AnnualDirectiveFolder, masterName);

                var proc = await processor.ProcessAsync(rawPath, masterPath, progress);

                if (!proc.Success)
                    errors2.Add($"{proc.Parameter}: {proc.ErrorMessage}");
            }

            StartAnnualSisagua.IsEnabled = true;
            StartAnnualSisagua.Content = "Iniciar Processo";

            if (errors2.Count > 0)
            {
                TxtStatus.Text = "Concluído com erros.";
                MessageBox.Show(string.Join("\n", errors2), "Erros",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                TxtStatus.Text = "Concluído com sucesso!";
            }
        }
    }
}

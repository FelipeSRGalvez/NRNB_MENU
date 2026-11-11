using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.IO;
using System.Windows;
using WebAutomation;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace Hud_Principal.Views
{
    public partial class ParameterSisaguaView : BaseView
    {
        private bool _updating = false;
        private List<CheckBox> _infoItems = [];
        private List<CheckBox> _basicosItems = [];
        private List<CheckBox> _demaisItems = [];

        public ParameterSisaguaView()
        {
            InitializeComponent();

            for (int year = DateTime.Now.Year; year >= 2014; year--)
                LstYears.Items.Add(year);

            _infoItems =
            [
                ChkMunicipio, ChkMotivo, ChkNome, ChkCodigo, ChkZona, ChkNumeroAmostra,
                ChkDataColeta, ChkDataLaudo, ChkDataRegistro, ChkCategoriaArea,
                ChkPrecedencia, ChkPontoColeta, ChkArea, ChkLocal, ChkDescricaoLocal,
                ChkLatLong, ChkHoraColeta, ChkChuva
            ];

            _basicosItems =
            [
                ChkColiformes, ChkFluoreto, ChkTurbidez, ChkPh,
                ChkCorAparente, ChkDioxidoCloro, ChkCloroLivre, ChkCloroCombinado
            ];

            _demaisItems =
            [
                ChkAgrotoxicos, ChkSubstanciasOrganicas, ChkCianobacterias,
                ChkProdutosSecundarios, ChkCianotoxinas, ChkRadioatividade,
                ChkSubstanciasInorganicas, ChkParamOrganoleticos, ChkOutros
            ];

            _updating = true;
            ChkMunicipio.IsChecked = true;
            ChkNome.IsChecked = true;
            ChkDataColeta.IsChecked = true;
            ChkCategoriaArea.IsChecked = true;
            ChkPrecedencia.IsChecked = true;
            ChkPontoColeta.IsChecked = true;
            ChkArea.IsChecked = true;
            ChkLocal.IsChecked = true;
            ChkColiformes.IsChecked = true;
            ChkTurbidez.IsChecked = true;
            ChkCloroLivre.IsChecked = true;
            _updating = false;
        }

        private void ChkInfoTodos_Checked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            foreach (var cb in _infoItems) cb.IsChecked = true;
            _updating = false;
        }

        private void ChkInfoTodos_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            foreach (var cb in _infoItems) cb.IsChecked = false;
            _updating = false;
        }

        private void InfoItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            ChkInfoTodos.IsChecked = _infoItems.All(cb => cb.IsChecked == true);
            _updating = false;
        }

        private void ChkBasicosTodos_Checked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            ClearDemais();
            foreach (var cb in _basicosItems) cb.IsChecked = true;
            _updating = false;
        }

        private void ChkBasicosTodos_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            foreach (var cb in _basicosItems) cb.IsChecked = false;
            _updating = false;
        }

        private void BasicosItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            if (((CheckBox)sender).IsChecked == true) ClearDemais();
            ChkBasicosTodos.IsChecked = _basicosItems.All(cb => cb.IsChecked == true);
            _updating = false;
        }

        private void ChkDemaisTodos_Checked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            ClearBasicos();
            foreach (var cb in _demaisItems) cb.IsChecked = true;
            _updating = false;
        }

        private void ChkDemaisTodos_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            foreach (var cb in _demaisItems) cb.IsChecked = false;
            _updating = false;
        }

        private void DemaisItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            if (((CheckBox)sender).IsChecked == true) ClearBasicos();
            ChkDemaisTodos.IsChecked = _demaisItems.All(cb => cb.IsChecked == true);
            _updating = false;
        }

        private void ClearBasicos()
        {
            ChkBasicosTodos.IsChecked = false;
            foreach (var cb in _basicosItems) cb.IsChecked = false;
        }

        private void ClearDemais()
        {
            ChkDemaisTodos.IsChecked = false;
            foreach (var cb in _demaisItems) cb.IsChecked = false;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("Sisagua — Amostras Analisadas", StartAsync, BtnStart);

        private async Task StartAsync()
        {
            var selectedYears = LstYears.SelectedItems.Cast<int>().OrderBy(y => y).ToList();

            if (selectedYears.Count == 0)
            {
                MessageBox.Show("Selecione ao menos um ano.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var infoLabels = _infoItems
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (string)cb.Content)
                .ToList();

            var basicosLabels = _basicosItems
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (string)cb.Content)
                .ToList();

            var demaisLabels = _demaisItems
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (string)cb.Content)
                .ToList();

            var config = new ConfigurationService().Load();

            var errors = PathVerifier.Verify(
            [
                (config.Vigiagua.RawFilesFolder, "Pasta de arquivos brutos não informada"),
            ]);

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Verificação falhou",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnStart.IsEnabled = false;
            BtnStart.Content = "Executando...";
            TxtStatus.Text = "";

            var progress = new Progress<string>(msg => TxtStatus.Text = msg);
            var automation = new AmostrasAnalisadasAutomation();

            var result = await automation.DownloadAsync(
                config.Vigiagua.Email,
                config.Vigiagua.Password,
                config.Vigiagua.RawFilesFolder,
                selectedYears,
                infoLabels,
                basicosLabels,
                demaisLabels,
                progress);

            BtnStart.IsEnabled = true;
            BtnStart.Content = "Iniciar Processo";

            if (!result.Success)
            {
                TxtStatus.Text = "Erro na automação.";
                MessageBox.Show($"Erro:\n{result.ErrorMessage}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Básicos e Demais são mutuamente exclusivos na tela — o mestre a atualizar
                // depende de qual seção de resultado foi marcada nesse processamento.
                string masterFileName = basicosLabels.Count > 0
                    ? "AMOSTRAS_ANALISADAS_BASICOS_MESTRE.xlsx"
                    : "AMOSTRAS_ANALISADAS_DEMAIS_MESTRE.xlsx";

                string masterPath = Path.Combine(
                    config.Vigiagua.AnalyzedSamplesFolder, masterFileName);

                var processor = new AmostrasAnalisadasProcessor();
                var processingErrors = new List<string>();

                foreach (var rawPath in result.DownloadedFiles)
                {
                    var proc = await processor.ProcessAsync(rawPath, masterPath, progress);
                    if (!proc.Success)
                        processingErrors.Add($"{proc.Year}: {proc.ErrorMessage}");
                }

                if (processingErrors.Count > 0)
                {
                    TxtStatus.Text = "Concluído com erros no processamento.";
                    MessageBox.Show(string.Join("\n", processingErrors), "Erros",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    TxtStatus.Text = $"Concluído. {result.DownloadedFiles.Count} arquivo(s) processado(s).";
                }
            }
        }
    }
}
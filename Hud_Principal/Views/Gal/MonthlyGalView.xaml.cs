using Conversor_de_Arquivos;
using Modulo_Seguranca;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WebAutomation;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Hud_Principal.Views
{
    public partial class MonthlyGalView : BaseView
    {
        private bool _formattingDate;

        public MonthlyGalView()
        {
            InitializeComponent();
            TxtStartDate.Text = $"01/01/{DateTime.Now.Year}";
            TxtEndDate.Text   = DateTime.Now.ToString("dd/MM/yyyy");
        }

        // Bloqueia todo caractere que não seja dígito; a "/" é inserida automaticamente por Date_TextChanged.
        private void Date_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!e.Text.All(char.IsDigit))
                e.Handled = true;
        }

        // Após cada tecla, remove o que não for dígito e remonta o formato "dd/MM/yyyy".
        private void Date_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_formattingDate) return;
            var tb = (WpfTextBox)sender;

            string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (digits.Length > 8) digits = digits[..8];

            string formatted = digits.Length switch
            {
                0 or 1 or 2 => digits,
                3 or 4       => digits[..2] + "/" + digits[2..],
                _            => digits[..2] + "/" + digits[2..4] + "/" + digits[4..]
            };

            _formattingDate = true;
            int caret = tb.CaretIndex;
            tb.Text = formatted;
            int newCaret = Math.Min(caret, formatted.Length);
            while (newCaret < formatted.Length && formatted[newCaret] == '/') newCaret++;
            tb.CaretIndex = newCaret;
            _formattingDate = false;

            tb.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
        }

        // Valida a data final quando o campo perde o foco.
        private void Date_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = (WpfTextBox)sender;
            if (!DateTime.TryParseExact(tb.Text, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                tb.BorderBrush = WpfBrushes.Red;
                TxtStatus.Text = $"Data inválida: '{tb.Text}'. Use o formato dd/MM/aaaa.";
            }
            else
            {
                tb.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
            => await RunModuleSafelyAsync("GAL — Acompanhamento Mensal", StartAsync, BtnStart);

        private async Task StartAsync()
        {
            if (string.IsNullOrWhiteSpace(TxtStartDate.Text) ||
                string.IsNullOrWhiteSpace(TxtEndDate.Text) ||
                string.IsNullOrWhiteSpace(TxtIbge.Text))
            {
                TxtStatus.Text = "Preencha todos os campos antes de iniciar.";
                return;
            }

            var config = new ConfigurationService().Load();

            var credentialErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(config.Gal.Username))
                credentialErrors.Add("Usuário do GAL não configurado.");
            if (string.IsNullOrWhiteSpace(config.Gal.Password))
                credentialErrors.Add("Senha do GAL não configurada.");

            var pathErrors = PathVerifier.Verify(new List<(string, string)>
            {
                (config.Gal.GalMonthlyRawFolder, "Pasta de download do GAL não configurada."),
                (config.Gal.GalMonthlyFolder,    "Pasta de relatórios processados do GAL não configurada."),
            });

            var allErrors = credentialErrors.Concat(pathErrors).ToList();
            if (allErrors.Count > 0)
            {
                TxtStatus.Text = string.Join("\n", allErrors);
                return;
            }

            string reference  = ((ComboBoxItem)CmbReference.SelectedItem).Content.ToString()!;
            string startDate  = TxtStartDate.Text;
            string endDate    = TxtEndDate.Text;
            string ibgeCode   = TxtIbge.Text;
            string username   = config.Gal.Username;
            string password   = config.Gal.Password;
            string module     = config.Gal.Module;
            string laboratory = config.Gal.Laboratory;
            string rawFolder  = config.Gal.GalMonthlyRawFolder;
            string masterFolder = config.Gal.GalMonthlyFolder;

            BtnStart.IsEnabled = false;

            var progress = new Progress<string>(msg => TxtStatus.Text = msg);

            bool success = await Task.Run(() =>
                new GalMonthlyAutomation().BaixarRelatorioMensalAsync(
                    username, password, module, laboratory,
                    rawFolder, reference, startDate, endDate, ibgeCode,
                    progress));

            if (!success)
            {
                TxtStatus.Text = "Falha na coleta. Verifique o status acima.";
                BtnStart.IsEnabled = true;
                return;
            }

            string dStart   = startDate.Replace("/", "");
            string dEnd     = endDate.Replace("/", "");
            string pdfPath  = Path.Combine(rawFolder,    $"Relatorio_GAL_{dStart}_a_{dEnd}.pdf");
            string masterPath = Path.Combine(masterFolder, "Acompanhamento_Mensal_Agua_MESTRE.xlsx");

            var result = await Task.Run(() =>
                new GalMonthlyDataProcessor().ProcessAsync(pdfPath, masterPath, progress));

            TxtStatus.Text = result.Success
                ? $"Concluído. {result.MunicipalitiesFound} municípios processados, " +
                  $"{result.RowsProcessed} linhas no mestre."
                : $"Download OK, mas falha no processamento: {result.ErrorMessage}";

            BtnStart.IsEnabled = true;
        }
    }
}

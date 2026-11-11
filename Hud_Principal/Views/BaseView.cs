using System.Windows;
using System.Windows.Controls;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Hud_Principal.Views
{
    public class BaseView : UserControl
    {
        public event EventHandler? RequestHome;

        private bool _moduleRunning;

        protected void BtnHome_Click(object sender, RoutedEventArgs e)

            => RequestHome?.Invoke(this, EventArgs.Empty);

        // Executa um módulo isolado do resto do app: qualquer exceção é registrada no log,
        // mostrada ao usuário em português (sem stack trace) e contida aqui, para que um
        // módulo com falha nunca derrube o menu. Os controles informados ficam desabilitados
        // durante a execução e são sempre restaurados (IsEnabled e Content) no final.
        protected async Task RunModuleSafelyAsync(
            string moduleName, Func<Task> action, params Control[] controlsToDisable)
        {
            if (_moduleRunning) return;
            _moduleRunning = true;

            var originalContent = controlsToDisable
                .OfType<ContentControl>()
                .ToDictionary(c => c, c => c.Content);

            foreach (var control in controlsToDisable)
                control.IsEnabled = false;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ModuleErrorLog.Write(moduleName, ex);
                MessageBox.Show(
                    $"Ocorreu um erro em \"{moduleName}\" e a execução foi interrompida.\n\n" +
                    $"Motivo: {ShortMessage(ex)}\n\n" +
                    "O restante do menu continua funcionando. Os detalhes técnicos foram salvos em:\n" +
                    ModuleErrorLog.LogFilePath,
                    "Erro no módulo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                foreach (var (control, content) in originalContent)
                    control.Content = content;
                foreach (var control in controlsToDisable)
                    control.IsEnabled = true;
                _moduleRunning = false;
            }
        }

        // Primeira linha da mensagem interna mais relevante, com tamanho limitado e sem stack trace.
        private static string ShortMessage(Exception ex)
        {
            var inner = ex is AggregateException agg ? agg.Flatten().InnerException ?? ex : ex;
            string first = (inner.Message ?? string.Empty).Split('\n')[0].Trim();
            if (string.IsNullOrEmpty(first)) first = inner.GetType().Name;
            return first.Length > 300 ? first[..300] + "…" : first;
        }
    }
}

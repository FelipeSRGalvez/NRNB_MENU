using Hud_Principal;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace hud_principal
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Redireciona browsers do Playwright para pasta relativa ao .exe,
            // mas só quando essa pasta existir (só existe em publish single-file).
            // Em Debug/F5 a pasta não existe: mantém o cache padrão do Playwright
            // (%LOCALAPPDATA%\ms-playwright), evitando quebrar execução local.
            var browsersPath = Path.Combine(AppContext.BaseDirectory, "playwright-browsers");
            if (Directory.Exists(browsersPath))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
            }

            RegisterGlobalErrorHandlers();

            base.OnStartup(e);
        }

        // Apenas rede de segurança. Erros dos módulos são tratados por BaseView.RunModuleSafelyAsync;
        // o que chega aqui escapou de um módulo, então é registrado e informado, nunca escondido.
        private void RegisterGlobalErrorHandlers()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                ModuleErrorLog.Write("Erro não tratado na interface", args.Exception);
                MessageBox.Show(
                    "Ocorreu um erro inesperado. A operação atual foi interrompida, mas o menu continua aberto.\n\n" +
                    $"Os detalhes técnicos foram salvos em:\n{ModuleErrorLog.LogFilePath}",
                    "Erro inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                ModuleErrorLog.Write("Tarefa em segundo plano com exceção não observada", args.Exception);
                args.SetObserved();
            };

            // Não consegue manter o processo vivo (IsTerminating), mas deixa um registro no log.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    ModuleErrorLog.Write("Erro fatal (encerrando)", ex);
            };
        }
    }
}
using System.IO;
using System.Text;

namespace Hud_Principal
{
    // Grava erros inesperados (com stack trace) para diagnóstico.
    // O usuário só vê uma mensagem curta em português; os detalhes ficam neste arquivo.
    public static class ModuleErrorLog
    {
        private static readonly object Sync = new();

        public static string LogFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NRNB_MENU", "logs", "erros.log");

        // Nunca lança exceção: gravar o log não pode gerar uma segunda falha.
        public static void Write(string source, Exception ex)
        {
            try
            {
                var sb = new StringBuilder()
                    .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}")
                    .AppendLine(ex.ToString())
                    .AppendLine(new string('-', 80));

                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                    File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Melhor esforço.
            }
        }
    }
}

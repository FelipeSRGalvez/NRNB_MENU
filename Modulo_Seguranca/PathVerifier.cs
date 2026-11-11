namespace Modulo_Seguranca
{
    public static class PathVerifier
    {
        // Recebe somente as pastas relevantes para a automação chamada.
        // Cada item: (Path = caminho configurado, Message = mensagem de erro caso esteja vazio ou não exista)
        public static List<string> Verify(List<(string Path, string Message)> paths)
        {
            var errors = new List<string>();

            foreach (var (path, message) in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add(message);
                    continue;
                }

                if (!Directory.Exists(path))
                    errors.Add($"{message} (pasta não encontrada: {path})");
            }

            return errors;
        }

        // Mesmo padrão de Verify, para arquivos (ex.: malha municipal em pasta compartilhada).
        // Cada item: (Path = caminho completo do arquivo, Message = mensagem caso esteja vazio ou não exista)
        public static List<string> VerifyFiles(List<(string Path, string Message)> files)
        {
            var errors = new List<string>();

            foreach (var (path, message) in files)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add(message);
                    continue;
                }

                if (!File.Exists(path))
                    errors.Add($"{message} (arquivo não encontrado: {path} — a pasta pode ter mudado de lugar)");
            }

            return errors;
        }
    }
}

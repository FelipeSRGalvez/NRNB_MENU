using System.IO.Compression;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Validação e extração segura dos ZIPs mensais do Monitor de Secas.
    // Não interpreta o shapefile: apenas grava os arquivos como vieram.
    public class SecasDataProcessor
    {
        // Limites defensivos contra arquivos corrompidos ou "zip bomb".
        // Um ZIP mensal típico tem ~40 arquivos e ~20 MB descompactados.
        private const int MaxEntries = 2000;
        private const long MaxTotalUncompressedBytes = 1L * 1024 * 1024 * 1024; // 1 GB

        // Mês considerado completo quando o ZIP original e a pasta extraída (não vazia) existem.
        // Ambos só aparecem no nome final após sucesso (gravação em .part / .tmp e depois Move).
        public bool IsMonthComplete(string zipPath, string extractDir)
            => File.Exists(zipPath)
               && Directory.Exists(extractDir)
               && Directory.EnumerateFileSystemEntries(extractDir).Any();

        // Confere a assinatura local file header "PK\x03\x04" no início do arquivo.
        public bool HasZipSignature(string path)
        {
            Span<byte> header = stackalloc byte[4];
            using var fs = File.OpenRead(path);
            if (fs.Read(header) < 4) return false;
            return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
        }

        // Extrai para uma pasta temporária ao lado do destino e só então move para o nome final,
        // para que uma extração interrompida nunca seja confundida com um mês completo.
        public int ExtractSafely(string zipPath, string extractDir)
        {
            if (!HasZipSignature(zipPath))
                throw new InvalidDataException("Arquivo baixado não é um ZIP válido (assinatura PK ausente).");

            string tempDir = extractDir + ".tmp";
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            Directory.CreateDirectory(tempDir);

            string rootFull = Path.GetFullPath(tempDir);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
                rootFull += Path.DirectorySeparatorChar;

            int files = 0;
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);

                if (archive.Entries.Count > MaxEntries)
                    throw new InvalidDataException($"ZIP com entradas demais ({archive.Entries.Count}).");

                long total = archive.Entries.Sum(e => e.Length);
                if (total > MaxTotalUncompressedBytes)
                    throw new InvalidDataException("ZIP excede o tamanho máximo descompactado permitido.");

                foreach (var entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(rootFull, entry.FullName));

                    // Zip-slip: o caminho resolvido precisa ficar dentro da pasta de destino.
                    if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Entrada com caminho inválido rejeitada: {entry.FullName}");

                    // Entrada de diretório
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                    files++;
                }

                if (files == 0)
                    throw new InvalidDataException("ZIP não contém arquivos.");

                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, recursive: true);
                Directory.Move(tempDir, extractDir);
            }
            catch
            {
                TryDeleteDirectory(tempDir);
                throw;
            }

            return files;
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch { /* melhor esforço */ }
        }
    }
}

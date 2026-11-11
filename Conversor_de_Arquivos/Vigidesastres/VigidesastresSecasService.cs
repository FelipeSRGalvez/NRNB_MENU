using System.Net;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Baixa e extrai os shapefiles mensais públicos do Monitor de Secas (ANA).
    // Estrutura gerada na pasta de brutos (PastaArquivosBrutosSecas):
    //   {ano}\{mes}{ano}.zip    -> ZIP original
    //   {ano}\{mes}{ano}\       -> conteúdo extraído
    public class VigidesastresSecasService
    {
        private const int DelayBetweenCallsMs = 400;

        // Nomes usados na URL: minúsculos e sem acento.
        private static readonly string[] MonthNames =
        [
            "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
            "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
        ];

        private static readonly HttpClient Http = CreateHttpClient();

        private readonly SecasDataProcessor _processor = new();

        public async Task<SecasRunSummary> ExecuteAsync(
            string rawFilesFolder,
            string baseUrl,
            List<int> years,
            IProgress<string> progress)
        {
            var summary = new SecasRunSummary();
            var months = BuildMonthList(years, DateTime.Now);

            if (months.Count == 0)
            {
                progress.Report("Nenhum mês encerrado nos anos selecionados.");
                return summary;
            }

            progress.Report($"{months.Count} mês(es) a verificar: " +
                            $"{months.First().FileStem} até {months.Last().FileStem}.");

            foreach (var month in months)
            {
                var result = await ProcessMonthAsync(month, rawFilesFolder, baseUrl, progress);
                summary.Results.Add(result);

                if (result.Status is SecasMonthStatus.Downloaded or SecasMonthStatus.NotPublished)
                    await Task.Delay(DelayBetweenCallsMs);
            }

            progress.Report($"Concluído. Baixados: {summary.Downloaded} · Pulados: {summary.Skipped} · " +
                            $"Não publicados: {summary.NotPublished} · Erros: {summary.Errors}");
            return summary;
        }

        private async Task<SecasMonthResult> ProcessMonthAsync(
            SecasMonth month, string rawFilesFolder, string baseUrl, IProgress<string> progress)
        {
            string yearDir    = Path.Combine(rawFilesFolder, month.Year.ToString());
            string zipPath    = Path.Combine(yearDir, month.FileStem + ".zip");
            string extractDir = Path.Combine(yearDir, month.FileStem);

            if (_processor.IsMonthComplete(zipPath, extractDir))
            {
                progress.Report($"{month.FileStem}: já baixado, pulando.");
                return new SecasMonthResult(month, SecasMonthStatus.Skipped);
            }

            try
            {
                Directory.CreateDirectory(yearDir);

                // ZIP já existe mas a extração não: reaproveita sem baixar de novo.
                if (!File.Exists(zipPath) || !_processor.HasZipSignature(zipPath))
                {
                    progress.Report($"{month.FileStem}: baixando...");

                    // Tenta cada variante de nome; só é "não publicado" se todas derem 404.
                    string? usedStem = null;
                    foreach (string stem in month.RemoteStems)
                    {
                        string url = baseUrl.TrimEnd('/') + "/" + stem + ".zip";
                        if (await DownloadAsync(url, zipPath))
                        {
                            usedStem = stem;
                            break;
                        }
                    }

                    if (usedStem is null)
                    {
                        progress.Report($"{month.FileStem}: ainda não publicado (404 em {string.Join(" e ", month.RemoteStems)}).");
                        return new SecasMonthResult(month, SecasMonthStatus.NotPublished);
                    }

                    if (usedStem != month.FileStem)
                        progress.Report($"{month.FileStem}: encontrado como {usedStem}.zip.");
                }

                int files;
                try
                {
                    files = _processor.ExtractSafely(zipPath, extractDir);
                }
                catch
                {
                    // ZIP inválido não pode ficar no disco, senão seria reaproveitado na próxima execução.
                    try { File.Delete(zipPath); } catch { /* melhor esforço */ }
                    throw;
                }

                progress.Report($"{month.FileStem}: extraído ({files} arquivos).");
                return new SecasMonthResult(month, SecasMonthStatus.Downloaded);
            }
            catch (Exception ex)
            {
                progress.Report($"{month.FileStem}: erro — {ex.Message}");
                return new SecasMonthResult(month, SecasMonthStatus.Error, ex.Message);
            }
        }

        // Retorna false para 404 (mês não publicado). Grava em .part e só renomeia após validar o ZIP.
        private async Task<bool> DownloadAsync(string url, string zipPath)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();

            string partPath = zipPath + ".part";
            try
            {
                await using (var input = await response.Content.ReadAsStreamAsync())
                await using (var output = File.Create(partPath))
                {
                    await input.CopyToAsync(output);
                }

                if (!_processor.HasZipSignature(partPath))
                    throw new InvalidDataException("Resposta do servidor não é um arquivo ZIP (assinatura PK ausente).");

                File.Move(partPath, zipPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(partPath))
                    File.Delete(partPath);
            }

            return true;
        }

        // Só meses já encerrados: o mês corrente nunca está publicado.
        private static List<SecasMonth> BuildMonthList(List<int> years, DateTime now)
        {
            var list = new List<SecasMonth>();
            foreach (int year in years.Distinct().OrderBy(y => y))
            {
                for (int m = 1; m <= 12; m++)
                {
                    if (year > now.Year || (year == now.Year && m >= now.Month))
                        break;
                    list.Add(new SecasMonth(year, m, MonthNames[m - 1]));
                }
            }
            return list;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NRNB_MENU/0.1");
            return client;
        }
    }
}

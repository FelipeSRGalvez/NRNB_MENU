using Conversor_de_Arquivos.Geo;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Processa os meses já baixados/extraídos na pasta de brutos (PastaArquivosBrutosSecas)
    // e grava SECAS_MESTRE.xlsx na pasta do mestre (PastaSecasMestre).
    public class VigidesastresSecasProcessingService
    {
        // Antes de agosto/2022 o mapa do Monitor de Secas não cobre Rondônia
        // (a área monitorada terminava a leste do estado). Meses anteriores são pulados.
        public static readonly DateOnly InicioCoberturaRondonia = new(2022, 8, 1);

        // Mesma convenção de nomes do download (minúsculos, sem acento).
        private static readonly string[] MonthNames =
        [
            "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
            "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
        ];

        private readonly string _projectionWkt;
        private readonly SecasDataProcessor _zip = new();
        private readonly SecasMestreDataProcessor _mestre = new();

        // Áreas calculadas por padrão em SIRGAS 2000 / Brazil Albers (área igual).
        // A Policônica (EPSG:5880) não é de área igual: superestima a área de RO em ~1,2%
        // (diferença de até 0,13 p.p. nos percentuais municipais em set/2026).
        public VigidesastresSecasProcessingService(string projectionWkt = GeoProjections.BrazilAlbersEqualArea)
            => _projectionWkt = projectionWkt;

        public async Task<SecasProcessSummary> ExecuteAsync(
            string rawFilesFolder,
            string masterFolder,
            string meshPath,
            List<int> years,
            IProgress<string> progress)
        {
            var summary = new SecasProcessSummary();
            var months = BuildMonthList(years, DateTime.Now);
            if (months.Count == 0)
            {
                progress.Report("Nenhum mês encerrado nos anos selecionados.");
                return summary;
            }

            string masterPath = Path.Combine(masterFolder, SecasMestreDataProcessor.NomeArquivo);
            var recorded = _mestre.ReadRecordedMonths(masterPath);
            var projecao = new GeoReprojector(_projectionWkt);
            var analyzer = new SecasMunicipalAnalyzer(projecao);
            SecasMalhaMunicipal? malha = null;

            progress.Report($"{months.Count} mês(es) a verificar: {months.First().FileStem} até {months.Last().FileStem}.");

            foreach (var month in months)
            {
                string stem = month.FileStem;

                if (new DateOnly(month.Year, month.Month, 1) < InicioCoberturaRondonia)
                {
                    progress.Report($"{stem}: sem cobertura de RO, pulando.");
                    summary.Results.Add(new(month, SecasProcessStatus.NoCoverage));
                    continue;
                }

                if (recorded.Contains((month.Year, month.Month)))
                {
                    progress.Report($"{stem}: já está na planilha mestre, pulando.");
                    summary.Results.Add(new(month, SecasProcessStatus.AlreadyInMaster));
                    continue;
                }

                string yearDir = Path.Combine(rawFilesFolder, month.Year.ToString());
                string extractDir = Path.Combine(yearDir, stem);
                if (!_zip.IsMonthComplete(Path.Combine(yearDir, stem + ".zip"), extractDir))
                {
                    progress.Report($"{stem}: não baixado/extraído (não publicado ou erro na coleta), pulando.");
                    summary.Results.Add(new(month, SecasProcessStatus.NotDownloaded));
                    continue;
                }

                // Malha carregada uma única vez, só quando há mês a processar.
                if (malha == null)
                {
                    try
                    {
                        progress.Report("Carregando malha municipal...");
                        malha = await Task.Run(() => SecasMalhaMunicipal.Carregar(meshPath, projecao));
                        progress.Report($"Malha carregada: {malha.Municipios.Count} municípios.");
                    }
                    catch (Exception ex)
                    {
                        progress.Report($"Erro ao carregar a malha municipal: {ex.Message}");
                        summary.Results.Add(new(month, SecasProcessStatus.Error, ex.Message));
                        return summary;
                    }
                }

                try
                {
                    var camada = SecasCamadaLocator.Localizar(extractDir);
                    string tipo = camada.Tipo == SecasTipoCamada.Isolada ? "isolada" : "cumulativa";
                    progress.Report($"{stem}: processando {Path.GetFileName(camada.Arquivo)} (camada {tipo})...");

                    var resultados = await Task.Run(() => analyzer.Analisar(camada, malha));
                    await _mestre.AppendMonthAsync(masterPath, month.Year, month.Month, resultados);

                    progress.Report($"{stem}: {resultados.Count} municípios gravados.");
                    summary.Results.Add(new(month, SecasProcessStatus.Processed));
                }
                catch (Exception ex)
                {
                    progress.Report($"{stem}: erro — {ex.Message}");
                    summary.Results.Add(new(month, SecasProcessStatus.Error, ex.Message));
                }
            }

            progress.Report($"Concluído. Processados: {summary.Processed} · Já na planilha: {summary.AlreadyInMaster} · " +
                            $"Sem cobertura de RO: {summary.NoCoverage} · Não baixados: {summary.NotDownloaded} · " +
                            $"Erros: {summary.Errors}");
            return summary;
        }

        // Só meses encerrados.
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
    }
}

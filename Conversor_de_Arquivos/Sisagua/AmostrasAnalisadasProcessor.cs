using System.Data;
using System.Text.RegularExpressions;
using ExcelDataReader;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class AmostrasAnalisadasProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAdded { get; set; }
        public string Year { get; set; } = string.Empty;
    }

    public class AmostrasAnalisadasProcessor
    {
        private static readonly string[] MasterHeaders =
        [
            "Municipio", "CodIBGE", "Regional", "Ano", "Forma", "Motivo", "Nome", "Codigo",
            "NumeroAmostra", "DataColeta", "Dia", "Mes", "DataLaudo", "DataRegistroSisagua", "CategoriaArea",
            "Zona", "ProcedenciaColeta", "PontoColeta", "Area", "Local", "DescricaoLocal",
            "Latitude", "Longitude", "HoraColeta", "Chuva48h", "Parametro", "CategoriaParametro", "Valor"
        ];

        // Validado contra arquivo real (Básicos e Demais, todos os campos marcados).
        // Header do arquivo -> campo canônico do master.
        private static readonly Dictionary<string, string> InfoHeaders = new(StringComparer.Ordinal)
        {
            ["Município"] = "Municipio",
            ["Código IBGE"] = "CodIBGE",
            ["Motivo"] = "Motivo",
            ["Forma"] = "Forma",
            ["Nome"] = "Nome",
            ["Código"] = "Codigo",
            ["N° da amostra"] = "NumeroAmostra",
            ["Data da coleta"] = "DataColeta",
            ["Data do laudo"] = "DataLaudo",
            ["Data de registro SISAGUA"] = "DataRegistroSisagua",
            ["Procedência da coleta"] = "ProcedenciaColeta",
            ["Ponto de coleta"] = "PontoColeta",
            ["Categoria da Área"] = "CategoriaArea",
            ["Zona"] = "Zona",
            ["Área"] = "Area",
            ["Local"] = "Local",
            ["Descrição do local"] = "DescricaoLocal",
            ["Latitude"] = "Latitude",
            ["Longitude"] = "Longitude",
            ["Hora da coleta"] = "HoraColeta",
            ["Chuva nas últimas 48h"] = "Chuva48h",
        };

        public async Task<AmostrasAnalisadasProcessingResult> ProcessAsync(
            string rawFilePath,
            string masterPath,
            IProgress<string>? progress = null)
        {
            var result = new AmostrasAnalisadasProcessingResult();

            try
            {
                var match = Regex.Match(rawFilePath, @"PARAMETROS_SISAGUA_BRUTO_(\d{4})");
                string year = match.Success ? match.Groups[1].Value : string.Empty;
                result.Year = year;

                if (string.IsNullOrEmpty(year))
                {
                    result.Success = false;
                    result.ErrorMessage = "Não foi possível extrair o ano do nome do arquivo.";
                    return result;
                }

                progress?.Report($"[{year}] Lendo arquivo bruto de amostras analisadas...");
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                DataSet ds;
                using (var stream = File.Open(rawFilePath, FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                    ds = reader.AsDataSet();

                var sheet = ds.Tables[0];

                if (sheet.Rows.Count < 8)
                {
                    result.Success = false;
                    result.ErrorMessage = "Arquivo sem linhas de dados.";
                    return result;
                }

                int colCount = sheet.Columns.Count;

                // Linha 5 (idx 4) = cabeçalho principal / rótulo de grupo (quando o parâmetro
                // se desdobra em várias colunas, ex.: Agrotóxicos, Cianobactérias, Área).
                // Linha 6 (idx 5) = subcabeçalho / nome específico da coluna, quando existir.
                var effectiveName = new string[colCount];   // nome específico da coluna (usado como Parametro)
                var groupLabel = new string[colCount];      // rótulo de grupo, propagado (forward-fill) da linha 5

                string lastGroup = string.Empty;
                for (int c = 0; c < colCount; c++)
                {
                    string h5 = NormalizeHeader(sheet.Rows[4][c]?.ToString() ?? string.Empty);
                    string h6 = NormalizeHeader(sheet.Rows[5][c]?.ToString() ?? string.Empty);

                    if (h5.Length > 0) lastGroup = h5;
                    groupLabel[c] = lastGroup;
                    effectiveName[c] = h6.Length > 0 ? h6 : h5;
                }

                var infoColumnMap = new Dictionary<int, string>();               // col -> campo canônico
                var resultColumns = new List<(int col, string parametro, string categoria)>();

                for (int c = 0; c < colCount; c++)
                {
                    string name = effectiveName[c];
                    if (string.IsNullOrEmpty(name)) continue;

                    if (InfoHeaders.TryGetValue(name, out var canonical))
                    {
                        infoColumnMap[c] = canonical;
                        continue;
                    }

                    // Se o rótulo de grupo (linha 5) é diferente do nome específico (linha 6),
                    // a coluna faz parte de um grupo desdobrado (Demais Parâmetros) -> categoria
                    // = nome do grupo (Agrotóxicos, Cianobactérias, Outros, etc.).
                    // Se são iguais, é uma coluna isolada, sem desdobramento -> Parâmetro Básico.
                    string categoria = groupLabel[c] != name ? groupLabel[c] : "Básico";
                    resultColumns.Add((c, name, categoria));
                }

                if (resultColumns.Count == 0)
                    progress?.Report($"[{year}] Aviso: nenhuma coluna de resultado detectada no cabeçalho.");

                int municipioCol = infoColumnMap
                    .Where(kv => kv.Value == "Municipio")
                    .Select(kv => (int?)kv.Key)
                    .FirstOrDefault() ?? -1;

                if (municipioCol < 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Coluna 'Município' não encontrada no arquivo — marque o checkbox 'Município' antes de baixar.";
                    return result;
                }

                var masterRows = new List<string[]>();

                for (int r = 6; r < sheet.Rows.Count; r++)
                {
                    string municipio = sheet.Rows[r][municipioCol]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(municipio)) continue;

                    string regional = RondoniaRegionais.GetRegion(municipio);

                    var infoValues = new Dictionary<string, string>();
                    foreach (var canonical in MasterHeaders)
                        infoValues[canonical] = string.Empty;

                    infoValues["Municipio"] = municipio;
                    infoValues["Regional"] = regional;
                    infoValues["Ano"] = year;

                    foreach (var kv in infoColumnMap)
                    {
                        string v = sheet.Rows[r][kv.Key]?.ToString()?.Trim() ?? string.Empty;
                        infoValues[kv.Value] = v.Length == 0 ? "-" : v;
                    }

                    // Deriva Dia/Mes de DataColeta (dd/MM/yyyy). Segue a mesma regra: só
                    // preenche se "Data da coleta" foi marcada nesse download; "-" se foi
                    // marcada mas veio vazia; em branco se o campo nem existe no arquivo.
                    if (infoColumnMap.Values.Contains("DataColeta"))
                    {
                        string dataColeta = infoValues["DataColeta"];
                        if (dataColeta == "-")
                        {
                            infoValues["Dia"] = "-";
                            infoValues["Mes"] = "-";
                        }
                        else
                        {
                            var partes = dataColeta.Split('/');
                            infoValues["Dia"] = partes.Length == 3 ? partes[0] : "-";
                            infoValues["Mes"] = partes.Length == 3 ? partes[1] : "-";
                        }
                    }

                    if (resultColumns.Count == 0)
                    {
                        // Nenhuma seção de resultado foi marcada nesse download -> campo
                        // não selecionado, fica em branco (mesma regra das colunas de info).
                        masterRows.Add(BuildRow(infoValues, string.Empty, string.Empty, string.Empty));
                        continue;
                    }

                    bool anyResultForThisSample = false;
                    foreach (var (col, parametro, categoria) in resultColumns)
                    {
                        string valor = sheet.Rows[r][col]?.ToString()?.Trim() ?? string.Empty;
                        if (valor.Length == 0 || valor == "-") continue; // sem resultado nesse parâmetro pra essa amostra

                        masterRows.Add(BuildRow(infoValues, parametro, categoria, valor));
                        anyResultForThisSample = true;
                    }

                    // Seção de resultado foi selecionada, mas nenhum parâmetro teve valor
                    // nessa amostra -> "-" (selecionado, porém vazio), não branco. Preserva
                    // a amostra no master em vez de deixá-la desaparecer.
                    if (!anyResultForThisSample)
                        masterRows.Add(BuildRow(infoValues, "-", "-", "-"));
                }

                progress?.Report($"[{year}] Atualizando planilha mestre de amostras analisadas...");
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                ExcelPackage package;
                ExcelWorksheet ws;

                if (File.Exists(masterPath))
                {
                    package = new ExcelPackage(new FileInfo(masterPath));
                    ws = package.Workbook.Worksheets[0];

                    // Sempre realinha o cabeçalho com o MasterHeaders atual. Protege contra
                    // desalinhamento se o schema mudar (colunas novas/reordenadas) e o
                    // arquivo já existir de uma versão anterior do processor.
                    for (int c = 0; c < MasterHeaders.Length; c++)
                        ws.Cells[1, c + 1].Value = MasterHeaders[c];

                    int lastRow = ws.Dimension?.End.Row ?? 1;
                    int anoCol = Array.IndexOf(MasterHeaders, "Ano") + 1;
                    for (int r = lastRow; r >= 2; r--)
                    {
                        if (ws.Cells[r, 1].Text == string.Empty) continue;
                        if (ws.Cells[r, anoCol].Text.Trim() == year)
                            ws.DeleteRow(r);
                    }
                }
                else
                {
                    package = new ExcelPackage();
                    ws = package.Workbook.Worksheets.Add("Dados");

                    for (int c = 0; c < MasterHeaders.Length; c++)
                        ws.Cells[1, c + 1].Value = MasterHeaders[c];
                }

                int nextRow = (ws.Dimension?.End.Row ?? 1) + 1;
                foreach (var row in masterRows)
                {
                    for (int c = 0; c < row.Length; c++)
                        ws.Cells[nextRow, c + 1].Value = row[c];
                    nextRow++;
                }

                await package.SaveAsAsync(new FileInfo(masterPath));
                package.Dispose();

                result.RowsAdded = masterRows.Count;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static string[] BuildRow(Dictionary<string, string> infoValues, string parametro, string categoriaParametro, string valor)
        {
            var row = new string[MasterHeaders.Length];
            for (int i = 0; i < MasterHeaders.Length; i++)
            {
                string key = MasterHeaders[i];
                row[i] = key switch
                {
                    "Parametro" => parametro,
                    "CategoriaParametro" => categoriaParametro,
                    "Valor" => valor,
                    _ => infoValues.TryGetValue(key, out var v) ? v : string.Empty
                };
            }
            return row;
        }

        private static string NormalizeHeader(string header) =>
            Regex.Replace(header.Trim(), @"\s+", " ");
    }
}
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

        // Método auxiliar para tratar valores nulos, vazios ou contendo apenas "-"
        private static string CleanValue(object? cellValue)
        {
            string val = cellValue?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(val) || val == "-")
            {
                return "Ignorado";
            }
            return val;
        }

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

                var effectiveName = new string[colCount];
                var groupLabel = new string[colCount];

                string lastGroup = string.Empty;
                for (int c = 0; c < colCount; c++)
                {
                    string h5 = NormalizeHeader(sheet.Rows[4][c]?.ToString() ?? string.Empty);
                    string h6 = NormalizeHeader(sheet.Rows[5][c]?.ToString() ?? string.Empty);

                    if (h5.Length > 0) lastGroup = h5;
                    groupLabel[c] = lastGroup;
                    effectiveName[c] = h6.Length > 0 ? h6 : h5;
                }

                var infoColumnMap = new Dictionary<int, string>();
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
                    string municipio = CleanValue(sheet.Rows[r][municipioCol]);
                    if (municipio == "Ignorado") continue;

                    string regional = RondoniaRegionais.GetRegion(municipio);

                    var infoValues = new Dictionary<string, string>();
                    foreach (var canonical in MasterHeaders)
                        infoValues[canonical] = string.Empty;

                    infoValues["Municipio"] = municipio;
                    infoValues["Regional"] = regional;
                    infoValues["Ano"] = year;

                    foreach (var kv in infoColumnMap)
                    {
                        // Aplica o CleanValue para substituir vazios e "-" por "Ignorado"
                        infoValues[kv.Value] = CleanValue(sheet.Rows[r][kv.Key]);
                    }

                    // Deriva Dia/Mes de DataColeta
                    if (infoColumnMap.Values.Contains("DataColeta"))
                    {
                        string dataColeta = infoValues["DataColeta"];
                        if (dataColeta == "Ignorado")
                        {
                            infoValues["Dia"] = "Ignorado";
                            infoValues["Mes"] = "Ignorado";
                        }
                        else
                        {
                            var partes = dataColeta.Split('/');
                            infoValues["Dia"] = partes.Length == 3 ? partes[0] : "Ignorado";
                            infoValues["Mes"] = partes.Length == 3 ? partes[1] : "Ignorado";
                        }
                    }

                    if (resultColumns.Count == 0)
                    {
                        masterRows.Add(BuildRow(infoValues, string.Empty, string.Empty, string.Empty));
                        continue;
                    }

                    bool anyResultForThisSample = false;
                    foreach (var (col, parametro, categoria) in resultColumns)
                    {
                        // Limpa o valor do parâmetro
                        string valor = CleanValue(sheet.Rows[r][col]);

                        // Ignora parâmetros sem valor nesta amostra
                        if (valor == "Ignorado") continue;

                        masterRows.Add(BuildRow(infoValues, parametro, categoria, valor));
                        anyResultForThisSample = true;
                    }

                    if (!anyResultForThisSample)
                        masterRows.Add(BuildRow(infoValues, "Ignorado", "Ignorado", "Ignorado"));
                }

                progress?.Report($"[{year}] Atualizando planilha mestre de amostras analisadas...");
                ExcelLicense.Ensure();

                ExcelPackage package;
                ExcelWorksheet ws;

                if (File.Exists(masterPath))
                {
                    package = new ExcelPackage(new FileInfo(masterPath));
                    ws = package.Workbook.Worksheets[0];

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
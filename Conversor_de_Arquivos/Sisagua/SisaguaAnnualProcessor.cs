using System.Data;
using ExcelDataReader;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class AnnualProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAdded { get; set; }
        public string Parameter { get; set; } = string.Empty;
    }

    public class SisaguaAnnualDataProcessor
    {
        public async Task<AnnualProcessingResult> ProcessAsync(
            string rawFilePath,
            string masterPath,
            IProgress<string>? progress = null)
        {
            var result = new AnnualProcessingResult();

            try
            {
                progress?.Report("Lendo arquivo bruto anual...");
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                DataSet ds;
                using (var stream = File.Open(rawFilePath,
                           FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                    ds = reader.AsDataSet();

                var sheet = ds.Tables[0];

                string period = sheet.Rows[2][1]?.ToString() ?? string.Empty;
                string parameter = sheet.Rows[3][1]?.ToString() ?? string.Empty;
                result.Parameter = parameter;

                var years = new List<(int Year, int ColIndex)>();
                for (int c = 5; c < sheet.Columns.Count; c++)
                {
                    string header = sheet.Rows[6][c]?.ToString()?.Trim() ?? string.Empty;
                    if (int.TryParse(header, out int year))
                        years.Add((year, c));
                }

                int startYear = years.Count > 0 ? years.Min(a => a.Year) : DateTime.Now.Year;
                int endYear   = years.Count > 0 ? years.Max(a => a.Year) : DateTime.Now.Year;

                progress?.Report($"Processando {parameter} ({period})...");

                var rows = new List<string[]>();

                for (int i = 0; i < 52; i++)
                {
                    var rowPerc = sheet.Rows[7 + i];
                    var rowN = sheet.Rows[62 + i];

                    string municipality = rowPerc[0]?.ToString() ?? string.Empty;
                    string codIbge = rowPerc[1]?.ToString() ?? string.Empty;
                    string population = rowPerc[2]?.ToString() ?? string.Empty;
                    string region = RondoniaRegionais.GetRegion(municipality);
                    string obrigatorio = CleanN(rowN[3]?.ToString() ?? string.Empty);

                    foreach (var (year, colIndex) in years)
                    {
                        string rawPerc = rowPerc[colIndex]?.ToString() ?? string.Empty;
                        string rawN = rowN[colIndex]?.ToString() ?? string.Empty;

                        bool noData = string.IsNullOrWhiteSpace(rawPerc)
                                   && string.IsNullOrWhiteSpace(rawN);
                        if (noData) continue;

                        rows.Add([
                            municipality,
                            codIbge,
                            population,
                            region,
                            year.ToString(),
                            CleanPercentage(rawPerc),
                            CleanN(rawN),
                            obrigatorio,
                            parameter
                        ]);
                    }
                }

                progress?.Report("Atualizando planilha mestre anual...");
                ExcelLicense.Ensure();

                ExcelPackage package;
                ExcelWorksheet ws;

                if (File.Exists(masterPath))
                {
                    package = new ExcelPackage(new FileInfo(masterPath));
                    ws = package.Workbook.Worksheets[0];

                    int lastRow = ws.Dimension?.End.Row ?? 1;
                    for (int r = lastRow; r >= 2; r--)
                    {
                        if (int.TryParse(ws.Cells[r, 5].Text, out int yearRow)
                            && yearRow >= startYear && yearRow <= endYear)
                            ws.DeleteRow(r);
                    }
                }
                else
                {
                    package = new ExcelPackage();
                    ws = package.Workbook.Worksheets.Add("Dados");

                    string[] headers =
                        ["Município", "CodIBGE", "Populacao", "Regional",
                         "Periodo", "Percentual", "N", "Obrigatorio", "Parametro"];
                    for (int c = 0; c < headers.Length; c++)
                        ws.Cells[1, c + 1].Value = headers[c];
                }

                int nextRow = (ws.Dimension?.End.Row ?? 1) + 1;
                foreach (var row in rows.OrderBy(r => r[4]))
                {
                    for (int c = 0; c < row.Length; c++)
                        ws.Cells[nextRow, c + 1].Value = row[c];
                    nextRow++;
                }

                await package.SaveAsAsync(new FileInfo(masterPath));
                package.Dispose();

                result.RowsAdded = rows.Count;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static string CleanPercentage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-") return "0";
            return raw.Replace("%", " ").Trim();
        }

        private static string CleanN(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-") return "0";
            return raw.Trim();
        }
    }
}

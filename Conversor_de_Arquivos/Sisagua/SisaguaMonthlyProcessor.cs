using System.Data;
using ExcelDataReader;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAdded { get; set; }
        public string Parameter { get; set; } = string.Empty;
    }

    public class SisaguaDataProcessor
    {
        private static readonly string[] Months =
            ["JAN", "FEV", "MAR", "ABR", "MAI", "JUN",
             "JUL", "AGO", "SET", "OUT", "NOV", "DEZ"];

        public async Task<ProcessingResult> ProcessAsync(
            string rawFilePath,
            string masterPath,
            IProgress<string>? progress = null)
        {
            var result = new ProcessingResult();

            try
            {
                progress?.Report("Lendo arquivo bruto...");
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                DataSet ds;
                using (var stream = File.Open(rawFilePath,
                           FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                    ds = reader.AsDataSet();

                var sheet = ds.Tables[0];

                string year = sheet.Rows[2][1]?.ToString()
                                   ?? DateTime.Now.Year.ToString();
                string parameter = sheet.Rows[4][1]?.ToString()
                                   ?? string.Empty;
                result.Parameter = parameter;

                progress?.Report($"Processando {parameter} ({year})...");

                var rows = new List<string[]>();

                for (int i = 0; i < 52; i++)
                {
                    var rowPerc = sheet.Rows[8 + i];
                    var rowN = sheet.Rows[63 + i];

                    string municipality = rowPerc[0]?.ToString() ?? string.Empty;
                    string codIbge = rowPerc[1]?.ToString() ?? string.Empty;
                    string population = rowPerc[2]?.ToString() ?? string.Empty;
                    string region = RondoniaRegionais.GetRegion(municipality);
                    string obrigatorio = CleanN(rowN[3]?.ToString() ?? string.Empty);

                    for (int m = 0; m < 12; m++)
                    {
                        string rawPerc = rowPerc[5 + m]?.ToString() ?? string.Empty;
                        string rawN = rowN[5 + m]?.ToString() ?? string.Empty;

                        bool noData = string.IsNullOrWhiteSpace(rawPerc)
                                   && string.IsNullOrWhiteSpace(rawN);
                        if (noData) continue;

                        rows.Add([
                            municipality,
                            codIbge,
                            population,
                            region,
                            year,
                            Months[m],
                            CleanPercentage(rawPerc),
                            CleanN(rawN),
                            obrigatorio,
                            parameter
                        ]);
                    }
                }

                progress?.Report("Atualizando planilha mestre...");
                ExcelLicense.Ensure();

                ExcelPackage package;
                ExcelWorksheet ws;

                string[] headers =
                    ["Município", "CodIBGE", "Populacao", "Regional",
                    "Ano", "Mes", "Percentual", "N", "Obrigatorio", "Parametro"];

            if (File.Exists(masterPath))
                {
                    package = new ExcelPackage(new FileInfo(masterPath));
                    ws = package.Workbook.Worksheets[0];

                    // Sempre realinha o header, mesmo em arquivo existente
                    for (int c = 0; c < headers.Length; c++)
                        ws.Cells[1, c + 1].Value = headers[c];

                    int lastRow = ws.Dimension?.End.Row ?? 1;
                    for (int r = lastRow; r >= 2; r--)
                    {
                        if (ws.Cells[r, 5].Text == year)
                            ws.DeleteRow(r);
                    }
                }
                else
                {
                    package = new ExcelPackage();
                    ws = package.Workbook.Worksheets.Add("Dados");

                    for (int c = 0; c < headers.Length; c++)
                        ws.Cells[1, c + 1].Value = headers[c];
                }

                int nextRow = (ws.Dimension?.End.Row ?? 1) + 1;
                foreach (var row in rows)
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

using System.Data;
using ExcelDataReader;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class ImplementationProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAdded { get; set; }
        public string Year { get; set; } = string.Empty;
    }

    public class SisaguaImplementationProcessor
    {
        public async Task<ImplementationProcessingResult> ProcessAsync(
            string rawFilePath,
            string masterPath,
            IProgress<string>? progress = null)
        {
            var result = new ImplementationProcessingResult();

            try
            {
                progress?.Report("Lendo arquivo bruto de implementação...");
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                DataSet ds;
                using (var stream = File.Open(rawFilePath,
                           FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                    ds = reader.AsDataSet();

                var sheet = ds.Tables[0];

                string year = sheet.Rows[5][1]?.ToString()?.Trim() ?? string.Empty;
                result.Year = year;

                progress?.Report($"Processando implementação ({year})...");

                var rows = new List<string[]>();

                for (int r = 8; r < sheet.Rows.Count; r++)
                {
                    string municipality = sheet.Rows[r][0]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(municipality)) break;

                    string codIbge = sheet.Rows[r][1]?.ToString()?.Trim() ?? string.Empty;
                    string population = sheet.Rows[r][2]?.ToString()?.Trim() ?? string.Empty;
                    string cadastro = sheet.Rows[r][3]?.ToString()?.Trim() ?? string.Empty;
                    string controle = sheet.Rows[r][4]?.ToString()?.Trim() ?? string.Empty;
                    string vigilancia = sheet.Rows[r][5]?.ToString()?.Trim() ?? string.Empty;
                    string region = RondoniaRegionais.GetRegion(municipality);

                    rows.Add([
                        municipality,
                        codIbge,
                        population,
                        region,
                        year,
                        cadastro,
                        controle,
                        vigilancia
                    ]);
                }

                progress?.Report("Atualizando planilha mestre de implementação...");
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
                        if (ws.Cells[r, 1].Text == string.Empty) continue;
                        if (ws.Cells[r, 5].Text.Trim() == year)
                            ws.DeleteRow(r);
                    }
                }
                else
                {
                    package = new ExcelPackage();
                    ws = package.Workbook.Worksheets.Add("Dados");

                    string[] headers =
                        ["Municipio", "CodIBGE", "Populacao", "Regional",
                         "Ano", "Cadastro", "Controle", "Vigilancia"];
                    for (int c = 0; c < headers.Length; c++)
                        ws.Cells[1, c + 1].Value = headers[c];
                }

                int nextRow = (ws.Dimension?.End.Row ?? 1) + 1;
                foreach (var row in rows.OrderBy(r => r[0]))
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
    }
}
using System.Globalization;
using System.Text;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class FocosCalorDataProcessor
    {
        public (int Year, int Week)? ReadMaxWeek(string masterPath)
        {
            if (!File.Exists(masterPath)) return null;

            ExcelLicense.Ensure();
            using var pkg = new ExcelPackage(new FileInfo(masterPath));
            if (pkg.Workbook.Worksheets.Count == 0) return null;

            var ws = pkg.Workbook.Worksheets[0];
            int lastRow = ws.Dimension?.End.Row ?? 1;

            int maxYear = 0, maxWeek = 0;
            bool found = false;

            for (int r = 2; r <= lastRow; r++)
            {
                int year = GetInt(ws, r, 3);
                int week = GetInt(ws, r, 4);
                if (year == 0) continue;

                if (!found || year > maxYear || (year == maxYear && week > maxWeek))
                {
                    maxYear = year;
                    maxWeek = week;
                    found   = true;
                }
            }

            return found ? (maxYear, maxWeek) : null;
        }

        public async Task AppendWeekAsync(WeekHotspots weekData, string masterPath, IProgress<string>? progress = null)
        {
            ExcelLicense.Ensure();

            var fileInfo = new FileInfo(masterPath);
            fileInfo.Directory?.Create();

            using var pkg = fileInfo.Exists
                ? new ExcelPackage(fileInfo)
                : new ExcelPackage();

            ExcelWorksheet ws;
            if (pkg.Workbook.Worksheets.Count == 0)
            {
                ws = pkg.Workbook.Worksheets.Add("Dados");
                ws.Cells[1, 1].Value = "Município";
                ws.Cells[1, 2].Value = "Regional";
                ws.Cells[1, 3].Value = "Ano";
                ws.Cells[1, 4].Value = "Semana";
                ws.Cells[1, 5].Value = "Data_Inicio_Semana";
                ws.Cells[1, 6].Value = "Data_Final_Semana";
                ws.Cells[1, 7].Value = "Focos";
            }
            else
            {
                ws = pkg.Workbook.Worksheets[0];
            }

            int lastRow = ws.Dimension?.End.Row ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                if (GetInt(ws, r, 3) == weekData.Year && GetInt(ws, r, 4) == weekData.Week)
                    return;
            }

            int nextRow = lastRow + 1;

            if (weekData.Hotspots.Count == 0)
            {
                WriteRow(ws, nextRow, "Sem Registro", "Sem Registro", weekData);
            }
            else
            {
                foreach (var hotspot in weekData.Hotspots)
                {
                    string municipalityNorm = StripAccents(hotspot.Municipality.Trim().ToUpperInvariant());
                    string region = RondoniaRegionais.GetRegion(municipalityNorm);

                    if (string.IsNullOrWhiteSpace(region) || region == "Não identificado")
                        region = "Sem Registro";

                    WriteRow(ws, nextRow, hotspot.Municipality, region, weekData, hotspot.Hotspots);
                    nextRow++;
                }
            }

            await pkg.SaveAsAsync(fileInfo);
        }

        private static void WriteRow(ExcelWorksheet ws, int row,
            string municipality, string region, WeekHotspots weekData, int hotspots = 0)
        {
            ws.Cells[row, 1].Value = municipality;
            ws.Cells[row, 2].Value = region;
            ws.Cells[row, 3].Value = weekData.Year;
            ws.Cells[row, 4].Value = weekData.Week;
            ws.Cells[row, 5].Value = weekData.Start.Date;
            ws.Cells[row, 5].Style.Numberformat.Format = "dd/MM/yyyy";
            ws.Cells[row, 6].Value = weekData.End.Date;
            ws.Cells[row, 6].Style.Numberformat.Format = "dd/MM/yyyy";
            ws.Cells[row, 7].Value = hotspots;
        }

        private static int GetInt(ExcelWorksheet ws, int row, int col)
        {
            object? v = ws.Cells[row, col].Value;
            return v switch
            {
                int    i => i,
                double d => (int)Math.Round(d),
                _        => int.TryParse(ws.Cells[row, col].Text, out int n) ? n : 0,
            };
        }

        private static string StripAccents(string text)
        {
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}

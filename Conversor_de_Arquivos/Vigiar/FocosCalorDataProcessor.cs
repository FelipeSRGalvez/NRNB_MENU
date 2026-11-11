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

        // Pares (Ano, Semana) já presentes na planilha mestre.
        public HashSet<(int Year, int Week)> ReadRecordedWeeks(string masterPath)
        {
            var weeks = new HashSet<(int, int)>();
            if (!File.Exists(masterPath)) return weeks;

            ExcelLicense.Ensure();
            using var pkg = new ExcelPackage(new FileInfo(masterPath));
            if (pkg.Workbook.Worksheets.Count == 0) return weeks;

            var ws = pkg.Workbook.Worksheets[0];
            int lastRow = ws.Dimension?.End.Row ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                int year = GetInt(ws, r, 3);
                int week = GetInt(ws, r, 4);
                if (year > 0 && week > 0) weeks.Add((year, week));
            }
            return weeks;
        }

        // Substitui as linhas das semanas informadas pelos dados novos (as semanas são baixadas de novo
        // a cada sessão de coleta porque o INPE revisa semanas anteriores) e mantém as demais linhas.
        // A planilha é regravada em ordem de ano/semana, então nunca há linhas duplicadas.
        public async Task UpsertWeeksAsync(IReadOnlyList<WeekHotspots> weeks, string masterPath)
        {
            if (weeks.Count == 0) return;

            ExcelLicense.Ensure();

            var fileInfo = new FileInfo(masterPath);
            fileInfo.Directory?.Create();

            using var pkg = fileInfo.Exists ? new ExcelPackage(fileInfo) : new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Count == 0
                ? pkg.Workbook.Worksheets.Add("Dados")
                : pkg.Workbook.Worksheets[0];

            var replaced = weeks.Select(w => (w.Year, w.Week)).ToHashSet();

            // Linhas das semanas que não serão substituídas: (ano, semana, 7 valores de célula).
            var kept = new List<(int Year, int Week, object?[] Values)>();
            int lastRow = ws.Dimension?.End.Row ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                int year = GetInt(ws, r, 3);
                int week = GetInt(ws, r, 4);
                if (year == 0 || replaced.Contains((year, week))) continue;
                kept.Add((year, week, Enumerable.Range(1, 7).Select(c => ws.Cells[r, c].Value).ToArray()));
            }

            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].Clear();

            ws.Cells[1, 1].Value = "Município";
            ws.Cells[1, 2].Value = "Regional";
            ws.Cells[1, 3].Value = "Ano";
            ws.Cells[1, 4].Value = "Semana";
            ws.Cells[1, 5].Value = "Data_Inicio_Semana";
            ws.Cells[1, 6].Value = "Data_Final_Semana";
            ws.Cells[1, 7].Value = "Focos";

            var fresh = new List<(int Year, int Week, object?[] Values)>();
            foreach (var weekData in weeks.GroupBy(w => (w.Year, w.Week)).Select(g => g.Last()))
            {
                if (weekData.Hotspots.Count == 0)
                {
                    fresh.Add((weekData.Year, weekData.Week,
                        RowValues("Sem Registro", "Sem Registro", weekData, 0)));
                    continue;
                }

                foreach (var hotspot in weekData.Hotspots)
                {
                    string municipalityNorm = StripAccents(hotspot.Municipality.Trim().ToUpperInvariant());
                    string region = RondoniaRegionais.GetRegion(municipalityNorm);
                    if (string.IsNullOrWhiteSpace(region) || region == "Não identificado")
                        region = "Sem Registro";

                    fresh.Add((weekData.Year, weekData.Week,
                        RowValues(hotspot.Municipality, region, weekData, hotspot.Hotspots)));
                }
            }

            int row = 2;
            foreach (var item in kept.Concat(fresh).OrderBy(x => x.Year).ThenBy(x => x.Week))
            {
                for (int c = 0; c < 7; c++)
                    ws.Cells[row, c + 1].Value = item.Values[c];
                ws.Cells[row, 5].Style.Numberformat.Format = "dd/MM/yyyy";
                ws.Cells[row, 6].Style.Numberformat.Format = "dd/MM/yyyy";
                row++;
            }

            await pkg.SaveAsAsync(fileInfo);
        }

        private static object?[] RowValues(string municipality, string region, WeekHotspots weekData, int hotspots)
            => [municipality, region, weekData.Year, weekData.Week, weekData.Start.Date, weekData.End.Date, hotspots];

        public Task AppendWeekAsync(WeekHotspots weekData, string masterPath, IProgress<string>? progress = null)
            => AppendWeeksAsync([weekData], masterPath);

        // Abre e salva o mestre uma única vez para o lote inteiro. Semana já presente no
        // mestre (ou repetida no lote) é ignorada, então nunca há linhas duplicadas.
        // Retorna quantas semanas foram de fato adicionadas.
        public async Task<int> AppendWeeksAsync(IReadOnlyList<WeekHotspots> weeks, string masterPath)
        {
            if (weeks.Count == 0) return 0;

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

            var recorded = new HashSet<(int, int)>();
            for (int r = 2; r <= lastRow; r++)
                recorded.Add((GetInt(ws, r, 3), GetInt(ws, r, 4)));

            int nextRow = lastRow + 1;
            int added = 0;

            foreach (var weekData in weeks.OrderBy(w => w.Year).ThenBy(w => w.Week))
            {
                if (!recorded.Add((weekData.Year, weekData.Week)))
                    continue;

                if (weekData.Hotspots.Count == 0)
                {
                    WriteRow(ws, nextRow, "Sem Registro", "Sem Registro", weekData);
                    nextRow++;
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
                added++;
            }

            if (added > 0)
                await pkg.SaveAsAsync(fileInfo);

            return added;
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

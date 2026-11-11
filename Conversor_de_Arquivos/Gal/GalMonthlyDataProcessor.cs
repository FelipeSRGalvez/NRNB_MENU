using System.Globalization;
using System.Text;
using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    public class GalMonthlyProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsProcessed { get; set; }
        public int MunicipalitiesFound { get; set; }
    }

    public class GalMonthlyDataProcessor
    {
        private static readonly Dictionary<string, (string Name, int Order)> _monthMap = new()
        {
            ["Jan"]  = ("Janeiro",    1),
            ["Fev"]  = ("Fevereiro",  2),
            ["Mar"]  = ("Março",      3),
            ["Abr"]  = ("Abril",      4),
            ["Maio"] = ("Maio",       5),
            ["Jun"]  = ("Junho",      6),
            ["Jul"]  = ("Julho",      7),
            ["Ago"]  = ("Agosto",     8),
            ["Set"]  = ("Setembro",   9),
            ["Out"]  = ("Outubro",   10),
            ["Nov"]  = ("Novembro",  11),
            ["Dez"]  = ("Dezembro",  12),
        };

        private static readonly Dictionary<string, int> _orderByName =
            _monthMap.ToDictionary(kv => kv.Value.Name, kv => kv.Value.Order);

        private sealed class GalRow
        {
            public string Municipality { get; init; } = string.Empty;
            public string Region       { get; init; } = string.Empty;
            public int    Year         { get; init; }
            public string Month        { get; init; } = string.Empty;
            public int    MonthOrder   { get; init; }
            public int    Tot          { get; init; }
            public int    Sat          { get; init; }
            public int    Ins          { get; init; }
        }

        public async Task<GalMonthlyProcessingResult> ProcessAsync(
            string pdfPath,
            string masterPath,
            IProgress<string>? progress = null)
        {
            var result = new GalMonthlyProcessingResult();
            try
            {
                var (months, records) = ExtractWithPython(pdfPath, progress);
                result.MunicipalitiesFound = records.Count;

                progress?.Report($"Encontrados {records.Count} municípios. Gerando linhas...");
                var newRows = GenerateLongRows(records, months);
                result.RowsProcessed = newRows.Count;

                progress?.Report("Atualizando arquivo mestre...");
                await UpdateMasterAsync(masterPath, newRows, months, progress);

                result.Success = true;
                progress?.Report("Concluído.");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        private static (
            List<(string Abbrev, string Name, int Year, int Order)> Months,
            List<(string Municipality, int[] Values)> Records
        ) ExtractWithPython(string pdfPath, IProgress<string>? progress)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gal_extract.exe");
            string pyPath  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gal_extract.py");

            string processFile, processArgs;
            if (File.Exists(exePath))
            {
                processFile = exePath;
                processArgs = $"\"{pdfPath}\"";
            }
            else if (File.Exists(pyPath))
            {
                processFile = FindPython();
                processArgs = $"\"{pyPath}\" \"{pdfPath}\"";
            }
            else
            {
                throw new FileNotFoundException(
                    "Extrator GAL não encontrado. Gere gal_extract.exe com PyInstaller " +
                    "ou mantenha gal_extract.py com Python instalado.");
            }

            progress?.Report("Extraindo tabela do PDF via pdfplumber...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = processFile,
                Arguments              = processArgs,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Falha ao iniciar Python.");

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                throw new InvalidOperationException(
                    $"gal_extract.py falhou (código {proc.ExitCode}): {stderr}");

            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var allLines = doc.RootElement.EnumerateArray()
                .Select(row => row.EnumerateArray()
                    .Select(cell => cell.GetString() ?? string.Empty)
                    .ToList())
                .ToList();

            if (allLines.Count < 2)
                throw new InvalidDataException(
                    "Tabela retornada pelo extrator Python não contém dados.");

            var headerRow = allLines[0];
            var dataRows  = allLines.Skip(1).ToList();

            var months         = new List<(string Abbrev, string Name, int Year, int Order)>();
            var monthColIndices = new List<int>();

            for (int i = 0; i < headerRow.Count; i++)
            {
                foreach (var token in
                    headerRow[i].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int sep = token.IndexOf('/');
                    if (sep <= 0) continue;

                    string abbrev  = token[..sep];
                    string yearStr = token[(sep + 1)..];

                    if (_monthMap.TryGetValue(abbrev, out var info)
                        && int.TryParse(yearStr, out int year))
                    {
                        months.Add((abbrev, info.Name, year, info.Order));
                        monthColIndices.Add(i);
                        break;
                    }
                }
            }

            if (months.Count == 0)
                throw new InvalidDataException(
                    "Nenhum mês encontrado no cabeçalho da tabela extraída.");

            progress?.Report(
                $"Meses: {string.Join(", ", months.Select(m => m.Abbrev + "/" + m.Year))}");

            int numValues = months.Count * 3;
            var records   = new List<(string Municipality, int[] Values)>();

            foreach (var row in dataRows)
            {
                if (row.Count == 0) continue;

                string name = row[0].Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var vals = new List<int>();
                foreach (int col in monthColIndices)
                {
                    for (int offset = 0; offset < 3; offset++)
                    {
                        int c = col + offset;
                        vals.Add(c < row.Count && int.TryParse(row[c], out int v) ? v : 0);
                    }
                }

                if (vals.Count < numValues) continue;

                string canonical = CanonizeMunicipality(NormalizeMunicipalityName(name));
                if (string.IsNullOrWhiteSpace(canonical)
                    || canonical.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
                    continue;

                records.Add((canonical, [.. vals.Take(numValues)]));
            }

            return (months, records);
        }

        private static string FindPython()
        {
            foreach (var candidate in new[] { "py", "python", "python3" })
            {
                try
                {
                    using var p = System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName               = candidate,
                            Arguments              = "--version",
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            UseShellExecute        = false,
                            CreateNoWindow         = true,
                        })!;
                    p.WaitForExit(3000);
                    if (p.ExitCode == 0) return candidate;
                }
                catch { /* try next */ }
            }
            throw new InvalidOperationException(
                "Python não encontrado no PATH. Instale o Python e tente novamente.");
        }

        // Score = matched_words² / canonical_word_count. Threshold ≥ 1.0 avoids false positives
        // from orphaned single-word fragments (e.g. a stray "D'OESTE").
        private static string CanonizeMunicipality(string parsedName)
        {
            if (string.IsNullOrWhiteSpace(parsedName)) return parsedName;

            static string Norm(string s) =>
                StripAccents(s).ToUpperInvariant()
                    .Replace('\'', ' ').Replace('-', ' ');

            var parsedWords = new HashSet<string>(
                Norm(parsedName).Split(' ', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            if (parsedWords.Count == 0) return parsedName;

            string? bestKey   = null;
            double  bestScore = 0;

            foreach (var (key, _) in RondoniaRegionais.RegionalMap)
            {
                string[] keyWords = Norm(key)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                int matched = keyWords.Count(w => parsedWords.Contains(w));
                if (matched == 0) continue;

                double score = (double)(matched * matched) / keyWords.Length;
                if (score > bestScore) { bestScore = score; bestKey = key; }
            }

            return bestKey != null && bestScore >= 1.0 ? bestKey : parsedName;
        }

        private static List<GalRow> GenerateLongRows(
            List<(string Municipality, int[] Values)> records,
            List<(string Abbrev, string Name, int Year, int Order)> months)
        {
            var rows = new List<GalRow>();

            foreach (var (municipality, values) in records)
            {
                string key    = StripAccents(municipality.ToUpper());
                string region = RondoniaRegionais.GetRegion(key);

                for (int m = 0; m < months.Count; m++)
                {
                    int baseIdx = m * 3;
                    if (baseIdx + 2 >= values.Length) break;

                    rows.Add(new GalRow
                    {
                        Municipality = municipality,
                        Region       = region,
                        Year         = months[m].Year,
                        Month        = months[m].Name,
                        MonthOrder   = months[m].Order,
                        Tot          = values[baseIdx],
                        Sat          = values[baseIdx + 1],
                        Ins          = values[baseIdx + 2],
                    });
                }
            }

            return rows;
        }

        private static async Task UpdateMasterAsync(
            string masterPath,
            List<GalRow> newRows,
            List<(string Abbrev, string Name, int Year, int Order)> months,
            IProgress<string>? progress)
        {
            static int ReadInt(ExcelWorksheet ws, int row, int col)
            {
                object? v = ws.Cells[row, col].Value;
                return v switch
                {
                    int    i => i,
                    double d => (int)Math.Round(d),
                    _        => int.TryParse(ws.Cells[row, col].Text, out int n) ? n : 0,
                };
            }

            ExcelLicense.Ensure();

            var pdfMonthYear = new HashSet<(int Year, string Month)>(
                months.Select(m => (m.Year, m.Name)));

            var existingRows = new List<GalRow>();

            if (File.Exists(masterPath))
            {
                progress?.Report("Lendo linhas existentes do mestre...");
                using var pkgRead = new ExcelPackage(new FileInfo(masterPath));
                var wsRead   = pkgRead.Workbook.Worksheets[0];
                int lastRow  = wsRead.Dimension?.End.Row ?? 1;

                for (int r = 2; r <= lastRow; r++)
                {
                    string municipality = wsRead.Cells[r, 1].Text;
                    if (string.IsNullOrWhiteSpace(municipality)) continue;

                    string region = wsRead.Cells[r, 2].Text;
                    int    year   = ReadInt(wsRead, r, 3);
                    string month  = wsRead.Cells[r, 4].Text;

                    if (year == 0 || string.IsNullOrWhiteSpace(month)) continue;

                    if (pdfMonthYear.Contains((year, month))) continue;

                    _orderByName.TryGetValue(month, out int monthOrder);

                    existingRows.Add(new GalRow
                    {
                        Municipality = municipality,
                        Region       = region,
                        Year         = year,
                        Month        = month,
                        MonthOrder   = monthOrder,
                        Tot          = ReadInt(wsRead, r, 5),
                        Sat          = ReadInt(wsRead, r, 6),
                        Ins          = ReadInt(wsRead, r, 7),
                    });
                }
            }

            var allRows = existingRows
                .Concat(newRows)
                .OrderBy(r => r.Municipality)
                .ThenBy(r => r.Year)
                .ThenBy(r => r.MonthOrder)
                .ToList();

            progress?.Report($"Salvando {allRows.Count} linhas no mestre...");

            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("Dados");

            ws.Cells[1, 1].Value = "Município";
            ws.Cells[1, 2].Value = "Regional";
            ws.Cells[1, 3].Value = "Ano";
            ws.Cells[1, 4].Value = "Mês";
            ws.Cells[1, 5].Value = "TOT";
            ws.Cells[1, 6].Value = "SAT";
            ws.Cells[1, 7].Value = "INS";

            for (int r = 0; r < allRows.Count; r++)
            {
                var l   = allRows[r];
                int row = r + 2;
                ws.Cells[row, 1].Value = l.Municipality;
                ws.Cells[row, 2].Value = l.Region;
                ws.Cells[row, 3].Value = l.Year;
                ws.Cells[row, 4].Value = l.Month;
                ws.Cells[row, 5].Value = l.Tot;
                ws.Cells[row, 6].Value = l.Sat;
                ws.Cells[row, 7].Value = l.Ins;
            }

            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

            await pkg.SaveAsAsync(new FileInfo(masterPath));
        }

        // Normalizes municipality names:
        //  • strips control chars
        //  • collapses any run of apostrophe-like characters into a single ASCII apostrophe
        //  • removes the space that sometimes appears before or after the apostrophe
        private static string NormalizeMunicipalityName(string name)
        {
            var sb       = new StringBuilder(name.Length);
            bool prevApos = false;
            bool prevSpc  = false;

            foreach (char c in name)
            {
                if (char.IsControl(c)) { prevApos = false; prevSpc = false; continue; }

                bool isApos = c is '\'' or '"'
                    or '\u2018' or '\u2019'
                    or '\u201C' or '\u201D'
                    or '\u2032' or '\u2033'
                    or '\u02BC' or '\u00B4';

                if (isApos)
                {
                    if (prevSpc && sb.Length > 0 && sb[sb.Length - 1] == ' ')
                        sb.Length--;

                    if (!prevApos)
                        sb.Append('\'');

                    prevApos = true;
                    prevSpc  = false;
                }
                else if (c == ' ')
                {
                    if (!prevApos)
                    {
                        sb.Append(' ');
                        prevSpc = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    prevApos = false;
                    prevSpc  = false;
                }
            }

            return sb.ToString().Trim();
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

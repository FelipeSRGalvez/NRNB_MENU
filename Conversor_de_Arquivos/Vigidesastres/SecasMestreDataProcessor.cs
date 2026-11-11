using System.Globalization;
using System.Text;
using OfficeOpenXml;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Grava SECAS_MESTRE.xlsx (formato longo: uma linha por município e mês).
    public class SecasMestreDataProcessor
    {
        public const string NomeArquivo = "SECAS_MESTRE.xlsx";

        private const int ColAno = 4;
        private const int ColMes = 5;

        private static readonly string[] Cabecalho =
        [
            "Município", "Código IBGE", "Regional", "Ano", "Mês", "Data_Referencia",
            "Categoria_Maxima", "Categoria_Predominante",
            "Pct_SemSeca", "Pct_S0", "Pct_S1", "Pct_S2", "Pct_S3", "Pct_S4"
        ];

        // Meses (ano, mês) já gravados na planilha.
        public HashSet<(int Year, int Month)> ReadRecordedMonths(string masterPath)
        {
            var set = new HashSet<(int, int)>();
            if (!File.Exists(masterPath)) return set;

            ExcelLicense.Ensure();
            using var pkg = new ExcelPackage(new FileInfo(masterPath));
            if (pkg.Workbook.Worksheets.Count == 0) return set;

            var ws = pkg.Workbook.Worksheets[0];
            int lastRow = ws.Dimension?.End.Row ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                int year = GetInt(ws, r, ColAno);
                int month = GetInt(ws, r, ColMes);
                if (year > 0 && month > 0) set.Add((year, month));
            }
            return set;
        }

        public async Task AppendMonthAsync(
            string masterPath, int year, int month, IReadOnlyList<SecasResultadoMunicipio> resultados)
        {
            ExcelLicense.Ensure();

            var fileInfo = new FileInfo(masterPath);
            fileInfo.Directory?.Create();

            using var pkg = fileInfo.Exists ? new ExcelPackage(fileInfo) : new ExcelPackage();

            ExcelWorksheet ws;
            if (pkg.Workbook.Worksheets.Count == 0)
            {
                ws = pkg.Workbook.Worksheets.Add("Dados");
                for (int c = 0; c < Cabecalho.Length; c++)
                    ws.Cells[1, c + 1].Value = Cabecalho[c];
            }
            else
            {
                ws = pkg.Workbook.Worksheets[0];
            }

            int lastRow = ws.Dimension?.End.Row ?? 1;

            // Idempotência: mês já gravado não é duplicado.
            for (int r = 2; r <= lastRow; r++)
            {
                if (GetInt(ws, r, ColAno) == year && GetInt(ws, r, ColMes) == month)
                    return;
            }

            var referencia = new DateTime(year, month, 1);
            int row = lastRow + 1;

            foreach (var res in resultados.OrderBy(r => r.Municipio.Nome))
            {
                var m = res.Municipio;
                string regional = RondoniaRegionais.GetRegion(StripAccents(m.Nome.Trim().ToUpperInvariant()));

                ws.Cells[row, 1].Value = m.Nome;
                ws.Cells[row, 2].Value = m.CodigoIbge;
                ws.Cells[row, 3].Value = regional;
                ws.Cells[row, ColAno].Value = year;
                ws.Cells[row, ColMes].Value = month;
                ws.Cells[row, 6].Value = referencia;
                ws.Cells[row, 6].Style.Numberformat.Format = "dd/MM/yyyy";
                ws.Cells[row, 7].Value = res.CategoriaMaxima is { } max ? SecasCategorias.Rotulo(max) : "Sem dados";
                ws.Cells[row, 8].Value = res.CategoriaPredominante is { } pred ? SecasCategorias.Rotulo(pred) : "Sem dados";

                int col = 9;
                foreach (var c in SecasCategorias.Todas)
                {
                    ws.Cells[row, col].Value = Math.Round(res.Percentuais[c], 2);
                    ws.Cells[row, col].Style.Numberformat.Format = "0.00";
                    col++;
                }
                row++;
            }

            await pkg.SaveAsAsync(fileInfo);
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

using System.Text;
using System.Text.Json;

namespace Conversor_de_Arquivos
{
    // Resposta bruta do BDQueimadas de cada semana epidemiológica, um arquivo por semana:
    //   {folder}\{year}\{year}_SE{week:D2}.json
    // (folder = pasta da sessão de coleta, ver VigiarFocosCalorService).
    // Uma semana só conta como baixada quando o arquivo existe e o conteúdo é lido
    // por completo (não vazio, não truncado, estrutura esperada).
    public class FocosCalorRawWeekStore
    {
        private readonly string _root;

        public FocosCalorRawWeekStore(string folder) => _root = folder;

        public string GetPath(int year, int week)
            => Path.Combine(_root, year.ToString(), $"{year}_SE{week:D2}.json");

        public bool TryRead(int year, int week, out List<CityHotspot> hotspots)
        {
            hotspots = [];
            string path = GetPath(year, week);
            if (!File.Exists(path)) return false;

            try
            {
                hotspots = ParseHotspots(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch
            {
                return false; // arquivo corrompido/parcial: a semana continua pendente e é baixada de novo
            }
        }

        // Valida primeiro, depois grava num arquivo temporário e o renomeia, para que uma queda
        // no meio da gravação nunca deixe um arquivo que pareça completo.
        public void Save(int year, int week, string json)
        {
            ParseHotspots(json);

            string path = GetPath(year, week);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, path, overwrite: true);
        }

        // Conteúdo esperado: { "firesCount": { "rows": [ { "municipio", "estado", "count" }, ... ] } }.
        // Lança InvalidDataException para qualquer outra coisa (vazio, truncado, página de erro, outro formato).
        public static List<CityHotspot> ParseHotspots(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("resposta vazia do BDQueimadas");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"resposta incompleta ou inválida do BDQueimadas ({ex.Message})");
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("firesCount", out var firesCount) ||
                    firesCount.ValueKind != JsonValueKind.Object ||
                    !firesCount.TryGetProperty("rows", out var rows) ||
                    rows.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("estrutura inesperada na resposta do BDQueimadas (firesCount.rows ausente)");
                }

                var hotspots = new List<CityHotspot>();
                foreach (var row in rows.EnumerateArray())
                {
                    string municipality = row.GetProperty("municipio").GetString() ?? string.Empty;
                    string state        = row.GetProperty("estado").GetString()    ?? string.Empty;
                    var countElement    = row.GetProperty("count");
                    int count = countElement.ValueKind == JsonValueKind.Number
                        ? countElement.GetInt32()
                        : int.TryParse(countElement.GetString(), out int c) ? c : 0;
                    hotspots.Add(new CityHotspot(municipality, state, count));
                }
                return hotspots;
            }
        }
    }
}

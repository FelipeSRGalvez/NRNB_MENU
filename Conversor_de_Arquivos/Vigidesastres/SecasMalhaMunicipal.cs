using Conversor_de_Arquivos.Geo;
using NetTopologySuite.Geometries;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Malha municipal de Rondônia (TopoJSON ou GeoJSON informado na configuração).
    // Sem CRS declarado: coordenadas tratadas como lon/lat SIRGAS 2000 / WGS 84.
    public sealed class SecasMalhaMunicipal
    {
        public const string CampoCodigo = "CD_MUN";
        public const string CampoNome = "NM_MUN";
        public const string PrefixoCodigoRondonia = "11";

        public IReadOnlyList<SecasMunicipio> Municipios { get; }

        // Extensão em lon/lat, usada para recortar as camadas nacionais antes de projetar.
        public Envelope EnvelopeGeografico { get; }

        private SecasMalhaMunicipal(List<SecasMunicipio> municipios, Envelope envelope)
        {
            Municipios = municipios;
            EnvelopeGeografico = envelope;
        }

        public static SecasMalhaMunicipal Carregar(string caminho, GeoReprojector projecao)
        {
            if (!File.Exists(caminho))
                throw new FileNotFoundException($"Arquivo da malha municipal não encontrado: {caminho}");

            var features = VectorFileReader.Read(caminho);
            if (features.Count == 0)
                throw new InvalidDataException("A malha municipal não contém feições.");

            var municipios = new List<SecasMunicipio>();
            var envelope = new Envelope();

            foreach (var f in features)
            {
                string? codigo = f.GetString(CampoCodigo);
                string? nome = f.GetString(CampoNome);
                if (codigo == null || nome == null)
                    throw new InvalidDataException(
                        $"A malha municipal precisa dos campos {CampoCodigo} e {CampoNome}.");

                if (codigo.Length != 7 || !codigo.StartsWith(PrefixoCodigoRondonia))
                    continue;

                var geografica = GeometryTools.MakeValidPolygonal(f.Geometry);
                if (geografica.IsEmpty) continue;
                envelope.ExpandToInclude(geografica.EnvelopeInternal);

                var projetada = GeometryTools.MakeValidPolygonal(projecao.Project(geografica));
                municipios.Add(new SecasMunicipio(codigo, nome, projetada, projetada.Area));
            }

            if (municipios.Count == 0)
                throw new InvalidDataException(
                    $"Nenhum município de Rondônia ({CampoCodigo} iniciando em {PrefixoCodigoRondonia}) na malha.");

            var duplicados = municipios.GroupBy(m => m.CodigoIbge).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicados.Count > 0)
                throw new InvalidDataException($"Códigos IBGE duplicados na malha: {string.Join(", ", duplicados)}");

            return new SecasMalhaMunicipal(municipios.OrderBy(m => m.Nome).ToList(), envelope);
        }
    }
}

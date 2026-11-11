using Conversor_de_Arquivos.Geo;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Cruza a camada de seca de um mês com a malha municipal e calcula, por município,
    // o % da área em cada categoria, a categoria máxima e a predominante.
    public sealed class SecasMunicipalAnalyzer
    {
        // Categoria_Maxima = a categoria mais grave cuja interseção ocupa pelo menos este % da
        // área do município. Evita que lascas de borda (fronteira de polígono passando rente ao
        // limite municipal) definam a categoria máxima.
        public const double LimiarCategoriaMaximaPct = 1.0;

        public const string CampoCategoria = "uf_codigo";

        // Margem (graus) do recorte da camada nacional em torno da malha, antes de projetar.
        private const double MargemRecorteGraus = 0.1;

        private readonly GeoReprojector _projecao;

        public SecasMunicipalAnalyzer(GeoReprojector projecao) => _projecao = projecao;

        public List<SecasResultadoMunicipio> Analisar(SecasCamadaMes camada, SecasMalhaMunicipal malha)
        {
            var categorias = CarregarCategorias(camada.Arquivo, malha.EnvelopeGeografico);
            var resultados = new List<SecasResultadoMunicipio>(malha.Municipios.Count);

            foreach (var m in malha.Municipios)
            {
                var bruto = new Dictionary<SecasCategoria, double>();
                foreach (var c in SecasCategorias.Todas)
                {
                    bruto[c] = categorias.TryGetValue(c, out var geom)
                        ? GeometryTools.SafeIntersection(m.GeometriaProjetada, geom).Area / m.Area * 100.0
                        : 0.0;
                }

                var pct = camada.Tipo == SecasTipoCamada.Cumulativa ? Desacumular(bruto) : bruto;
                resultados.Add(Montar(m, pct));
            }

            return resultados;
        }

        // Camada cumulativa: a área da categoria k inclui todas as mais graves.
        // % isolado(k) = % cumulativo(k) − % cumulativo(k+1).
        private static Dictionary<SecasCategoria, double> Desacumular(Dictionary<SecasCategoria, double> cumulativo)
        {
            var resultado = new Dictionary<SecasCategoria, double>();
            var ordem = SecasCategorias.Todas;
            for (int i = 0; i < ordem.Length; i++)
            {
                double proximo = i + 1 < ordem.Length ? cumulativo[ordem[i + 1]] : 0.0;
                resultado[ordem[i]] = Math.Max(0.0, cumulativo[ordem[i]] - proximo);
            }
            return resultado;
        }

        private static SecasResultadoMunicipio Montar(SecasMunicipio m, Dictionary<SecasCategoria, double> pct)
        {
            SecasCategoria? maxima = SecasCategorias.Todas.Reverse()
                .Cast<SecasCategoria?>()
                .FirstOrDefault(c => pct[c!.Value] >= LimiarCategoriaMaximaPct);

            var maior = pct.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First();
            SecasCategoria? predominante = maior.Value > 0 ? maior.Key : null;

            return new SecasResultadoMunicipio
            {
                Municipio = m,
                Percentuais = pct,
                CategoriaMaxima = maxima,
                CategoriaPredominante = predominante
            };
        }

        // Lê a camada, corrige geometrias, recorta para a região da malha, projeta e une por categoria.
        private Dictionary<SecasCategoria, Geometry> CarregarCategorias(string shp, Envelope envelopeMalha)
        {
            var features = ShapefileFeatureReader.Read(shp);
            if (features.Count == 0)
                throw new InvalidDataException($"Camada sem feições: {Path.GetFileName(shp)}");
            if (!features[0].Properties.ContainsKey(CampoCategoria))
                throw new InvalidDataException($"Campo '{CampoCategoria}' ausente em {Path.GetFileName(shp)}.");

            var envelope = new Envelope(envelopeMalha);
            envelope.ExpandBy(MargemRecorteGraus);

            var grupos = new Dictionary<SecasCategoria, List<Geometry>>();
            foreach (var f in features)
            {
                string? codigo = f.GetString(CampoCategoria);
                if (!SecasCategorias.TryParseCodigo(codigo, out var categoria))
                    throw new InvalidDataException($"Categoria desconhecida '{codigo}' em {Path.GetFileName(shp)}.");

                var valida = GeometryTools.MakeValidPolygonal(f.Geometry);
                var recorte = GeometryTools.SafeIntersection(valida, valida.Factory.ToGeometry(envelope));
                if (recorte.IsEmpty) continue;

                var projetada = GeometryTools.MakeValidPolygonal(_projecao.Project(recorte));
                if (!grupos.TryGetValue(categoria, out var lista))
                    grupos[categoria] = lista = [];
                lista.Add(projetada);
            }

            return grupos.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Count == 1
                    ? kv.Value[0]
                    : GeometryTools.MakeValidPolygonal(CascadedPolygonUnion.Union(kv.Value)));
        }
    }
}

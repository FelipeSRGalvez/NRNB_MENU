using System.Text.RegularExpressions;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Localiza a camada de seca dentro da pasta extraída de um mês.
    // Os nomes variam (ex.: setembro26_isolado.shp, setembro2024/setembro24.shp, abril25.shp);
    // as pastas/arquivos de apoio (Estados_monitor, UF_Brasil, *_MS_IMPACTOS*) são ignorados.
    public static class SecasCamadaLocator
    {
        // Camada principal: {mes}{aa}.shp ou {mes}{aaaa}.shp
        private static readonly Regex PrincipalRegex =
            new(@"^[a-z]+\d{2}(\d{2})?\.shp$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] Ignorados = ["estados_monitor", "uf_brasil", "_ms_impactos"];

        public static SecasCamadaMes Localizar(string pastaMes)
        {
            var shps = Directory.EnumerateFiles(pastaMes, "*.shp", SearchOption.AllDirectories)
                .Where(p => !Ignorar(pastaMes, p))
                .ToList();

            // Preferência: camada isolada (polígonos disjuntos, existe a partir de mai/2025).
            var isoladas = shps
                .Where(p => Path.GetFileNameWithoutExtension(p).EndsWith("_isolado", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (isoladas.Count == 1)
                return new SecasCamadaMes(isoladas[0], SecasTipoCamada.Isolada);
            if (isoladas.Count > 1)
                throw new InvalidDataException($"Mais de uma camada _isolado encontrada: {Nomes(pastaMes, isoladas)}");

            // Meses anteriores: só existe a camada principal, cumulativa.
            var principais = shps.Where(p => PrincipalRegex.IsMatch(Path.GetFileName(p))).ToList();
            if (principais.Count == 1)
                return new SecasCamadaMes(principais[0], SecasTipoCamada.Cumulativa);

            throw new InvalidDataException(principais.Count == 0
                ? $"Camada de seca não encontrada. Shapefiles na pasta: {Nomes(pastaMes, shps)}"
                : $"Mais de uma camada principal encontrada: {Nomes(pastaMes, principais)}");
        }

        private static bool Ignorar(string raiz, string caminho)
        {
            string relativo = Path.GetRelativePath(raiz, caminho);
            return Ignorados.Any(i => relativo.Contains(i, StringComparison.OrdinalIgnoreCase));
        }

        private static string Nomes(string raiz, IEnumerable<string> caminhos)
        {
            var lista = caminhos.Select(c => Path.GetRelativePath(raiz, c)).ToList();
            return lista.Count == 0 ? "(nenhum)" : string.Join(", ", lista);
        }
    }
}

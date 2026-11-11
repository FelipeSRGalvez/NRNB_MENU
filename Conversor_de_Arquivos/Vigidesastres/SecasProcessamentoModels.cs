using NetTopologySuite.Geometries;

namespace Conversor_de_Arquivos.Vigidesastres
{
    // Categorias do Monitor de Secas (campo uf_codigo), da menos para a mais grave.
    public enum SecasCategoria
    {
        SemSeca = 0, // si
        S0 = 1,
        S1 = 2,
        S2 = 3,
        S3 = 4,
        S4 = 5
    }

    public static class SecasCategorias
    {
        public static readonly SecasCategoria[] Todas =
        [
            SecasCategoria.SemSeca, SecasCategoria.S0, SecasCategoria.S1,
            SecasCategoria.S2, SecasCategoria.S3, SecasCategoria.S4
        ];

        // Texto gravado na planilha mestre.
        public static string Rotulo(SecasCategoria c) => c switch
        {
            SecasCategoria.SemSeca => "Sem seca",
            SecasCategoria.S0      => "Fraca",
            SecasCategoria.S1      => "Moderada",
            SecasCategoria.S2      => "Grave",
            SecasCategoria.S3      => "Extrema",
            SecasCategoria.S4      => "Excepcional",
            _ => c.ToString()
        };

        public static bool TryParseCodigo(string? codigo, out SecasCategoria categoria)
        {
            switch (codigo?.Trim().ToLowerInvariant())
            {
                case "si": categoria = SecasCategoria.SemSeca; return true;
                case "s0": categoria = SecasCategoria.S0; return true;
                case "s1": categoria = SecasCategoria.S1; return true;
                case "s2": categoria = SecasCategoria.S2; return true;
                case "s3": categoria = SecasCategoria.S3; return true;
                case "s4": categoria = SecasCategoria.S4; return true;
                default:   categoria = default; return false;
            }
        }
    }

    // Isolada: polígonos disjuntos, um por categoria ({mes}{aa}_isolado.shp, a partir de mai/2025).
    // Cumulativa: polígonos aninhados (si ⊇ s0 ⊇ s1 ⊇ ...), camada principal {mes}{aa}.shp.
    public enum SecasTipoCamada
    {
        Isolada,
        Cumulativa
    }

    public sealed record SecasCamadaMes(string Arquivo, SecasTipoCamada Tipo);

    // Município com geometria já corrigida e projetada (metros).
    public sealed record SecasMunicipio(string CodigoIbge, string Nome, Geometry GeometriaProjetada, double Area);

    public sealed class SecasResultadoMunicipio
    {
        public required SecasMunicipio Municipio { get; init; }

        // % da área do município em cada categoria (0–100), já desacumulado.
        public required IReadOnlyDictionary<SecasCategoria, double> Percentuais { get; init; }

        public SecasCategoria? CategoriaMaxima { get; init; }
        public SecasCategoria? CategoriaPredominante { get; init; }
    }

    public enum SecasProcessStatus
    {
        Processed,
        AlreadyInMaster,
        NoCoverage,
        NotDownloaded,
        Error
    }

    public record SecasProcessMonthResult(SecasMonth Month, SecasProcessStatus Status, string? Detail = null);

    public class SecasProcessSummary
    {
        public List<SecasProcessMonthResult> Results { get; } = [];

        public int Processed       => Results.Count(r => r.Status == SecasProcessStatus.Processed);
        public int AlreadyInMaster => Results.Count(r => r.Status == SecasProcessStatus.AlreadyInMaster);
        public int NoCoverage      => Results.Count(r => r.Status == SecasProcessStatus.NoCoverage);
        public int NotDownloaded   => Results.Count(r => r.Status == SecasProcessStatus.NotDownloaded);
        public int Errors          => Results.Count(r => r.Status == SecasProcessStatus.Error);
    }
}

using NetTopologySuite.Geometries;

namespace Conversor_de_Arquivos.Geo
{
    // Feição genérica: geometria NTS + atributos (chaves sem diferenciar maiúsculas/minúsculas).
    public sealed class GeoFeature
    {
        public Geometry Geometry { get; }
        public IReadOnlyDictionary<string, object?> Properties { get; }

        public GeoFeature(Geometry geometry, IDictionary<string, object?> properties)
        {
            Geometry = geometry;
            Properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        }

        public string? GetString(string key)
            => Properties.TryGetValue(key, out var v) ? v?.ToString()?.Trim() : null;
    }
}

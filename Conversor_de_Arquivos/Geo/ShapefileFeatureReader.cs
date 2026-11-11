using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;

namespace Conversor_de_Arquivos.Geo
{
    // Lê um shapefile (.shp + .dbf) para feições genéricas.
    public static class ShapefileFeatureReader
    {
        // Por padrão, polígonos com anéis malformados são lidos como vieram (IgnoreInvalidShapes),
        // em vez de abortar a leitura (Strict) ou descartar a feição (SkipInvalidShapes).
        // Quem consome deve corrigir a geometria antes de operar (ver GeometryTools.MakeValidPolygonal).
        public static List<GeoFeature> Read(
            string shpPath, GeometryBuilderMode mode = GeometryBuilderMode.IgnoreInvalidShapes)
        {
            var options = new ShapefileReaderOptions { GeometryBuilderMode = mode };

            var list = new List<GeoFeature>();
            foreach (var f in Shapefile.ReadAllFeatures(shpPath, options))
            {
                if (f.Geometry == null || f.Geometry.IsEmpty) continue;

                var props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (string name in f.Attributes.GetNames())
                    props[name] = f.Attributes[name];

                list.Add(new GeoFeature(f.Geometry, props));
            }
            return list;
        }
    }
}

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;

namespace Conversor_de_Arquivos.Geo
{
    // Operações geométricas utilitárias: correção, extração de partes poligonais e interseção segura.
    public static class GeometryTools
    {
        // Corrige geometria inválida (GeometryFixer = MakeValid do NTS) e mantém só as partes poligonais.
        public static Geometry MakeValidPolygonal(Geometry g)
        {
            var fixedGeom = g.IsValid ? g : GeometryFixer.Fix(g);
            return ExtractPolygonal(fixedGeom);
        }

        // Mantém apenas polígonos (descarta pontos/linhas que podem sobrar de correções e interseções).
        public static Geometry ExtractPolygonal(Geometry g)
        {
            if (g is Polygon or MultiPolygon) return g;

            var polys = new List<Polygon>();
            for (int i = 0; i < g.NumGeometries; i++)
            {
                var part = g.GetGeometryN(i);
                if (part is Polygon p && !p.IsEmpty) polys.Add(p);
                else if (part is MultiPolygon mp)
                    for (int j = 0; j < mp.NumGeometries; j++) polys.Add((Polygon)mp.GetGeometryN(j));
                else if (part is GeometryCollection gc && part != g)
                {
                    var inner = ExtractPolygonal(gc);
                    for (int j = 0; j < inner.NumGeometries; j++)
                        if (inner.GetGeometryN(j) is Polygon ip && !ip.IsEmpty) polys.Add(ip);
                }
            }
            return g.Factory.CreateMultiPolygon(polys.ToArray());
        }

        // Interseção poligonal com OverlayNG robusto (snapping automático).
        // Se ainda assim falhar por topologia, corrige as entradas com Buffer(0) e tenta de novo.
        public static Geometry SafeIntersection(Geometry a, Geometry b)
        {
            if (!a.EnvelopeInternal.Intersects(b.EnvelopeInternal))
                return a.Factory.CreateMultiPolygon();
            try
            {
                return ExtractPolygonal(OverlayNGRobust.Overlay(a, b, SpatialFunction.Intersection));
            }
            catch (TopologyException)
            {
                var fa = ExtractPolygonal(MakeValidPolygonal(a).Buffer(0));
                var fb = ExtractPolygonal(MakeValidPolygonal(b).Buffer(0));
                return ExtractPolygonal(OverlayNGRobust.Overlay(fa, fb, SpatialFunction.Intersection));
            }
        }

        // Retângulo (lon/lat) para recorte rápido.
        public static Polygon EnvelopeToPolygon(Envelope env, GeometryFactory factory)
            => (Polygon)factory.ToGeometry(env);
    }
}

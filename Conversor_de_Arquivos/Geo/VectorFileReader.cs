using System.Text.Json;
using NetTopologySuite.Geometries;

namespace Conversor_de_Arquivos.Geo
{
    // Lê GeoJSON ou TopoJSON (detectado pelo conteúdo: "type": "Topology") para feições NTS.
    // Coordenadas são devolvidas como vieram (lon/lat); não há reprojeção aqui.
    public static class VectorFileReader
    {
        private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

        public static List<GeoFeature> Read(string path, string? topoJsonObjectName = null)
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

            return type.Equals("Topology", StringComparison.OrdinalIgnoreCase)
                ? ReadTopoJson(root, topoJsonObjectName)
                : ReadGeoJson(root);
        }

        public static bool IsTopoJson(string path)
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.TryGetProperty("type", out var t)
                   && string.Equals(t.GetString(), "Topology", StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ GeoJSON

        private static List<GeoFeature> ReadGeoJson(JsonElement root)
        {
            var list = new List<GeoFeature>();
            string type = root.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "FeatureCollection":
                    foreach (var f in root.GetProperty("features").EnumerateArray())
                        AddGeoJsonFeature(f, list);
                    break;
                case "Feature":
                    AddGeoJsonFeature(root, list);
                    break;
                default:
                    list.Add(new GeoFeature(ParseGeoJsonGeometry(root), new Dictionary<string, object?>()));
                    break;
            }
            return list;
        }

        private static void AddGeoJsonFeature(JsonElement f, List<GeoFeature> list)
        {
            if (!f.TryGetProperty("geometry", out var g) || g.ValueKind == JsonValueKind.Null)
                return;
            list.Add(new GeoFeature(ParseGeoJsonGeometry(g), ReadProperties(f)));
        }

        private static Geometry ParseGeoJsonGeometry(JsonElement g)
        {
            string type = g.GetProperty("type").GetString() ?? "";
            if (type == "GeometryCollection")
                return Factory.CreateGeometryCollection(
                    g.GetProperty("geometries").EnumerateArray().Select(ParseGeoJsonGeometry).ToArray());

            var c = g.GetProperty("coordinates");
            return type switch
            {
                "Point"           => Factory.CreatePoint(ToCoord(c)),
                "MultiPoint"      => Factory.CreateMultiPointFromCoords(c.EnumerateArray().Select(ToCoord).ToArray()),
                "LineString"      => Factory.CreateLineString(ToCoords(c)),
                "MultiLineString" => Factory.CreateMultiLineString(c.EnumerateArray().Select(l => Factory.CreateLineString(ToCoords(l))).ToArray()),
                "Polygon"         => BuildPolygon(c.EnumerateArray().Select(ToCoords).ToList()),
                "MultiPolygon"    => Factory.CreateMultiPolygon(c.EnumerateArray()
                                        .Select(p => BuildPolygon(p.EnumerateArray().Select(ToCoords).ToList())).ToArray()),
                _ => throw new InvalidDataException($"Tipo de geometria GeoJSON não suportado: {type}")
            };
        }

        private static Coordinate ToCoord(JsonElement p) => new(p[0].GetDouble(), p[1].GetDouble());
        private static Coordinate[] ToCoords(JsonElement a) => a.EnumerateArray().Select(ToCoord).ToArray();

        // ------------------------------------------------------------------ TopoJSON

        private static List<GeoFeature> ReadTopoJson(JsonElement root, string? objectName)
        {
            double sx = 1, sy = 1, tx = 0, ty = 0;
            bool quantized = false;
            if (root.TryGetProperty("transform", out var tr))
            {
                quantized = true;
                sx = tr.GetProperty("scale")[0].GetDouble();
                sy = tr.GetProperty("scale")[1].GetDouble();
                tx = tr.GetProperty("translate")[0].GetDouble();
                ty = tr.GetProperty("translate")[1].GetDouble();
            }

            // Arcos decodificados (delta + transform quando quantizado).
            var arcs = new List<Coordinate[]>();
            foreach (var arc in root.GetProperty("arcs").EnumerateArray())
            {
                var pts = new List<Coordinate>();
                double x = 0, y = 0;
                foreach (var p in arc.EnumerateArray())
                {
                    if (quantized)
                    {
                        x += p[0].GetDouble();
                        y += p[1].GetDouble();
                        pts.Add(new Coordinate(x * sx + tx, y * sy + ty));
                    }
                    else
                    {
                        pts.Add(new Coordinate(p[0].GetDouble(), p[1].GetDouble()));
                    }
                }
                arcs.Add(pts.ToArray());
            }

            Coordinate Pos(JsonElement p) => quantized
                ? new Coordinate(p[0].GetDouble() * sx + tx, p[1].GetDouble() * sy + ty)
                : new Coordinate(p[0].GetDouble(), p[1].GetDouble());

            var objects = root.GetProperty("objects");
            IEnumerable<JsonElement> selected;
            if (objectName != null)
            {
                if (!objects.TryGetProperty(objectName, out var o))
                    throw new InvalidDataException($"Objeto '{objectName}' não encontrado no TopoJSON.");
                selected = [o];
            }
            else
            {
                selected = objects.EnumerateObject().Select(p => p.Value).ToList();
            }

            var list = new List<GeoFeature>();
            foreach (var obj in selected)
                CollectTopoObject(obj, arcs, Pos, list);
            return list;
        }

        private static void CollectTopoObject(
            JsonElement obj, List<Coordinate[]> arcs, Func<JsonElement, Coordinate> pos, List<GeoFeature> list)
        {
            string type = obj.GetProperty("type").GetString() ?? "";
            if (type == "GeometryCollection")
            {
                foreach (var g in obj.GetProperty("geometries").EnumerateArray())
                    CollectTopoObject(g, arcs, pos, list);
                return;
            }

            Geometry? geom = type switch
            {
                "Point"           => Factory.CreatePoint(pos(obj.GetProperty("coordinates"))),
                "MultiPoint"      => Factory.CreateMultiPointFromCoords(obj.GetProperty("coordinates").EnumerateArray().Select(pos).ToArray()),
                "LineString"      => Factory.CreateLineString(StitchArcs(obj.GetProperty("arcs"), arcs)),
                "MultiLineString" => Factory.CreateMultiLineString(obj.GetProperty("arcs").EnumerateArray()
                                        .Select(l => Factory.CreateLineString(StitchArcs(l, arcs))).ToArray()),
                "Polygon"         => BuildPolygon(obj.GetProperty("arcs").EnumerateArray().Select(r => StitchArcs(r, arcs)).ToList()),
                "MultiPolygon"    => Factory.CreateMultiPolygon(obj.GetProperty("arcs").EnumerateArray()
                                        .Select(p => BuildPolygon(p.EnumerateArray().Select(r => StitchArcs(r, arcs)).ToList())).ToArray()),
                null or ""        => null,
                _ => throw new InvalidDataException($"Tipo de geometria TopoJSON não suportado: {type}")
            };

            if (geom != null)
                list.Add(new GeoFeature(geom, ReadProperties(obj)));
        }

        // Junta os arcos de um anel/linha. Índice negativo (~i) = arco i invertido.
        // O primeiro ponto de cada arco seguinte repete o último do anterior e é descartado.
        private static Coordinate[] StitchArcs(JsonElement indexes, List<Coordinate[]> arcs)
        {
            var result = new List<Coordinate>();
            foreach (var idx in indexes.EnumerateArray())
            {
                int i = idx.GetInt32();
                IEnumerable<Coordinate> pts = i >= 0 ? arcs[i] : arcs[~i].Reverse();
                bool first = true;
                foreach (var c in pts)
                {
                    if (first && result.Count > 0) { first = false; continue; }
                    first = false;
                    result.Add(c.Copy());
                }
            }
            return result.ToArray();
        }

        // ------------------------------------------------------------------ comuns

        private static Polygon BuildPolygon(List<Coordinate[]> rings)
        {
            var linear = rings.Select(CloseRing).Where(r => r.Length >= 4)
                              .Select(r => Factory.CreateLinearRing(r)).ToList();
            if (linear.Count == 0) return Factory.CreatePolygon();
            return Factory.CreatePolygon(linear[0], linear.Skip(1).ToArray());
        }

        private static Coordinate[] CloseRing(Coordinate[] ring)
        {
            if (ring.Length == 0 || ring[0].Equals2D(ring[^1])) return ring;
            return [.. ring, ring[0].Copy()];
        }

        private static Dictionary<string, object?> ReadProperties(JsonElement element)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!element.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
                return dict;

            foreach (var p in props.EnumerateObject())
            {
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out long l) ? l : p.Value.GetDouble(),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Null   => null,
                    _                    => p.Value.GetRawText()
                };
            }
            return dict;
        }
    }
}

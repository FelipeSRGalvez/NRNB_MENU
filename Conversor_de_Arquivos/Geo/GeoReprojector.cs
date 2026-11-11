using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Conversor_de_Arquivos.Geo
{
    // Reprojeta geometrias em lon/lat (graus) para um sistema projetado definido em WKT.
    // A origem é o próprio datum geográfico do sistema de destino, evitando transformação de datum
    // (lon/lat em WGS 84 ou SIRGAS 2000 são tratados como equivalentes).
    public sealed class GeoReprojector
    {
        private readonly MathTransform _transform;

        public GeoReprojector(string projectedWkt)
        {
            var target = new CoordinateSystemFactory().CreateFromWkt(projectedWkt) as ProjectedCoordinateSystem
                ?? throw new ArgumentException("O WKT informado não é um sistema projetado.", nameof(projectedWkt));

            _transform = new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(target.GeographicCoordinateSystem, target)
                .MathTransform;
        }

        public Geometry Project(Geometry geographic)
        {
            var copy = geographic.Copy();
            copy.Apply(new TransformFilter(_transform));
            copy.GeometryChanged();
            return copy;
        }

        private sealed class TransformFilter(MathTransform transform) : ICoordinateSequenceFilter
        {
            public bool Done => false;
            public bool GeometryChanged => true;

            public void Filter(CoordinateSequence seq, int i)
            {
                var (x, y) = transform.Transform(seq.GetX(i), seq.GetY(i));
                seq.SetX(i, x);
                seq.SetY(i, y);
            }
        }
    }
}

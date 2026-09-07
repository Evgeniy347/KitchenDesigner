using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct EdgeLayout
    {
        public readonly bool IsValid;
        public readonly int ThicknessAxis;
        public readonly int LengthAxis;
        public readonly int WidthAxis;
        public readonly int LengthMM;
        public readonly int WidthMM;
        public readonly int ThicknessMM;

        public EdgeLayout(int thicknessAxis, int lengthAxis, int widthAxis,
            int lengthMM, int widthMM, int thicknessMM)
        {
            IsValid = true;
            ThicknessAxis = thicknessAxis;
            LengthAxis = lengthAxis;
            WidthAxis = widthAxis;
            LengthMM = lengthMM;
            WidthMM = widthMM;
            ThicknessMM = thicknessMM;
        }

        public int FaceIndex(EdgeSide side) => side switch
        {
            EdgeSide.L1 => WidthAxis * 2,
            EdgeSide.L2 => WidthAxis * 2 + 1,
            EdgeSide.W1 => LengthAxis * 2,
            _ => LengthAxis * 2 + 1,
        };

        public int SideLengthMM(EdgeSide side) =>
            side == EdgeSide.L1 || side == EdgeSide.L2 ? LengthMM : WidthMM;
    }

    public readonly struct EdgeCoverage
    {
        private readonly float _l1, _l2, _w1, _w2;

        public EdgeCoverage(float l1, float l2, float w1, float w2)
        {
            _l1 = l1; _l2 = l2; _w1 = w1; _w2 = w2;
        }

        public float Ratio(EdgeSide side) => side switch
        {
            EdgeSide.L1 => _l1,
            EdgeSide.L2 => _l2,
            EdgeSide.W1 => _w1,
            _ => _w2,
        };

        public bool IsCovered(EdgeSide side) => Ratio(side) >= 1f - EdgeBanding.CoverEpsilon;

        public bool HasEdge(EdgeSide side) => !IsCovered(side);

        public bool IsPartial(EdgeSide side)
        {
            float r = Ratio(side);
            return r > EdgeBanding.CoverEpsilon && r < 1f - EdgeBanding.CoverEpsilon;
        }
    }

    public sealed class SceneFaces
    {
        public readonly struct Sphere
        {
            public readonly Vector3 Center;
            public readonly float Radius;

            public Sphere(Vector3 center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            public bool Touches(in Sphere other, float slack)
            {
                float reach = Radius + other.Radius + slack;
                return (Center - other.Center).sqrMagnitude <= reach * reach;
            }
        }

        private readonly List<KitchenElement> _elements = new List<KitchenElement>();
        private readonly List<Face[]> _faces = new List<Face[]>();
        private readonly List<Sphere> _spheres = new List<Sphere>();

        public static SceneFaces Of(IReadOnlyList<KitchenElement>? all)
        {
            var scene = new SceneFaces();
            if (all == null) return scene;
            foreach (var element in all)
            {
                if (element == null) continue;
                var faces = element.GetFaces();
                scene._elements.Add(element);
                scene._faces.Add(faces);
                scene._spheres.Add(BoundingSphere(element, faces));
            }
            return scene;
        }

        public int Count => _elements.Count;
        public KitchenElement ElementAt(int i) => _elements[i];
        public Face[] FacesAt(int i) => _faces[i];
        public Sphere SphereAt(int i) => _spheres[i];

        public Sphere SphereOf(KitchenElement element)
        {
            for (int i = 0; i < _elements.Count; i++)
                if (_elements[i] == element) return _spheres[i];
            return BoundingSphere(element, element.GetFaces());
        }

        private static Sphere BoundingSphere(KitchenElement element, Face[] faces)
        {
            var center = element.transform.position;
            float radius = 0f;
            foreach (var f in faces)
            {
                float halfFaceDiagonal = 0.5f * Mathf.Sqrt(f.size.x * f.size.x + f.size.y * f.size.y);
                radius = Mathf.Max(radius, (f.center - center).magnitude + halfFaceDiagonal);
            }
            return new Sphere(center, radius);
        }
    }

    public static class EdgeBanding
    {
        public const float CoverEpsilon = 0.001f;

        public static bool IsSheet(Vector3Int dimsMM) => ThinAxis(dimsMM) >= 0;

        public static int ThinAxis(Vector3Int dimsMM)
        {
            int axis = -1;
            for (int i = 0; i < 3; i++)
            {
                if (dimsMM[i] >= AppConstants.EDGE_MAX_SIDE_MM) continue;
                if (axis >= 0) return -1;
                axis = i;
            }
            return axis;
        }

        public static EdgeLayout LayoutOf(Vector3Int dimsMM)
        {
            int thick = ThinAxis(dimsMM);
            if (thick < 0) return default;

            int a = (thick + 1) % 3;
            int b = (thick + 2) % 3;
            if (a > b) (a, b) = (b, a);

            int lengthAxis = dimsMM[a] >= dimsMM[b] ? a : b;
            int widthAxis = lengthAxis == a ? b : a;
            return new EdgeLayout(thick, lengthAxis, widthAxis,
                dimsMM[lengthAxis], dimsMM[widthAxis], dimsMM[thick]);
        }

        public static string FormatThickness(float thicknessMM) =>
            thicknessMM.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        private static bool IsTransparentToEdges(KitchenElement other) =>
            other is LightSourceElement || other is SinkElement || other is CooktopElement
            || other is OvenElement || other is DishwasherElement || other is PillarElement
            || other is FacadeElement || other is DrawerElement;

        public static EdgeCoverage Coverage(KitchenElement element, IReadOnlyList<KitchenElement> others)
            => Coverage(element, SceneFaces.Of(others));

        public static EdgeCoverage Coverage(KitchenElement element, SceneFaces scene)
        {
            if (element == null || scene == null) return default;
            var layout = LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return default;

            var faces = element.GetFaces();
            var ends = new[]
            {
                faces[layout.FaceIndex(EdgeSide.L1)],
                faces[layout.FaceIndex(EdgeSide.L2)],
                faces[layout.FaceIndex(EdgeSide.W1)],
                faces[layout.FaceIndex(EdgeSide.W2)],
            };

            var covers = new List<Rect>[4];
            for (int i = 0; i < 4; i++) covers[i] = new List<Rect>();

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            var sphere = scene.SphereOf(element);
            for (int k = 0; k < scene.Count; k++)
            {
                var other = scene.ElementAt(k);
                if (other == null || other == element) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                if (IsTransparentToEdges(other)) continue;
                if (!sphere.Touches(scene.SphereAt(k), contactDist)) continue;

                var otherFaces = scene.FacesAt(k);
                for (int i = 0; i < 4; i++)
                {
                    var face = ends[i];
                    Vector3 u = face.rightAxis, v = face.upAxis;
                    Rect target = FaceRect(face, u, v);

                    foreach (var of in otherFaces)
                    {
                        if (Vector3.Dot(face.normal, of.normal) > -Tolerance.ParallelDot) continue;
                        if (Mathf.Abs(Vector3.Dot(of.center - face.center, face.normal)) > contactDist) continue;

                        Rect r = FaceRect(of, u, v);
                        float xMin = Mathf.Max(target.xMin, r.xMin), xMax = Mathf.Min(target.xMax, r.xMax);
                        float yMin = Mathf.Max(target.yMin, r.yMin), yMax = Mathf.Min(target.yMax, r.yMax);
                        if (xMax <= xMin || yMax <= yMin) continue;
                        covers[i].Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
                    }
                }
            }

            return new EdgeCoverage(
                CoveredRatio(ends[0], covers[0]),
                CoveredRatio(ends[1], covers[1]),
                CoveredRatio(ends[2], covers[2]),
                CoveredRatio(ends[3], covers[3]));
        }

        public static KitchenElement? DominantCoverer(KitchenElement element,
            IReadOnlyList<KitchenElement> others, EdgeSide side, out float coveredArea)
        {
            coveredArea = 0f;
            if (element == null) return null;
            var layout = LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return null;

            var face = element.GetFaces()[layout.FaceIndex(side)];
            Vector3 u = face.rightAxis, v = face.upAxis;
            Rect target = FaceRect(face, u, v);
            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

            KitchenElement? best = null;
            foreach (var other in others)
            {
                if (other == null || other == element) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                if (IsTransparentToEdges(other)) continue;

                var rects = new List<Rect>();
                foreach (var of in other.GetFaces())
                {
                    if (Vector3.Dot(face.normal, of.normal) > -Tolerance.ParallelDot) continue;
                    if (Mathf.Abs(Vector3.Dot(of.center - face.center, face.normal)) > contactDist) continue;

                    Rect r = FaceRect(of, u, v);
                    float xMin = Mathf.Max(target.xMin, r.xMin), xMax = Mathf.Min(target.xMax, r.xMax);
                    float yMin = Mathf.Max(target.yMin, r.yMin), yMax = Mathf.Min(target.yMax, r.yMax);
                    if (xMax <= xMin || yMax <= yMin) continue;
                    rects.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
                }
                if (rects.Count == 0) continue;

                float area = UnionArea(rects);
                if (area <= coveredArea) continue;
                coveredArea = area;
                best = other;
            }
            return best;
        }

        private static float CoveredRatio(in Face face, List<Rect> covers)
        {
            float area = face.size.x * face.size.y;
            if (area <= 0f || covers.Count == 0) return 0f;
            return Mathf.Clamp01(UnionArea(covers) / area);
        }

        private static float UnionArea(List<Rect> rects)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            foreach (var r in rects)
            {
                if (!xs.Contains(r.xMin)) xs.Add(r.xMin);
                if (!xs.Contains(r.xMax)) xs.Add(r.xMax);
                if (!ys.Contains(r.yMin)) ys.Add(r.yMin);
                if (!ys.Contains(r.yMax)) ys.Add(r.yMax);
            }
            xs.Sort();
            ys.Sort();

            float total = 0f;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float cx = (xs[i] + xs[i + 1]) * 0.5f;
                float w = xs[i + 1] - xs[i];
                if (w <= 0f) continue;
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    float cy = (ys[j] + ys[j + 1]) * 0.5f;
                    float h = ys[j + 1] - ys[j];
                    if (h <= 0f) continue;
                    foreach (var r in rects)
                    {
                        if (cx < r.xMin || cx > r.xMax || cy < r.yMin || cy > r.yMax) continue;
                        total += w * h;
                        break;
                    }
                }
            }
            return total;
        }

        private static Rect FaceRect(in Face face, Vector3 u, Vector3 v)
        {
            float cu = Vector3.Dot(face.center, u);
            float cv = Vector3.Dot(face.center, v);
            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;
            return Rect.MinMaxRect(cu - halfU, cv - halfV, cu + halfU, cv + halfV);
        }
    }
}

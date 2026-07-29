using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Варочная поверхность: простой параллелепипед 550×550×65 мм, садится на
    /// столешницу сверху (выступ 5 мм, 60 мм вглубь) и прилипает к ней тем же
    /// магнитом, что и мойка. Вырез в столешнице не делает — лежит на пласти.
    /// Коллизия со столешницей подавлена (как у мойки), с боковинами и ящиками
    /// под столешницей — ошибка.
    /// </summary>
    public class CooktopElement : KitchenElement
    {
        public const int WIDTH_MM = 550;
        public const int DEPTH_MM = 550;
        public const int TOTAL_HEIGHT_MM = 65;
        public const int RIM_HEIGHT_MM = 5;          // выступ над столешницей
        public const int BODY_DEPTH_MM = 60;         // глубина тела под столешницей
        public const int MIN_EDGE_MM = 30;           // остаток столешницы за телом

        public const int SNAP_CATCH_MM = 100;
        public const int SNAP_RELEASE_MM = 60;

        [SerializeField] private string _attachedPartName = "";
        [SerializeField] private int _offsetXMM;
        [SerializeField] private int _offsetYMM;

        private readonly List<GameObject> _children = new List<GameObject>();
        private KitchenElement? _lastHost;
        private int _lastOffsetXMM = int.MinValue;
        private int _lastOffsetYMM = int.MinValue;

        private float _freeHeightMM;
        private Vector3 _appliedPos;
        private bool _hasAppliedPos;
        private Vector3 _lastHostPosition;

        public string AttachedPartName { get => _attachedPartName; set => _attachedPartName = value ?? ""; }
        public int OffsetXMM { get => _offsetXMM; set => _offsetXMM = value; }
        public int OffsetYMM { get => _offsetYMM; set => _offsetYMM = value; }
        public bool IsAttached => _lastHost != null;

        protected override Vector3 EffectiveScale => new Vector3(
            WIDTH_MM * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            DEPTH_MM * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        // Бортик приподнят над плоскостью врезки — сдвиг обязан ехать вместе с
        // примеряемой позицией, иначе снэп считает панель на полбортика ниже.
        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, RIM_HEIGHT_MM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        private void Start()
        {
            SnapToPart();
        }

        private int _lastPoseVersion;

        private bool HostMoved =>
            _lastHost != null &&
            (_lastHost.transform.position - _lastHostPosition).sqrMagnitude > Tolerance.EpsilonSqr;

        private void Update()
        {
            if (HostMoved)
            {
                _lastHostPosition = _lastHost!.transform.position;
                SnapToPart();
            }
        }

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            Data.DimensionsMM = new Vector3Int(WIDTH_MM, TOTAL_HEIGHT_MM, DEPTH_MM);
            UpdateCollider();
            EnsureChildren();
            RebuildGeometry();
        }

        public void SnapToPart()
        {
            var host = _lastHost != null ? _lastHost : FindAttachedPart();
            TrackDrift(host);

            if (host != null && !StillHolds(host)) { ReleaseFrom(host); host = null; }
            if (host == null) host = FindCatchingPart();
            if (host == null) return;

            if (host.PartName != _attachedPartName)
                _attachedPartName = host.PartName;

            AlignToPart(host);
        }

        public void AttachToPart(KitchenElement part)
        {
            if (part == null || !IsSuitableHost(part)) return;
            _attachedPartName = part.PartName;
            _freeHeightMM = 0f;
            AlignToPart(part);
        }

        internal void UnregisterFromPart()
        {
            _attachedPartName = "";
            _lastHost = null;
        }

        private void ReleaseFrom(KitchenElement host)
        {
            var (upAxis, upSign) = UpAxisOf(host);
            Vector3 up = host.transform.rotation * (AxisVector(upAxis) * upSign);
            float actualHeightMM = LocalPose(host).heightMM;
            transform.position += up * ((_freeHeightMM - actualHeightMM) * AppConstants.MM_TO_UNITS);
            _appliedPos = transform.position;

            UnregisterFromPart();
            _freeHeightMM = 0f;
            _lastOffsetXMM = int.MinValue;
            _lastOffsetYMM = int.MinValue;
        }

        private void TrackDrift(KitchenElement? host)
        {
            if (!_hasAppliedPos) { _appliedPos = transform.position; _hasAppliedPos = true; return; }
            Vector3 drift = transform.position - _appliedPos;
            _appliedPos = transform.position;
            if (host == null || drift.sqrMagnitude < Tolerance.EpsilonSqr) return;

            var pt = host.transform;
            Vector3 local = Quaternion.Inverse(pt.rotation) * drift;
            float toU = AppConstants.MM_TO_UNITS;
            var (up, sign) = UpAxisOf(host);
            var (a, b) = PlaneAxes(up);
            _offsetXMM += Mathf.RoundToInt(local[a] / toU);
            _offsetYMM += Mathf.RoundToInt(local[b] / toU);
            _freeHeightMM += local[up] / toU * sign;
        }

        private static Vector3 AxisVector(int axis) =>
            axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static (int axis, float sign) UpAxisOf(KitchenElement part)
        {
            var rot = part.transform.rotation;
            int best = 2;
            float bestDot = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float dot = Vector3.Dot(rot * AxisVector(axis), Vector3.up);
                if (Mathf.Abs(dot) > Mathf.Abs(bestDot)) { bestDot = dot; best = axis; }
            }
            return (best, bestDot < 0f ? -1f : 1f);
        }

        private static (int a, int b) PlaneAxes(int upAxis) => upAxis switch
        {
            2 => (0, 1),
            1 => (0, 2),
            _ => (2, 1),
        };

        private bool StillHolds(KitchenElement host) =>
            IsSuitableHost(host) &&
            _freeHeightMM >= -SNAP_RELEASE_MM && _freeHeightMM <= SNAP_CATCH_MM;

        public static bool IsSuitableHost(KitchenElement part)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var (up, _) = UpAxisOf(part);
            float upness = Mathf.Abs((part.transform.rotation * AxisVector(up)).y);
            if (upness < 0.9f) return false;
            var (a, b) = PlaneAxes(up);
            var dims = part.DimensionsMM;
            return dims[a] >= MinPartWidthMM && dims[b] >= MinPartDepthMM;
        }

        public static int MinPartWidthMM => WIDTH_MM + 2 * MIN_EDGE_MM;
        public static int MinPartDepthMM => DEPTH_MM + 2 * MIN_EDGE_MM;

        private static bool IsOverFootprint(KitchenElement part, int offX, int offY)
        {
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            return Mathf.Abs(offX) <= dims[a] * 0.5f && Mathf.Abs(offY) <= dims[b] * 0.5f;
        }

        private KitchenElement? FindCatchingPart()
        {
            KitchenElement? best = null;
            float bestHeight = float.MaxValue;
            int bestX = 0, bestY = 0;
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || !IsSuitableHost(el)) continue;

                var (offX, offY, heightMM) = LocalPose(el);
                if (heightMM > SNAP_CATCH_MM || heightMM < -SNAP_RELEASE_MM) continue;
                if (!IsOverFootprint(el, offX, offY)) continue;

                ClampOffsets(el, ref offX, ref offY);
                if (BodyBlocked(el, offX, offY)) continue;

                float h = Mathf.Abs(heightMM);
                if (h >= bestHeight) continue;
                bestHeight = h;
                best = el;
                bestX = offX;
                bestY = offY;
            }
            if (best == null) return null;

            _offsetXMM = bestX;
            _offsetYMM = bestY;
            _freeHeightMM = 0f;
            return best;
        }

        private (int offX, int offY, float heightMM) LocalPose(KitchenElement part)
        {
            var pt = part.transform;
            float toU = AppConstants.MM_TO_UNITS;
            Vector3 local = Quaternion.Inverse(pt.rotation) * (transform.position - pt.position);
            var (up, sign) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);
            float height = local[up] / toU * sign - part.DimensionsMM[up] * 0.5f;
            return (Mathf.RoundToInt(local[a] / toU), Mathf.RoundToInt(local[b] / toU), height);
        }

        private static void ClampOffsets(KitchenElement part, ref int offX, ref int offY)
        {
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            int maxX = (dims[a] - WIDTH_MM) / 2 - MIN_EDGE_MM;
            int maxY = (dims[b] - DEPTH_MM) / 2 - MIN_EDGE_MM;
            offX = Mathf.Clamp(offX, -maxX, maxX);
            offY = Mathf.Clamp(offY, -maxY, maxY);
        }

        public bool BodyBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        public string DescribeCatch(KitchenElement part)
        {
            if (!IsSuitableHost(part)) return "деталь не годится под варочную";
            var (offX, offY, height) = LocalPose(part);
            bool over = IsOverFootprint(part, offX, offY);
            int cx = offX, cy = offY;
            ClampOffsets(part, ref cx, ref cy);
            return $"height={height:F1}мм (полоса {-SNAP_RELEASE_MM}..{SNAP_CATCH_MM}) " +
                   $"over={over} off=({offX},{offY})→({cx},{cy}) " +
                   $"blocker={FirstBlocker(part, cx, cy) ?? "-"}";
        }

        private static bool IsObstacle(KitchenElement el)
        {
            if (el is FacadeElement || el is DoorElement || el is WindowElement) return false;
            if (el is DrawerElement || el is PanelElement) return false;
            if (el is SinkElement || el is CooktopElement || el is LightSourceElement || el is FloorElement) return false;
            return el.GetComponent<BasePlate>() == null;
        }

        public string? FirstBlocker(KitchenElement part, int offX, int offY)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var pt = part.transform;
            var dims = part.DimensionsMM;

            var (up, sign) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);
            float x0 = (offX - WIDTH_MM * 0.5f) * toU, x1 = (offX + WIDTH_MM * 0.5f) * toU;
            float y0 = (offY - DEPTH_MM * 0.5f) * toU, y1 = (offY + DEPTH_MM * 0.5f) * toU;
            float halfT = dims[up] * 0.5f * toU;
            float body = BODY_DEPTH_MM * toU;
            float z0 = sign > 0f ? halfT - body : -halfT;
            float z1 = sign > 0f ? halfT : -halfT + body;

            var inv = Quaternion.Inverse(pt.rotation);
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || el == part || !IsObstacle(el)) continue;

                var verts = el.GetVertices();
                if (verts.Length == 0) continue;
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var w in verts)
                {
                    Vector3 l = inv * (w - pt.position);
                    min = Vector3.Min(min, l);
                    max = Vector3.Max(max, l);
                }

                float eps = Tolerance.EpsilonUnits;
                if (max[a] <= x0 + eps || min[a] >= x1 - eps) continue;
                if (max[b] <= y0 + eps || min[b] >= y1 - eps) continue;
                if (max[up] <= z0 + eps || min[up] >= z1 - eps) continue;
                return el.PartName;
            }
            return null;
        }

        private void AlignToPart(KitchenElement part)
        {
            var pt = part.transform;
            var dims = part.DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;

            var (up, sign) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);

            Vector3 upLocal = AxisVector(up) * sign;
            Vector3 fwdLocal = Vector3.Cross(AxisVector(a), upLocal);
            Quaternion targetRot = Quaternion.LookRotation(pt.rotation * fwdLocal, pt.rotation * upLocal);

            int offX = _offsetXMM, offY = _offsetYMM;
            ClampOffsets(part, ref offX, ref offY);

            bool hasPrev = _lastHost == part && _lastOffsetXMM != int.MinValue;
            if (hasPrev && BodyBlocked(part, offX, offY))
            {
                if (!BodyBlocked(part, offX, _lastOffsetYMM))
                    offY = _lastOffsetYMM;
                else if (!BodyBlocked(part, _lastOffsetXMM, offY))
                    offX = _lastOffsetXMM;
                else
                {
                    offX = _lastOffsetXMM;
                    offY = _lastOffsetYMM;
                }
            }
            _offsetXMM = offX;
            _offsetYMM = offY;

            Vector3 localPos = AxisVector(a) * (_offsetXMM * toU)
                             + AxisVector(b) * (_offsetYMM * toU)
                             + upLocal * (dims[up] * 0.5f * toU);
            Vector3 targetPos = pt.position + pt.rotation * localPos;

            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > 0.05f)
                transform.SetPositionAndRotation(targetPos, targetRot);
            _appliedPos = transform.position;

            if (_lastHost != part || _lastOffsetXMM != _offsetXMM || _lastOffsetYMM != _offsetYMM)
            {
                _lastHost = part;
                _lastOffsetXMM = _offsetXMM;
                _lastOffsetYMM = _offsetYMM;
                _lastHostPosition = part.transform.position;
            }
        }

        private KitchenElement? FindAttachedPart()
        {
            if (string.IsNullOrEmpty(_attachedPartName)) return null;
            foreach (var el in PartRegistry.All)
                if (el != null && el != this && el.PartName == _attachedPartName)
                    return el;
            return null;
        }

        // ── Геометрия ───────────────────────────────────────────────────

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            box.size = new Vector3(WIDTH_MM * toU, TOTAL_HEIGHT_MM * toU, DEPTH_MM * toU);
            box.center = new Vector3(0f, (RIM_HEIGHT_MM - TOTAL_HEIGHT_MM) * 0.5f * toU, 0f);
        }

        private const int ChildCount = 1;

        private void EnsureChildren()
        {
            while (_children.Count < ChildCount)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = "Body";
                var col = child.GetComponent<BoxCollider>();
                if (col != null) Object.DestroyImmediate(col);
                child.transform.SetParent(transform, false);
                _children.Add(child);
            }
        }

        private void RebuildGeometry()
        {
            if (_children.Count < ChildCount) return;
            float toU = AppConstants.MM_TO_UNITS;

            float centerY = (RIM_HEIGHT_MM - TOTAL_HEIGHT_MM) * 0.5f * toU;
            _children[0].transform.localPosition = new Vector3(0f, centerY, 0f);
            _children[0].transform.localRotation = Quaternion.identity;
            _children[0].transform.localScale = new Vector3(WIDTH_MM * toU, TOTAL_HEIGHT_MM * toU, DEPTH_MM * toU);

            ApplyMaterials();
        }

        private static Material? _surfaceMat;

        private static Material SurfaceMaterial()
        {
            if (_surfaceMat == null)
            {
                var color = new Color(0.08f, 0.08f, 0.08f, 1f);
                _surfaceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _surfaceMat.SetColor("_BaseColor", color);
                _surfaceMat.color = color;
                _surfaceMat.SetFloat("_Metallic", 0.05f);
                _surfaceMat.SetFloat("_Smoothness", 0.92f);
            }
            return _surfaceMat!;
        }

        private void ApplyMaterials()
        {
            if (_children.Count < ChildCount) return;
            var mr = _children[0].GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = SurfaceMaterial();
        }

        public void DestroyChildren()
        {
            foreach (var child in _children)
            {
                if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
            _children.Clear();
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            UnregisterFromPart();
            DestroyChildren();
        }
    }
}

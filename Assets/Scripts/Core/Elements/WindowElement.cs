using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum GlassTint { Clear = 0, Tinted = 1 }

    /// <summary>
    /// Окно: неподвижная коробка (рама на всю толщину стены, подоконник, отлив,
    /// откосы) + поворотная створка (обвязка со стеклом). Окно живёт только на
    /// стене: каждый кадр прилипает к ближайшей стене, встаёт в её срединную
    /// плоскость и наследует её толщину (глубина окна не редактируется напрямую).
    /// Геометрия строится в мировых единицах при единичном масштабе корня —
    /// как у PillarElement/RadialShelfElement, иначе дети масштабируются дважды.
    /// </summary>
    public class WindowElement : KitchenElement, IOpenable
    {

        public override string DisplayTypeName => "Окно";
        private const float OpenSeconds = 0.4f;

        [SerializeField] private GlassTint _tint = GlassTint.Clear;
        [SerializeField] private int _sillProtrusionMM = AppConstants.WINDOW_SILL_DEFAULT_MM;
        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private string _attachedWallName = "";

        private static Shader? _cachedShader;
        private static Material? _tintedGlassMat;
        private static Material? _clearGlassMat;
        private static Material? _slopeMat;

        private readonly List<GameObject> _children = new List<GameObject>();
        private GameObject? _frameTop, _frameBottom, _frameLeft, _frameRight;
        private GameObject? _glassPane;
        private GameObject? _sillObj;
        private GameObject? _dripObj;
        private GameObject? _slopeTop, _slopeBottom, _slopeLeft, _slopeRight;
        private GameObject? _sashLeft, _sashRight, _sashTop, _sashBottom;
        private Transform? _staticGroup; // коробка/подоконник/отлив/откосы — не двигаются
        private Transform? _sashGroup;   // створка (обвязка + стекло) — поворачивается на петле

        private float _openT;
        private Vector3 _sashClosedLocal;
        private Vector3 _sashHalfExtents;
        // Позиция, для которой в последний раз перестраивался вырез в стене.
        private Vector3 _lastCutoutPos = new Vector3(float.NaN, 0f, 0f);

        [Undoable]
        public GlassTint Tint
        {
            get => _tint;
            set { _tint = value; ApplyTint(); }
        }

        [Undoable]
        public int SillProtrusionMM
        {
            get => _sillProtrusionMM;
            set { _sillProtrusionMM = Mathf.Clamp(value, 0, 200); ApplyDimensions(); }
        }

        [Undoable]
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; ApplyDoorPose(); }
        }

        public bool IsOpen => _isOpen;
        public float DoorProgress => _openT;

        [NotUndoable("служебная привязка к стене, вычисляется SnapToWall")]
        public string AttachedWallName { get => _attachedWallName; set => _attachedWallName = value ?? ""; }

        // Корень окна при открывании не двигается (поворачивается только створка),
        // поэтому закрытая поза всегда совпадает с текущей.
        public Vector3 ClosedPosition => transform.position;
        public Quaternion ClosedRotation => transform.rotation;
        public bool IsDoorClosed => !_isOpen && _openT <= 0f;

        private void Start()
        {
            SnapToWall();
        }

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            UpdateCollider();
            EnsureChildren();
            RebuildGeometry();

            // Вырез в стене зависит от габаритов окна.
            var wall = FindAttachedWall();
            if (wall != null) wall.RebuildMesh();
        }

        /// <summary>Перестроить геометрию без изменения размеров (вызов при изменении
        /// списка соседних окон на стене).</summary>
        public void RefreshGeometry()
        {
            RebuildGeometry();
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (!Mathf.Approximately(_openT, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ToggleOpen() => SetOpen(!_isOpen);

        public void ForceClose()
        {
            if (_openT <= 0f && !_isOpen) return;
            _isOpen = false;
            _openT = 0f;
            ApplyDoorPose();
        }

        private void ApplyDoorPose()
        {
            if (_sashGroup == null) return;
            FacadeDoor.Pose(_sashClosedLocal, Quaternion.identity, _sashHalfExtents,
                _mode, _openT, out var pos, out var rot, HingeKinematics.EdgePivot);
            _sashGroup.localPosition = pos;
            _sashGroup.localRotation = rot;
        }

        private int _lastPoseVersion;

        private void Update()
        {
            StepDoor(Time.deltaTime);
            if (PoseVersion != _lastPoseVersion)
            {
                _lastPoseVersion = PoseVersion;
                SnapToWall();
            }
        }

        public void StepDoor(float dt)
        {
            float target = _isOpen ? 1f : 0f;
            if (Mathf.Approximately(_openT, target)) return;
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _openT = Mathf.MoveTowards(_openT, target, step);

            if (_isOpen && _openT > 0f)
            {
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds);
                if (safe < _openT) _openT = Mathf.Max(_openT - step, safe);
            }

            ApplyDoorPose();
        }

        /// <summary>Мировые границы створки (AABB) при заданном прогрессе открывания [0..1].</summary>
        public (Vector3 min, Vector3 max) GetOpenBounds(float progress)
        {
            if (_sashGroup == null || _sashHalfExtents.sqrMagnitude < 1e-12f)
                return OpeningCollision.MinMax(GetVertices());

            FacadeDoor.Pose(_sashClosedLocal, Quaternion.identity, _sashHalfExtents,
                _mode, progress, out var localPos, out var localRot, HingeKinematics.EdgePivot);

            var worldPos = transform.TransformPoint(localPos);
            var worldRot = transform.rotation * localRot;

            var half = _sashHalfExtents;
            var localCorners = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z), new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z), new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z), new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z), new Vector3(-half.x,  half.y,  half.z),
            };
            var world = new Vector3[8];
            for (int i = 0; i < 8; i++)
                world[i] = worldPos + worldRot * localCorners[i];
            return OpeningCollision.MinMax(world);
        }

        // ── Привязка к стене ────────────────────────────────────────────

        /// <summary>Прилипание к ближайшей стене: регистрация, поворот вдоль стены,
        /// центрирование в срединной плоскости и наследование толщины стены.</summary>
        public void SnapToWall()
        {
            using var _ = PerfMarkers.WindowSnapToWall.Auto();
            var wall = RegisterWithNearestWall();
            if (wall != null) AlignToWall(wall);
        }

        public void AttachToWall(Wall wall)
        {
            if (wall == null) return;
            UnregisterFromWall();
            _attachedWallName = wall.gameObject.name;
            wall.RegisterWindow(this);
            _lastCutoutPos = transform.position;
            AlignToWall(wall);
        }

        private Wall? RegisterWithNearestWall()
        {
            var best = FindNearestWall();
            if (best == null) return null;
            // Проверяем фактическое членство, а не только имя: после загрузки
            // сцены имя уже восстановлено из сейва, но стена окно ещё не знает —
            // без регистрации вырез в стене не строится.
            if (best.gameObject.name != _attachedWallName || !best.HasWindow(this))
            {
                UnregisterFromWall();
                _attachedWallName = best.gameObject.name;
                best.RegisterWindow(this);
                _lastCutoutPos = transform.position;
            }
            return best;
        }

        // Гистерезис смены стены: на стыке двух стен расстояния почти равны и
        // дрожат (float, опускание стен камерой) — без запаса окно скачет.
        private const float WallSwitchHysteresisU = 0.05f; // 50 мм

        /// <summary>Ближайшая стена по расстоянию до её бокса (а не до центра —
        /// иначе у длинных стен выигрывает не та, на которой стоит окно).
        /// Текущая стена удерживается, пока другая не станет ближе на гистерезис.</summary>
        private Wall? FindNearestWall()
        {
            float bestDist = float.MaxValue;
            float attachedDist = float.MaxValue;
            Wall? bestWall = null;
            Wall? attached = null;
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this) continue;
                var wall = el.GetComponent<Wall>();
                if (wall == null) continue;
                float dist = DistanceToWall(wall);
                if (wall.gameObject.name == _attachedWallName) { attached = wall; attachedDist = dist; }
                if (dist < bestDist) { bestDist = dist; bestWall = wall; }
            }
            if (attached != null && bestWall != attached &&
                attachedDist - bestDist < WallSwitchHysteresisU)
                return attached;
            return bestWall;
        }

        /// <summary>Расстояние от центра окна до бокса стены. Опущенная камерой
        /// стена (WallCutaway) считается по ПОЛНОЙ высоте — иначе привязка
        /// зависела бы от положения камеры.</summary>
        private float DistanceToWall(Wall wall)
        {
            var t = wall.transform;
            Vector3 center = wall.FullPosition;
            Vector3 half = t.localScale;
            half.y = wall.FullScaleY;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * 0.5f;

            Vector3 local = Quaternion.Inverse(t.rotation) * (transform.position - center);
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            Vector3 closest = center + t.rotation * local;
            return (closest - transform.position).magnitude;
        }

        private void AlignToWall(Wall wall)
        {
            var wallEl = wall.GetComponent<KitchenElement>();
            if (wallEl == null) return;
            var wt = wall.transform;
            var wallDims = wallEl.DimensionsMM;

            // Толщина стены — меньший из горизонтальных габаритов.
            bool thickAlongX = wallDims.x <= wallDims.z;
            int thicknessMM = Mathf.Min(wallDims.x, wallDims.z);

            // Локальная Z окна (глубина) — вдоль оси толщины стены; знак — ближе
            // к текущему развороту окна, чтобы не «перещёлкивало» на 180°.
            Vector3 dir = thickAlongX ? wt.right : wt.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            if (Vector3.Dot(transform.forward, dir) < 0f) dir = -dir;
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

            // Центр окна — в срединную плоскость стены (обнуляем компоненту
            // вдоль толщины, остальные проходят без изменений).
            Vector3 local = wt.InverseTransformPoint(transform.position);
            if (thickAlongX) local.x = 0f; else local.z = 0f;
            Vector3 targetPos = wt.TransformPoint(local);

            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > 0.05f)
                transform.SetPositionAndRotation(targetPos, targetRot);

            // Сначала обрезаем позицию Y (до смены размеров — тогда RebuildMesh
            // вызывается ровно один раз, с финальными размерами и позицией).
            if (wallDims.y <= 0) return;
            int targetY = Mathf.Min(DimensionsMM.y, wallDims.y);
            int targetZ = thicknessMM;
            float toU = AppConstants.MM_TO_UNITS;
            float wallHalfH = wallDims.y * toU * 0.5f;
            float wallCenterY = wall.FullPosition.y;
            float targetHalfH = targetY * toU * 0.5f;
            float clampedY = Mathf.Clamp(transform.position.y,
                wallCenterY - wallHalfH + targetHalfH,
                wallCenterY + wallHalfH - targetHalfH);
            if (Mathf.Abs(clampedY - transform.position.y) > Tolerance.EpsilonUnits)
            {
                transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);
                _lastCutoutPos = new Vector3(float.NaN, 0f, 0f);
            }

            // Размеры — после позиции: ApplyDimensions() → RebuildMesh один раз.
            if (DimensionsMM.y != targetY || DimensionsMM.z != targetZ)
            {
                DimensionsMM = new Vector3Int(DimensionsMM.x, targetY, targetZ);
                _lastCutoutPos = transform.position;
            }

            // Вырез в стене — только если позиция изменилась.
            if (float.IsNaN(_lastCutoutPos.x) ||
                (transform.position - _lastCutoutPos).sqrMagnitude > Tolerance.EpsilonSqr)
            {
                _lastCutoutPos = transform.position;
                wall.RebuildMesh();
            }
        }

        private Wall? FindAttachedWall()
        {
            if (string.IsNullOrEmpty(_attachedWallName)) return null;
            foreach (var el in PartRegistry.All)
            {
                if (el == null) continue;
                var wall = el.GetComponent<Wall>();
                if (wall != null && wall.gameObject.name == _attachedWallName)
                    return wall;
            }
            return null;
        }

        internal void UnregisterFromWall()
        {
            var wall = FindAttachedWall();
            _attachedWallName = "";
            if (wall != null) wall.UnregisterWindow(this);
        }

        // ── Геометрия ───────────────────────────────────────────────────

        /// <summary>Проверяет соседние окна на той же стене и возвращает набор
        /// скрытых сторон frame (битовая маска: 1=left, 2=right, 4=top, 8=bottom).
        /// Сторона скрывается, если другое окно перекрывается с текущим в этой
        /// области — иначе frame одного окна виден в проёме другого (полоса).</summary>
        private int ComputeHiddenSides()
        {
            int hidden = 0;
            var wall = FindAttachedWall();
            if (wall == null) return hidden;

            var wallEl = wall.GetComponent<KitchenElement>();
            if (wallEl == null) return hidden;

            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float halfW = dims.x * 0.5f * toU;
            float halfH = dims.y * 0.5f * toU;

            // Локальные координаты текущего окна относительно стены
            var wallT = wall.transform;
            Vector3 localPos = wallT.InverseTransformPoint(transform.position);
            float lxMin = localPos.x - halfW, lxMax = localPos.x + halfW;
            float lyMin = localPos.y - halfH, lyMax = localPos.y + halfH;

            foreach (var other in wall.AttachedWindows)
            {
                if (other == null || other == this) continue;
                var oDims = other.DimensionsMM;
                var oLocalPos = wallT.InverseTransformPoint(other.transform.position);
                float oHalfW = oDims.x * 0.5f * toU;
                float oHalfH = oDims.y * 0.5f * toU;
                float olxMin = oLocalPos.x - oHalfW, olxMax = oLocalPos.x + oHalfW;
                float olyMin = oLocalPos.y - oHalfH, olyMax = oLocalPos.y + oHalfH;

                // Перекрытие по локальным координатам
                bool yOverlap = lyMax > olyMin && lyMin < olyMax;
                bool xOverlap = lxMax > olxMin && lxMin < olxMax;

                // Если окна перекрываются по Y (локальному), но не по X → скрыть left/right
                if (yOverlap && !xOverlap)
                {
                    if (olxMax >= lxMin && olxMax <= lxMax) hidden |= 1; // left
                    if (olxMin >= lxMin && olxMin <= lxMax) hidden |= 2; // right
                }
                // Если окна перекрываются по X (локальному), но не по Y → скрыть top/bottom
                if (xOverlap && !yOverlap)
                {
                    if (olyMax >= lyMin && olyMax <= lyMax) hidden |= 4; // top
                    if (olyMin >= lyMin && olyMin <= lyMax) hidden |= 8; // bottom
                }
            }
            foreach (var other in wall.AttachedDoors)
            {
                if (other == null) continue;
                var oDims = other.DimensionsMM;
                var oLocalPos = wallT.InverseTransformPoint(other.transform.position);
                float oHalfW = oDims.x * 0.5f * toU;
                float oHalfH = oDims.y * 0.5f * toU;
                float olxMin = oLocalPos.x - oHalfW, olxMax = oLocalPos.x + oHalfW;
                float olyMin = oLocalPos.y - oHalfH, olyMax = oLocalPos.y + oHalfH;

                bool yOverlap = lyMax > olyMin && lyMin < olyMax;
                bool xOverlap = lxMax > olxMin && lxMin < olxMax;

                if (yOverlap && !xOverlap)
                {
                    if (olxMax >= lxMin && olxMax <= lxMax) hidden |= 1;
                    if (olxMin >= lxMin && olxMin <= lxMax) hidden |= 2;
                }
                if (xOverlap && !yOverlap)
                {
                    if (olyMax >= lyMin && olyMax <= lyMax) hidden |= 4;
                    if (olyMin >= lyMin && olyMin <= lyMax) hidden |= 8;
                }
            }
            return hidden;
        }

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = EffectiveScale;
        }

        private void EnsureChildren()
        {
            if (_staticGroup == null)
            {
                var staticGo = new GameObject("_Static");
                staticGo.transform.SetParent(transform, false);
                _staticGroup = staticGo.transform;
            }
            if (_sashGroup == null)
            {
                var sashGo = new GameObject("_Sash");
                sashGo.transform.SetParent(transform, false);
                _sashGroup = sashGo.transform;
            }

            const int needed = 15;
            while (_children.Count < needed)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = GetChildName(idx);
                child.transform.SetParent(IsSashChild(idx) ? _sashGroup : _staticGroup, false);
                var col = child.GetComponent<BoxCollider>();
                if (col != null) Object.DestroyImmediate(col);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _children.Add(child);
            }

            _frameLeft   = _children[0];
            _frameRight  = _children[1];
            _frameTop    = _children[2];
            _frameBottom = _children[3];
            _glassPane   = _children[4];
            _sillObj     = _children[5];
            _dripObj     = _children[6];
            _slopeTop    = _children[7];
            _slopeBottom = _children[8];
            _slopeLeft   = _children[9];
            _slopeRight  = _children[10];
            _sashLeft    = _children[11];
            _sashRight   = _children[12];
            _sashTop     = _children[13];
            _sashBottom  = _children[14];
        }

        // Створка = стекло (4) + обвязка (11-14); остальное — неподвижная коробка.
        private static bool IsSashChild(int idx) => idx == 4 || idx >= 11;

        private string GetChildName(int idx) => idx switch
        {
            0 => "FrameLeft", 1 => "FrameRight", 2 => "FrameTop", 3 => "FrameBottom",
            4 => "Glass", 5 => "Sill", 6 => "DripCap",
            7 => "SlopeTop", 8 => "SlopeBottom", 9 => "SlopeLeft", 10 => "SlopeRight",
            11 => "SashLeft", 12 => "SashRight", 13 => "SashTop", 14 => "SashBottom",
            _ => "Child" + idx
        };

        private void RebuildGeometry()
        {
            if (_staticGroup == null || _sashGroup == null) return;
            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float frameU = AppConstants.WINDOW_FRAME_MM * toU;
            float glassThick = AppConstants.WINDOW_GLASS_THICKNESS_MM * toU;

            float totalW = dims.x * toU;
            float totalH = dims.y * toU;
            float totalD = dims.z * toU;
            float halfW = totalW * 0.5f;
            float halfH = totalH * 0.5f;
            float halfD = totalD * 0.5f;

            float innerW = totalW - 2f * frameU;
            float innerH = totalH - 2f * frameU;

            // Определяем скрытые стороны frame из-за соседних окон
            int hidden = ComputeHiddenSides();

            if (_frameLeft != null)
            {
                _frameLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, 0f);
                _frameLeft.transform.localScale = new Vector3(frameU, totalH, totalD);
                _frameLeft.SetActive((hidden & 1) == 0);
            }
            if (_frameRight != null)
            {
                _frameRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, 0f);
                _frameRight.transform.localScale = new Vector3(frameU, totalH, totalD);
                _frameRight.SetActive((hidden & 2) == 0);
            }
            if (_frameTop != null)
            {
                _frameTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, 0f);
                _frameTop.transform.localScale = new Vector3(innerW, frameU, totalD);
                _frameTop.SetActive((hidden & 4) == 0);
            }
            if (_frameBottom != null)
            {
                _frameBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, 0f);
                _frameBottom.transform.localScale = new Vector3(innerW, frameU, totalD);
                _frameBottom.SetActive((hidden & 8) == 0);
            }

            // Створка: коробчатая обвязка по центру коробки по глубине.
            float sashU = AppConstants.WINDOW_SASH_MM * toU;
            float sashD = Mathf.Min(AppConstants.WINDOW_SASH_DEPTH_MM * toU, totalD);
            _sashClosedLocal = new Vector3(0f, 0f, 0f);
            _sashHalfExtents = new Vector3(innerW * 0.5f, innerH * 0.5f, sashD * 0.5f);

            if (_sashLeft != null)
            {
                _sashLeft.transform.localPosition = new Vector3(-innerW * 0.5f + sashU * 0.5f, 0f, 0f);
                _sashLeft.transform.localScale = new Vector3(sashU, innerH, sashD);
                _sashLeft.SetActive(true);
            }
            if (_sashRight != null)
            {
                _sashRight.transform.localPosition = new Vector3(innerW * 0.5f - sashU * 0.5f, 0f, 0f);
                _sashRight.transform.localScale = new Vector3(sashU, innerH, sashD);
                _sashRight.SetActive(true);
            }
            if (_sashTop != null)
            {
                _sashTop.transform.localPosition = new Vector3(0f, innerH * 0.5f - sashU * 0.5f, 0f);
                _sashTop.transform.localScale = new Vector3(innerW - 2f * sashU, sashU, sashD);
                _sashTop.SetActive(true);
            }
            if (_sashBottom != null)
            {
                _sashBottom.transform.localPosition = new Vector3(0f, -innerH * 0.5f + sashU * 0.5f, 0f);
                _sashBottom.transform.localScale = new Vector3(innerW - 2f * sashU, sashU, sashD);
                _sashBottom.SetActive(true);
            }
            if (_glassPane != null)
            {
                _glassPane.transform.localPosition = Vector3.zero;
                _glassPane.transform.localScale = new Vector3(innerW - 2f * sashU, innerH - 2f * sashU, glassThick);
                _glassPane.SetActive(true);
            }

            // Подоконник: плита постоянной толщины, вылет — горизонтально от
            // плоскости стены; верх плиты вровень с верхом нижнего бруса коробки.
            if (_sillObj != null)
            {
                float sillProt = _sillProtrusionMM * toU;
                float sillThick = AppConstants.WINDOW_SILL_THICKNESS_MM * toU;
                _sillObj.transform.localPosition = new Vector3(
                    0f, -halfH + frameU - sillThick * 0.5f, halfD + sillProt * 0.5f);
                _sillObj.transform.localScale = new Vector3(totalW, sillThick, sillProt);
                _sillObj.SetActive(_sillProtrusionMM > 0);
            }
            if (_dripObj != null)
            {
                float dripH = AppConstants.WINDOW_DRIP_DEFAULT_MM * toU;
                float dripProtr = 30f * toU;
                _dripObj.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, -halfD - dripProtr * 0.5f);
                _dripObj.transform.localScale = new Vector3(totalW, dripH, dripProtr);
                _dripObj.SetActive(true);
            }
            if (_slopeTop != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeTop.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeTop.SetActive((hidden & 4) == 0);
            }
            if (_slopeBottom != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeBottom.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeBottom.SetActive((hidden & 8) == 0);
            }
            if (_slopeLeft != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeLeft.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeLeft.SetActive((hidden & 1) == 0);
            }
            if (_slopeRight != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeRight.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeRight.SetActive((hidden & 2) == 0);
            }

            ApplyDoorPose();
            ApplyTint();
            ApplyMaterialFrame();
        }

        private static Shader GetShader()
        {
            if (_cachedShader == null)
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            return _cachedShader;
        }

        private void ApplyTint()
        {
            if (_glassPane == null) return;
            var mr = _glassPane.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_tint == GlassTint.Tinted)
            {
                if (_tintedGlassMat == null)
                    _tintedGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.15f, 0.18f, 0.22f, 0.70f));
                mr.sharedMaterial = _tintedGlassMat;
            }
            else
            {
                if (_clearGlassMat == null)
                    _clearGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.6f, 0.75f, 0.85f, 0.35f));
                mr.sharedMaterial = _clearGlassMat;
            }
        }

        private static Material SlopeMaterial()
        {
            if (_slopeMat == null)
            {
                _slopeMat = new Material(GetShader());
                _slopeMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.82f, 1f));
                _slopeMat.color = new Color(0.85f, 0.85f, 0.82f, 1f);
            }
            return _slopeMat;
        }

        private void ApplyMaterialFrame()
        {
            var def = MaterialCatalog.Get(MaterialId);
            if (def == null) return;
            var mat = MaterialManager.GetSharedMaterial(def);
            if (mat == null) return;
            foreach (var go in new[] { _frameLeft, _frameRight, _frameTop, _frameBottom,
                                       _sashLeft, _sashRight, _sashTop, _sashBottom,
                                       _slopeTop, _slopeBottom, _slopeLeft, _slopeRight })
            {
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                if (mr != null) mr.sharedMaterial = mat;
            }
            var slopeMat = SlopeMaterial();
            foreach (var go in new[] { _sillObj, _dripObj })
            {
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                if (mr != null) mr.sharedMaterial = slopeMat;
            }
        }

        public void DestroyChildren()
        {
            foreach (var child in _children)
                if (child != null)
                {
                    if (Application.isPlaying) Object.Destroy(child);
                    else Object.DestroyImmediate(child);
                }
            _children.Clear();
            _frameLeft = _frameRight = _frameTop = _frameBottom = null;
            _glassPane = _sillObj = _dripObj = null;
            _slopeTop = _slopeBottom = _slopeLeft = _slopeRight = null;
            _sashLeft = _sashRight = _sashTop = _sashBottom = null;
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            UnregisterFromWall();
            DestroyChildren();
            DestroyGroup(ref _staticGroup);
            DestroyGroup(ref _sashGroup);
        }

        private static void DestroyGroup(ref Transform? group)
        {
            if (group == null) return;
            if (Application.isPlaying) Object.Destroy(group.gameObject);
            else Object.DestroyImmediate(group.gameObject);
            group = null;
        }
    }
}

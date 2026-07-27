using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Врезная прямоугольная мойка под модуль 600 мм: борт лежит на столешнице и
    /// перекрывает срез, чаша уходит в сквозной проём, сзади стоит смеситель.
    ///
    /// Мойка живёт только на ДЕТАЛИ (базовый KitchenElement с горизонтальной
    /// пластью — столешница): каждый кадр она прилипает к ближайшей подходящей
    /// детали, встаёт бортом на её верхнюю пласть и хранит смещение от центра
    /// детали в её ЛОКАЛЬНЫХ миллиметрах. Отсюда оба требования выполняются сами
    /// собой: деталь двигают — мойка едет с ней (смещение неизменно), деталь
    /// растягивают — доля проёма пересчитывается от новых габаритов.
    ///
    /// Геометрия строится в мировых единицах при единичном масштабе корня — как
    /// у WindowElement/PillarElement, иначе дети масштабируются дважды.
    /// </summary>
    public class SinkElement : KitchenElement
    {
        // ── Габариты (мм) ───────────────────────────────────────────────
        // Типовая врезная мойка под тумбу 600: внешний контур 600×500,
        // проём в столешнице 560×460 (борт перекрывает срез на 20 мм с каждой
        // стороны), короб чаши 540×440 — на 10 мм уже проёма, чтобы пройти в него.
        public const int OUTER_WIDTH_MM = 600;
        public const int OUTER_DEPTH_MM = 500;
        public const int RIM_HEIGHT_MM = 8;          // высота борта над столешницей
        public const int RIM_WIDTH_MM = 30;          // ширина борта (контур → короб чаши)
        public const int CUTOUT_CLEARANCE_MM = 10;   // зазор «проём − короб чаши» на сторону
        public const int BOWL_DEPTH_MM = 180;
        public const int BOWL_WALL_MM = 10;
        public const int MIN_EDGE_MM = 30;           // остаток столешницы за проёмом

        public static int CutoutWidthMM => OUTER_WIDTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int CutoutDepthMM => OUTER_DEPTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int TotalHeightMM => RIM_HEIGHT_MM + BOWL_DEPTH_MM;

        /// <summary>Минимальные габариты детали, в которую мойка помещается.</summary>
        public static int MinPartWidthMM => CutoutWidthMM + 2 * MIN_EDGE_MM;
        public static int MinPartDepthMM => CutoutDepthMM + 2 * MIN_EDGE_MM;

        // Корень мойки сидит в плоскости столешницы, а габаритный короб уходит
        // вниз — центр короба смещён относительно начала координат.
        private static float CenterOffsetUnits =>
            (BOWL_DEPTH_MM - RIM_HEIGHT_MM) * 0.5f * AppConstants.MM_TO_UNITS;

        // Гистерезис смены детали: на стыке двух столешниц расстояния почти равны
        // и дрожат — без запаса мойка скакала бы между ними (как окно между стен).
        private const float PartSwitchHysteresisU = 0.05f; // 50 мм

        [SerializeField] private string _attachedPartName = "";
        [SerializeField] private int _offsetXMM;   // от центра детали вдоль её локальной X
        [SerializeField] private int _offsetYMM;   // от центра детали вдоль её локальной Y

        private static Material? _steelMat;
        private static Material? _darkMat;

        private readonly List<GameObject> _children = new List<GameObject>();
        // Сторона, куда смотрит смеситель, в ЛОКАЛЬНОЙ оси Z мойки (+1 / −1).
        private int _faucetSign = 1;
        // Деталь и смещение, для которых в последний раз перестраивался проём.
        private KitchenElement? _lastHost;
        private int _lastOffsetXMM = int.MinValue;
        private int _lastOffsetYMM = int.MinValue;
        // Поза детали на прошлом кадре — по ней отличаем «пользователь тянет
        // мойку» от «поехала сама деталь» (см. AlignToPart).
        private Vector3 _lastHostPos;
        private Quaternion _lastHostRot = Quaternion.identity;
        private bool _hasHostPose;

        public string AttachedPartName { get => _attachedPartName; set => _attachedPartName = value ?? ""; }
        public int OffsetXMM { get => _offsetXMM; set => _offsetXMM = value; }
        public int OffsetYMM { get => _offsetYMM; set => _offsetYMM = value; }

        protected override Vector3 EffectiveScale => new Vector3(
            OUTER_WIDTH_MM * AppConstants.MM_TO_UNITS,
            TotalHeightMM * AppConstants.MM_TO_UNITS,
            OUTER_DEPTH_MM * AppConstants.MM_TO_UNITS);

        // Габаритный короб (выделение, ручки, валидация) — вокруг чаши, а не
        // вокруг начала координат: иначе он висел бы наполовину над столешницей.
        protected override Vector3 ValidationPosition =>
            transform.position - transform.rotation * new Vector3(0f, CenterOffsetUnits, 0f);

        private void Start()
        {
            SnapToPart();
        }

        private void Update()
        {
            SnapToPart();
        }

        /// <summary>Размер мойки фиксирован моделью — ресайз лишь возвращает его
        /// на место (как у опоры).</summary>
        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            Data.DimensionsMM = new Vector3Int(OUTER_WIDTH_MM, TotalHeightMM, OUTER_DEPTH_MM);
            UpdateCollider();
            EnsureChildren();
            RebuildGeometry();
        }

        // ── Привязка к детали ───────────────────────────────────────────

        /// <summary>Прилипание к ближайшей подходящей детали: регистрация проёма,
        /// разворот по её осям, посадка борта на верхнюю пласть.</summary>
        public void SnapToPart()
        {
            var part = FindNearestHostPart();
            if (part == null) return;

            // Проверяем фактическое членство, а не только имя: после загрузки
            // сцены имя уже восстановлено из сейва, но деталь мойку ещё не знает —
            // без регистрации проём не строится.
            if (part.PartName != _attachedPartName || !part.HasSink(this))
            {
                UnregisterFromPart();
                _attachedPartName = part.PartName;
                part.RegisterSink(this);
            }
            AlignToPart(part);
        }

        public void AttachToPart(KitchenElement part)
        {
            if (part == null || !IsSuitableHost(part)) return;
            UnregisterFromPart();
            _attachedPartName = part.PartName;
            part.RegisterSink(this);
            AlignToPart(part);
        }

        internal void UnregisterFromPart()
        {
            var part = FindAttachedPart();
            _attachedPartName = "";
            _lastHost = null;
            _hasHostPose = false;
            if (part != null) part.UnregisterSink(this);
        }

        /// <summary>Деталь годится под мойку: это базовая «Деталь» (у фасада,
        /// стола и прочих подтипов геометрия своя, врезка в неё не определена),
        /// её пласть горизонтальна и проём с запасом по краям помещается.</summary>
        public static bool IsSuitableHost(KitchenElement part)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var dims = part.DimensionsMM;
            if (dims.x < MinPartWidthMM || dims.y < MinPartDepthMM) return false;
            // Пласть (локальная ±Z) должна смотреть вверх — столешница, а не стойка.
            float upness = Mathf.Abs((part.transform.rotation * Vector3.forward).y);
            return upness > 0.7f;
        }

        private KitchenElement? FindNearestHostPart()
        {
            float bestDist = float.MaxValue;
            float attachedDist = float.MaxValue;
            KitchenElement? best = null;
            KitchenElement? attached = null;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null || el == this || !IsSuitableHost(el)) continue;
                float dist = DistanceToPart(el);
                if (el.PartName == _attachedPartName) { attached = el; attachedDist = dist; }
                if (dist < bestDist) { bestDist = dist; best = el; }
            }
            if (attached != null && best != attached && attachedDist - bestDist < PartSwitchHysteresisU)
                return attached;
            return best;
        }

        /// <summary>Расстояние от центра чаши до бокса детали.</summary>
        private float DistanceToPart(KitchenElement part)
        {
            var t = part.transform;
            var half = t.localScale;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * 0.5f;

            Vector3 center = ValidationPosition;
            Vector3 local = Quaternion.Inverse(t.rotation) * (center - t.position);
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return (t.position + t.rotation * local - center).magnitude;
        }

        private void AlignToPart(KitchenElement part)
        {
            var pt = part.transform;
            var dims = part.DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;

            // Верхняя пласть — та из ±Z, что смотрит вверх. Локальная Y мойки
            // (вверх) ложится на неё, локальная X — вдоль X детали.
            bool topIsPlusZ = (pt.rotation * Vector3.forward).y >= 0f;
            Quaternion targetRot = pt.rotation * Quaternion.Euler(topIsPlusZ ? 90f : -90f, 0f, 0f);

            // Кто именно сдвинулся? Если деталь стоит на месте, значит мойку тянут
            // мышью — проецируем её мировую позицию обратно на пласть и получаем
            // новое смещение. Если же поехала (или повернулась) сама деталь,
            // смещение СОХРАНЯЕМ: мойка обязана уехать вместе со столешницей.
            bool hostMoved = _hasHostPose && _lastHost == part &&
                ((pt.position - _lastHostPos).sqrMagnitude > Tolerance.EpsilonSqr ||
                 Quaternion.Angle(pt.rotation, _lastHostRot) > 0.05f);

            int maxX = (dims.x - CutoutWidthMM) / 2 - MIN_EDGE_MM;
            int maxY = (dims.y - CutoutDepthMM) / 2 - MIN_EDGE_MM;
            if (!hostMoved)
            {
                Vector3 local = Quaternion.Inverse(pt.rotation) * (transform.position - pt.position);
                _offsetXMM = Mathf.RoundToInt(local.x / toU);
                _offsetYMM = Mathf.RoundToInt(local.y / toU);
            }
            // Клампим всегда: ресайз детали может оставить проём за её краем.
            _offsetXMM = Mathf.Clamp(_offsetXMM, -maxX, maxX);
            _offsetYMM = Mathf.Clamp(_offsetYMM, -maxY, maxY);

            _lastHostPos = pt.position;
            _lastHostRot = pt.rotation;
            _hasHostPose = true;

            float halfThickness = dims.z * 0.5f * toU;
            Vector3 targetPos = pt.position + pt.rotation * new Vector3(
                _offsetXMM * toU,
                _offsetYMM * toU,
                topIsPlusZ ? halfThickness : -halfThickness);

            if ((targetPos - transform.position).sqrMagnitude > Tolerance.EpsilonSqr ||
                Quaternion.Angle(targetRot, transform.rotation) > 0.05f)
                transform.SetPositionAndRotation(targetPos, targetRot);

            // Смеситель — с той стороны, где до края детали больше места
            // (мойку обычно сдвигают к переднему краю, кран остаётся сзади).
            // Локальная +Y детали смотрит в −Z мойки, когда верх — это +Z детали.
            float spacePlusY = dims.y * 0.5f - _offsetYMM - OUTER_DEPTH_MM * 0.5f;
            float spaceMinusY = dims.y * 0.5f + _offsetYMM - OUTER_DEPTH_MM * 0.5f;
            int plusYInSinkZ = topIsPlusZ ? -1 : 1;
            int faucetSign = spacePlusY >= spaceMinusY ? plusYInSinkZ : -plusYInSinkZ;
            if (faucetSign != _faucetSign)
            {
                _faucetSign = faucetSign;
                RebuildGeometry();
            }

            // Проём перестраиваем только когда он реально изменился.
            if (_lastHost != part || _lastOffsetXMM != _offsetXMM || _lastOffsetYMM != _offsetYMM)
            {
                _lastHost = part;
                _lastOffsetXMM = _offsetXMM;
                _lastOffsetYMM = _offsetYMM;
                part.RebuildGrooveMesh();
            }
        }

        private KitchenElement? FindAttachedPart()
        {
            if (string.IsNullOrEmpty(_attachedPartName)) return null;
            foreach (var el in PartRegistry.GetAll())
                if (el != null && el != this && el.PartName == _attachedPartName)
                    return el;
            return null;
        }

        /// <summary>Проём мойки в нормализованных координатах пласти детали.
        /// Доли считаются от ТЕКУЩИХ габаритов, поэтому ресайз детали двигает и
        /// перемасштабирует вырез автоматически.</summary>
        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var dims = part.DimensionsMM;
            if (dims.x <= 0 || dims.y <= 0) return default;

            float halfW = CutoutWidthMM * 0.5f;
            float halfD = CutoutDepthMM * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (_offsetXMM - halfW) / dims.x,
                xMax = (_offsetXMM + halfW) / dims.x,
                yMin = (_offsetYMM - halfD) / dims.y,
                yMax = (_offsetYMM + halfD) / dims.y,
            };
        }

        // ── Геометрия ───────────────────────────────────────────────────

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = EffectiveScale;
            box.center = new Vector3(0f, -CenterOffsetUnits, 0f);
        }

        // 0-3 борт, 4-8 чаша, 9-13 смеситель.
        private const int ChildCount = 14;

        private static bool IsCylinder(int idx) => idx == 9 || idx == 10 || idx == 12;

        private static string ChildName(int idx) => idx switch
        {
            0 => "RimFront", 1 => "RimBack", 2 => "RimLeft", 3 => "RimRight",
            4 => "BowlFront", 5 => "BowlBack", 6 => "BowlLeft", 7 => "BowlRight",
            8 => "BowlBottom",
            9 => "FaucetBase", 10 => "FaucetStand", 11 => "FaucetSpout",
            12 => "FaucetOutlet", 13 => "FaucetHandle",
            _ => "Child" + idx
        };

        private void EnsureChildren()
        {
            while (_children.Count < ChildCount)
            {
                int idx = _children.Count;
                GameObject child;
                if (IsCylinder(idx))
                {
                    // НЕ CreatePrimitive(Cylinder): он тянет за собой коллайдер,
                    // который мойке не нужен, а встроенный меш берётся напрямую.
                    child = new GameObject(ChildName(idx));
                    child.AddComponent<MeshFilter>().sharedMesh =
                        Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
                    child.AddComponent<MeshRenderer>();
                }
                else
                {
                    child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    child.name = ChildName(idx);
                    var col = child.GetComponent<BoxCollider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
                child.transform.SetParent(transform, false);
                _children.Add(child);
            }
        }

        private void RebuildGeometry()
        {
            if (_children.Count < ChildCount) return;
            float toU = AppConstants.MM_TO_UNITS;

            float outerW = OUTER_WIDTH_MM * toU;
            float outerD = OUTER_DEPTH_MM * toU;
            float rimH = RIM_HEIGHT_MM * toU;
            float rimW = RIM_WIDTH_MM * toU;
            float bowlW = outerW - 2f * rimW;               // короб чаши по X
            float bowlD = outerD - 2f * rimW;               // короб чаши по Z
            float bowlH = BOWL_DEPTH_MM * toU;
            float wall = BOWL_WALL_MM * toU;

            // Борт: рамка вокруг проёма, верх на rimH над пластью.
            Cube(0, new Vector3(0f, rimH * 0.5f, (outerD - rimW) * 0.5f), new Vector3(outerW, rimH, rimW));
            Cube(1, new Vector3(0f, rimH * 0.5f, -(outerD - rimW) * 0.5f), new Vector3(outerW, rimH, rimW));
            Cube(2, new Vector3(-(outerW - rimW) * 0.5f, rimH * 0.5f, 0f), new Vector3(rimW, rimH, bowlD));
            Cube(3, new Vector3((outerW - rimW) * 0.5f, rimH * 0.5f, 0f), new Vector3(rimW, rimH, bowlD));

            // Чаша: четыре стенки от пласти вниз и дно.
            Cube(4, new Vector3(0f, -bowlH * 0.5f, (bowlD - wall) * 0.5f), new Vector3(bowlW, bowlH, wall));
            Cube(5, new Vector3(0f, -bowlH * 0.5f, -(bowlD - wall) * 0.5f), new Vector3(bowlW, bowlH, wall));
            Cube(6, new Vector3(-(bowlW - wall) * 0.5f, -bowlH * 0.5f, 0f), new Vector3(wall, bowlH, bowlD - 2f * wall));
            Cube(7, new Vector3((bowlW - wall) * 0.5f, -bowlH * 0.5f, 0f), new Vector3(wall, bowlH, bowlD - 2f * wall));
            Cube(8, new Vector3(0f, -bowlH + wall * 0.5f, 0f), new Vector3(bowlW, wall, bowlD));

            // Смеситель: пятак + стойка + излив с носиком + рычаг. Стоит на
            // БОРТУ мойки (у врезных моек отверстие под кран — в бортике), а не
            // на столешнице: у тумбы 600 за мойкой остаётся ~50 мм, и пятак
            // Ø55 свисал бы с края.
            float s = _faucetSign;
            float baseZ = s * (outerD - rimW) * 0.5f;
            float y0 = rimH;                        // верх борта — «пол» смесителя
            float baseH = 20f * toU;
            float standTop = y0 + 250f * toU;
            float spoutLen = 210f * toU;
            float spoutH = 25f * toU;

            Cylinder(9, new Vector3(0f, y0 + baseH * 0.5f, baseZ), 55f * toU, baseH);
            Cylinder(10, new Vector3(0f, (y0 + baseH + standTop) * 0.5f, baseZ),
                35f * toU, standTop - y0 - baseH);
            Cube(11, new Vector3(0f, standTop + spoutH * 0.5f, baseZ - s * spoutLen * 0.5f),
                new Vector3(30f * toU, spoutH, spoutLen));
            Cylinder(12, new Vector3(0f, standTop - 15f * toU, baseZ - s * (spoutLen - 15f * toU)),
                18f * toU, 30f * toU);
            var handle = _children[13];
            handle.transform.localPosition = new Vector3(0f,
                standTop + spoutH + 22f * toU, baseZ - s * 30f * toU);
            handle.transform.localRotation = Quaternion.Euler(s * 25f, 0f, 0f);
            handle.transform.localScale = new Vector3(16f * toU, 16f * toU, 90f * toU);

            ApplyMaterials();
        }

        private void Cube(int idx, Vector3 localPos, Vector3 localScale)
        {
            var go = _children[idx];
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
        }

        /// <summary>Встроенный цилиндр имеет диаметр 1 и высоту 2 — отсюда деление.</summary>
        private void Cylinder(int idx, Vector3 localPos, float diameter, float height)
        {
            var go = _children[idx];
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        }

        private void ApplyMaterials()
        {
            var steel = SteelMaterial();
            var dark = DarkMaterial();
            for (int i = 0; i < _children.Count; i++)
            {
                var mr = _children[i].GetComponent<MeshRenderer>();
                if (mr == null) continue;
                // Дно чаши чуть темнее — иначе чаша читается плоской заливкой.
                mr.sharedMaterial = i == 8 ? dark : steel;
            }
        }

        private static Material SteelMaterial()
        {
            if (_steelMat == null)
            {
                var color = new Color(0.72f, 0.74f, 0.76f, 1f);
                _steelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _steelMat.SetColor("_BaseColor", color);
                _steelMat.color = color;
                _steelMat.SetFloat("_Metallic", 0.85f);
                _steelMat.SetFloat("_Smoothness", 0.75f);
            }
            return _steelMat!;
        }

        private static Material DarkMaterial()
        {
            if (_darkMat == null)
            {
                var color = new Color(0.55f, 0.57f, 0.59f, 1f);
                _darkMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _darkMat.SetColor("_BaseColor", color);
                _darkMat.color = color;
                _darkMat.SetFloat("_Metallic", 0.7f);
                _darkMat.SetFloat("_Smoothness", 0.6f);
            }
            return _darkMat!;
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

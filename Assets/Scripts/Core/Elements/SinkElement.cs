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
    public class SinkElement : KitchenElement, IPartCutout
    {

        public override string DisplayTypeName => "Мойка";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;
        // ── Габариты (мм) ───────────────────────────────────────────────
        // Мойка НЕ равна модулю: из 600 мм ширины тумбы боковины съедают по 18 мм,
        // ещё запас нужен на крепёж и на кромку столешницы, поэтому стандартная
        // врезная мойка под тумбу 600 — 500×500. Проём 460×460 (борт перекрывает
        // срез на 20 мм с каждой стороны), короб чаши 440×440 — на 10 мм уже
        // проёма, чтобы в него пройти.
        public const int MODULE_WIDTH_MM = 600;      // тумба, под которую рассчитана мойка
        public const int OUTER_WIDTH_MM = 500;
        public const int OUTER_DEPTH_MM = 500;
        public const int RIM_HEIGHT_MM = 8;          // высота борта над столешницей
        public const int RIM_WIDTH_MM = 30;          // ширина борта (контур → короб чаши)
        public const int CUTOUT_CLEARANCE_MM = 10;   // зазор «проём − короб чаши» на сторону
        public const int BOWL_DEPTH_MM = 180;
        public const int BOWL_WALL_MM = 10;
        public const int MIN_EDGE_MM = 30;           // остаток столешницы за проёмом

        /// <summary>Ловится сверху с этой высоты над пластью; выше — мойка просто
        /// висит в воздухе и ничего не режет.</summary>
        public const int SNAP_CATCH_MM = 100;
        /// <summary>Протащили ниже пласти на столько — мойка отлипает и идёт
        /// дальше вниз (иначе от столешницы было бы не оторваться).</summary>
        public const int SNAP_RELEASE_MM = 60;

        public static int CutoutWidthMM => OUTER_WIDTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int CutoutDepthMM => OUTER_DEPTH_MM - 2 * (RIM_WIDTH_MM - CUTOUT_CLEARANCE_MM);
        public static int TotalHeightMM => RIM_HEIGHT_MM + BOWL_DEPTH_MM;

        /// <summary>Минимальные габариты детали, в которую мойка помещается.
        /// На модуле 600 столешница 600×600 обязана подходить — отсюда и размер
        /// самой мойки.</summary>
        public static int MinPartWidthMM => CutoutWidthMM + 2 * MIN_EDGE_MM;
        public static int MinPartDepthMM => CutoutDepthMM + 2 * MIN_EDGE_MM;

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

        // «Свободная» высота над пластью в мм: пока мойка прилипла, её позу
        // диктуем мы, и намерение пользователя жило бы только в этом счётчике —
        // по нему и решается отрыв. Копится из дрейфа (см. TrackDrift).
        private float _freeHeightMM;
        // Позиция, которую выставили сами: всё, что появилось сверх неё, —
        // движение пользователя (drag, стрелки, MCP), а не переезд столешницы.
        private Vector3 _appliedPos;
        private bool _hasAppliedPos;

        [NotUndoable("служебная привязка к детали, вычисляется SnapToPart")]
        public string AttachedPartName { get => _attachedPartName; set => _attachedPartName = value ?? ""; }

        [NotUndoable("смещение от центра детали — производная позиции, откатывается MoveCommand")]
        public int OffsetXMM { get => _offsetXMM; set => _offsetXMM = value; }

        [NotUndoable("см. OffsetXMM")]
        public int OffsetYMM { get => _offsetYMM; set => _offsetYMM = value; }
        public bool IsAttached => _lastHost != null;

        // Габарит для ВЫДЕЛЕНИЯ И РУЧЕК — плита борта на столешнице, а не короб
        // вместе с чашей. Короб уходит на 180 мм в тело детали, и центры его
        // боковых граней (а с ними и ручки) оказывались замурованы в столешнице.
        // Плоская же плита проходит общим путём HandlePlacement: тонкая ось Y →
        // ручки выносятся из плоскости к камере, ровно как у стены и полки.
        protected override Vector3 EffectiveScale => new Vector3(
            OUTER_WIDTH_MM * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            OUTER_DEPTH_MM * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        // Бортик мойки приподнят над плоскостью врезки — сдвиг едет вместе с
        // примеряемой позицией.
        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, RIM_HEIGHT_MM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        private void Start()
        {
            SnapToPart();
        }

        private int _lastPoseVersion;

        private void Update()
        {
            if (PoseVersion != _lastPoseVersion)
            {
                _lastPoseVersion = PoseVersion;
                SnapToPart();
            }
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

        /// <summary>Магнит к столешнице: пока мойка прилипла — держим её позу и
        /// копим «свободную» высоту из движений пользователя; вышли за полосу
        /// захвата (вверх) или протащили ниже пласти — отлипаем и идём дальше.
        /// Прилипнуть можно только СВЕРХУ и только к детали, в которую проём
        /// физически влезает и не попадает на боковину.</summary>
        public void SnapToPart()
        {
            using var _ = PerfMarkers.SinkSnapToPart.Auto();
            var host = _lastHost != null ? _lastHost : FindAttachedPart();
            TrackDrift(host);

            if (host != null && !StillHolds(host)) { ReleaseFrom(host); host = null; }
            if (host == null) host = FindCatchingPart();
            if (host == null) return;

            if (host.PartName != _attachedPartName || !host.HasCutout(this))
            {
                // Проверяем фактическое членство, а не только имя: после загрузки
                // сцены имя уже восстановлено из сейва, но деталь мойку ещё не
                // знает — без регистрации проём не строится.
                UnregisterFromPart();
                _attachedPartName = host.PartName;
                host.RegisterCutout(this);
            }
            AlignToPart(host);
        }

        public void AttachToPart(KitchenElement part)
        {
            if (part == null || !IsSuitableHost(part)) return;
            UnregisterFromPart();
            _attachedPartName = part.PartName;
            part.RegisterCutout(this);
            _freeHeightMM = 0f;
            AlignToPart(part);
        }

        internal void UnregisterFromPart()
        {
            var part = FindAttachedPart();
            _attachedPartName = "";
            _lastHost = null;
            if (part != null) part.UnregisterCutout(this);
        }

        /// <summary>Отлипнуть и догнать курсор: пока мойка сидела на пласти, её
        /// позу диктовали мы, а намерение пользователя копилось в _freeHeightMM —
        /// теперь мойка прыгает туда, куда её всё это время тянули. Без прыжка
        /// она осталась бы на пласти и тут же прилипла снова.</summary>
        private void ReleaseFrom(KitchenElement host)
        {
            var (upAxis, upSign) = UpAxisOf(host);
            Vector3 up = host.transform.rotation * (AxisVector(upAxis) * upSign);
            // Догоняем ровно недостающее: часть пути мойка уже проехала в этом
            // кадре (её сдвинул drag), остальное копилось, пока она держалась.
            float actualHeightMM = LocalPose(host).heightMM;
            transform.position += up * ((_freeHeightMM - actualHeightMM) * AppConstants.MM_TO_UNITS);
            _appliedPos = transform.position;

            UnregisterFromPart();
            _freeHeightMM = 0f;
            _lastOffsetXMM = int.MinValue;
            _lastOffsetYMM = int.MinValue;
        }

        /// <summary>Разложить внешнее смещение (drag, стрелки, MCP) по осям детали.
        /// Всё, что появилось сверх выставленной нами позы, — движение
        /// пользователя; переезд самой столешницы дрейфа не даёт, потому что
        /// мойку в тот кадр двигали мы же.</summary>
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

        // ── Оси детали ──────────────────────────────────────────────────
        // Столешницу собирают по-разному: повёрнутой доской (толщина по локальной
        // Z — «пласть») или коробом, у которого толщина лежит по Y. Поэтому
        // верхнюю грань ищем перебором осей, а не считаем, что это всегда ±Z.

        private static Vector3 AxisVector(int axis) =>
            axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        /// <summary>Локальная ось детали, смотрящая вверх, и её знак.</summary>
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

        /// <summary>Ось, поперёк которой режется проём в этой детали.</summary>
        public static int HoleAxisFor(KitchenElement part) => UpAxisOf(part).axis;

        public int HoleAxisIn(KitchenElement part) => HoleAxisFor(part);

        /// <summary>Две оси плоскости столешницы в том порядке, в каком их ждёт
        /// строитель меша: первая ложится на его X, вторая — на Y (см.
        /// GrooveMesh.Build с holeAxis).</summary>
        private static (int a, int b) PlaneAxes(int upAxis) => upAxis switch
        {
            2 => (0, 1),   // вырез вдоль Z — сетка в XY (канонический случай)
            1 => (0, 2),   // вдоль Y — сетка в XZ
            _ => (2, 1),   // вдоль X — сетка в ZY
        };

        /// <summary>Мойка всё ещё держится на этой детали? Поперёк пласти она не
        /// отрывается — упирается в край (клампинг в AlignToPart); отпускает
        /// только вертикаль.</summary>
        private bool StillHolds(KitchenElement host) =>
            IsSuitableHost(host) &&
            _freeHeightMM >= -SNAP_RELEASE_MM && _freeHeightMM <= SNAP_CATCH_MM;

        /// <summary>Деталь годится под мойку: это базовая «Деталь» (у фасада,
        /// стола и прочих подтипов геометрия своя, врезка в неё не определена),
        /// её пласть горизонтальна и проём с запасом по краям помещается.</summary>
        public static bool IsSuitableHost(KitchenElement part)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var (up, _) = UpAxisOf(part);
            // Верхняя грань должна быть горизонтальной — столешница, а не стойка.
            float upness = Mathf.Abs((part.transform.rotation * AxisVector(up)).y);
            if (upness < 0.9f) return false;
            var (a, b) = PlaneAxes(up);
            var dims = part.DimensionsMM;
            return dims[a] >= MinPartWidthMM && dims[b] >= MinPartDepthMM;
        }

        /// <summary>Центр мойки над деталью (иначе она «прилипает» краем к
        /// соседней столешнице через всю комнату).</summary>
        private static bool IsOverFootprint(KitchenElement part, int offX, int offY)
        {
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            return Mathf.Abs(offX) <= dims[a] * 0.5f && Mathf.Abs(offY) <= dims[b] * 0.5f;
        }

        /// <summary>Столешница, которая ловит мойку прямо сейчас: мойка над её
        /// пластью не выше полосы захвата и не ниже порога отрыва, проём не
        /// налезает на боковину. Из нескольких берём ту, к пласти которой ближе.</summary>
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
                if (CutoutBlocked(el, offX, offY)) continue;

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
            // Магнит «съедает» высоту захвата: дальше вертикаль отсчитывается от
            // самой пласти, и порог отрыва не зависит от того, с какой высоты
            // мойку опустили.
            _freeHeightMM = 0f;
            return best;
        }

        /// <summary>Поза мойки в осях детали: смещения по её плоскости и высота
        /// над верхней гранью.</summary>
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
            int maxX = (dims[a] - CutoutWidthMM) / 2 - MIN_EDGE_MM;
            int maxY = (dims[b] - CutoutDepthMM) / 2 - MIN_EDGE_MM;
            offX = Mathf.Clamp(offX, -maxX, maxX);
            offY = Mathf.Clamp(offY, -maxY, maxY);
        }

        /// <summary>Проём в этом месте налезает на другую деталь — боковину,
        /// перегородку, ящик? Считаем в осях столешницы: чужой габарит переводим
        /// в её систему координат и смотрим, попадает ли он в прямоугольник проёма
        /// и в слой, который занимает чаша.</summary>
        public bool CutoutBlocked(KitchenElement part, int offX, int offY) =>
            FirstBlocker(part, offX, offY) != null;

        /// <summary>Почему мойка (не) садится на эту деталь — все проверки захвата
        /// одной строкой. Нужна и тестам, и разбору сцены руками.</summary>
        public string DescribeCatch(KitchenElement part)
        {
            if (!IsSuitableHost(part)) return "деталь не годится под мойку";
            var (offX, offY, height) = LocalPose(part);
            bool over = IsOverFootprint(part, offX, offY);
            int cx = offX, cy = offY;
            ClampOffsets(part, ref cx, ref cy);
            return $"height={height:F1}мм (полоса {-SNAP_RELEASE_MM}..{SNAP_CATCH_MM}) " +
                   $"over={over} off=({offX},{offY})→({cx},{cy}) " +
                   $"blocker={FirstBlocker(part, cx, cy) ?? "-"}";
        }

        /// <summary>Первая деталь, мешающая проёму в этом месте (или null).
        /// Отдельным методом — чтобы в диагностике было видно имя виновника,
        /// а не только факт «нельзя».</summary>
        public string? FirstBlocker(KitchenElement part, int offX, int offY)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var pt = part.transform;
            var dims = part.DimensionsMM;

            var (up, sign) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);
            float x0 = (offX - CutoutWidthMM * 0.5f) * toU, x1 = (offX + CutoutWidthMM * 0.5f) * toU;
            float y0 = (offY - CutoutDepthMM * 0.5f) * toU, y1 = (offY + CutoutDepthMM * 0.5f) * toU;
            float halfT = dims[up] * 0.5f * toU;
            float bowl = BOWL_DEPTH_MM * toU;
            // Слой чаши: от верхней грани вглубь на глубину чаши.
            float z0 = sign > 0f ? halfT - bowl : -halfT;
            float z1 = sign > 0f ? halfT : -halfT + bowl;

            var inv = Quaternion.Inverse(pt.rotation);
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || el == part || !el.BlocksCutout) continue;

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

                // Касание кромкой не мешает — блокирует только реальное наложение.
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

            // Мойка встаёт вертикалью по верхней оси детали, своей X — по её
            // первой оси плоскости. Локальная +Z мойки при этом смотрит туда,
            // куда векторное произведение, — сторону крана считаем от неё.
            Vector3 upLocal = AxisVector(up) * sign;
            Vector3 fwdLocal = Vector3.Cross(AxisVector(a), upLocal);
            Quaternion targetRot = Quaternion.LookRotation(pt.rotation * fwdLocal, pt.rotation * upLocal);

            // Клампим всегда: ресайз детали может оставить проём за её краем.
            int offX = _offsetXMM, offY = _offsetYMM;
            ClampOffsets(part, ref offX, ref offY);

            // Боковина на пути — мойка упирается в неё, как деталь в деталь.
            // Пробуем скользить вдоль препятствия: сначала откатываем одну ось,
            // потом другую, и только затем остаёмся на прежнем месте целиком.
            bool hasPrev = _lastHost == part && _lastOffsetXMM != int.MinValue;
            if (hasPrev && CutoutBlocked(part, offX, offY))
            {
                if (!CutoutBlocked(part, offX, _lastOffsetYMM))
                    offY = _lastOffsetYMM;
                else if (!CutoutBlocked(part, _lastOffsetXMM, offY))
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

            // Смеситель — с той стороны, где до края детали больше места
            // (мойку обычно сдвигают к переднему краю, кран остаётся сзади).
            float spacePlusB = dims[b] * 0.5f - _offsetYMM - OUTER_DEPTH_MM * 0.5f;
            float spaceMinusB = dims[b] * 0.5f + _offsetYMM - OUTER_DEPTH_MM * 0.5f;
            // Куда смотрит +ось b в осях самой мойки: её локальная Z — это fwdLocal.
            int plusBInSinkZ = Vector3.Dot(fwdLocal, AxisVector(b)) >= 0f ? 1 : -1;
            int faucetSign = spacePlusB >= spaceMinusB ? plusBInSinkZ : -plusBInSinkZ;
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
            foreach (var el in PartRegistry.All)
                if (el != null && el != this && el.PartName == _attachedPartName)
                    return el;
            return null;
        }

        /// <summary>Проём мойки в нормализованных координатах той плоскости, в
        /// которой его режет GrooveMesh (оси задаёт PlaneAxes). Доли считаются от
        /// ТЕКУЩИХ габаритов, поэтому ресайз детали двигает и перемасштабирует
        /// вырез автоматически.</summary>
        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            if (dims[a] <= 0 || dims[b] <= 0) return default;

            float halfW = CutoutWidthMM * 0.5f;
            float halfD = CutoutDepthMM * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (_offsetXMM - halfW) / dims[a],
                xMax = (_offsetXMM + halfW) / dims[a],
                yMin = (_offsetYMM - halfD) / dims[b],
                yMax = (_offsetYMM + halfD) / dims[b],
            };
        }

        // ── Геометрия ───────────────────────────────────────────────────

        /// <summary>Кликабельный объём — всё тело: борт плюс чаша. Он шире, чем
        /// габарит для ручек (там только плита борта), иначе по чаше в проёме
        /// нельзя было бы попасть мышью.</summary>
        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            box.size = new Vector3(OUTER_WIDTH_MM * toU, TotalHeightMM * toU, OUTER_DEPTH_MM * toU);
            box.center = new Vector3(0f, (RIM_HEIGHT_MM - TotalHeightMM) * 0.5f * toU, 0f);
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

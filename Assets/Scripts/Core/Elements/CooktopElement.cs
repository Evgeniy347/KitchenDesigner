using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Варочная поверхность: ДВА параллелепипеда.
    ///   • верхняя плита толщиной <see cref="RIM_HEIGHT_MM"/> — она и выступает
    ///     над столешницей, её габарит и есть «ширина × глубина» в свойствах;
    ///   • короб выреза — уходит в столешницу, стоит по центру плиты, а его
    ///     габарит задаётся отдельно («ширина/глубина выреза»); высота короба =
    ///     общая высота − толщина плиты.
    ///
    /// Короб режет в столешнице сквозной проём (как чаша мойки): деталь ведёт
    /// список врезной техники сама, см. <see cref="KitchenElement.RegisterCutout"/>.
    ///
    /// Живёт только на ДЕТАЛИ с горизонтальной пластью: прилипает к её верхней
    /// грани и хранит смещение от центра детали в ЛОКАЛЬНЫХ мм — поэтому деталь
    /// двигают и растягивают, а варочная едет с ней сама. Там же живёт и её
    /// СОБСТВЕННЫЙ разворот вокруг нормали детали (<see cref="YawDeg"/>): позу
    /// целиком диктует хозяин, и без отдельного поля любой поворот пользователя
    /// стирался бы следующим же кадром.
    ///
    /// Под столешницей вырез магнитится к боковинам и фасадам (край выреза
    /// заподлицо с их гранью), но НЕ упирается в них: наезд на корпусную деталь
    /// разрешён движением и подсвечивается ошибкой валидации.
    ///
    /// Геометрия строится в мировых единицах при единичном масштабе корня — как
    /// у мойки и окна, иначе дети масштабируются дважды.
    ///
    /// Тот же класс обслуживает и ФИКСИРОВАННЫЙ пресет готовой модели из группы
    /// «Техника» (<see cref="Model"/> непустая): габариты плиты и вырез берутся
    /// у производителя, правка запрещена, а на стекле появляются конфорки и
    /// панель управления. Отдельный класс дублировал бы всю врезку — привязку к
    /// детали, проём, магниты, — ради одной таблицы чисел.
    /// </summary>
    public class CooktopElement : KitchenElement, IPartCutout, IFixedSizeElement
    {

        public override string DisplayTypeName => HasFixedSize ? Model : "Варочная";
        // ── Габариты (мм) ───────────────────────────────────────────────
        public const int RIM_HEIGHT_MM = 5;          // толщина верхней плиты (над столешницей)

        // Дефолт — полноразмерная 4-конфорочная панель под модуль 60 см:
        // стекло ≈590×520, ниша врезки 560×490 (см. appliances/hob/
        // final-selection-60cm.md — этот размер у 10 моделей из 18).
        public const int DEFAULT_WIDTH_MM = 590;
        public const int DEFAULT_DEPTH_MM = 520;
        public const int DEFAULT_HEIGHT_MM = 65;     // 5 плита + 60 короб
        public const int DEFAULT_CUTOUT_WIDTH_MM = 560;
        public const int DEFAULT_CUTOUT_DEPTH_MM = 490;

        /// <summary>Борт обязан перекрыть срез столешницы — вырез уже плиты как
        /// минимум на столько с каждой стороны.</summary>
        public const int MIN_RIM_OVERLAP_MM = 5;
        public const int MIN_SIDE_MM = 100;
        public const int MAX_SIDE_MM = 2000;
        public const int MIN_CUTOUT_MM = 50;
        public const int MIN_BODY_HEIGHT_MM = 10;
        public const int MAX_HEIGHT_MM = 600;

        /// <summary>Остаток столешницы за вырезом. 20 мм — именно столько даёт
        /// штатная ниша 560 мм в столешнице модуля 600; порог строже отказал бы
        /// самой типовой врезке.</summary>
        public const int MIN_EDGE_MM = 20;

        public const int SNAP_CATCH_MM = 100;
        public const int SNAP_RELEASE_MM = 60;

        /// <summary>Магнит выреза к грани боковины/фасада под столешницей.</summary>
        public const int SNAP_PLANE_MM = 20;

        // ── Готовые модели ──────────────────────────────────────────────
        // Индукционная поверхность Bosch Serie 4 PUE611BB5E: стекло 592×522,
        // общая высота 51, ниша врезки 560×490 (docs/APPLIANCES-BRIEF.md, §1).
        public const string MODEL_BOSCH_PUE611BB5E = "Bosch PUE611BB5E";
        public const int BOSCH_WIDTH_MM = 592;
        public const int BOSCH_DEPTH_MM = 522;
        public const int BOSCH_HEIGHT_MM = 51;
        public const int BOSCH_CUTOUT_WIDTH_MM = 560;
        public const int BOSCH_CUTOUT_DEPTH_MM = 490;

        /// <summary>Габарит плиты готовой модели (мм, Ш×В×Г) или нули, если
        /// модель неизвестна — тогда варочная остаётся свободной. Новая модель
        /// добавляется одной строкой сюда и в <see cref="ModelCutoutMM"/>.</summary>
        public static Vector3Int ModelDimensionsMM(string? model) => model switch
        {
            MODEL_BOSCH_PUE611BB5E => new Vector3Int(BOSCH_WIDTH_MM, BOSCH_HEIGHT_MM, BOSCH_DEPTH_MM),
            _ => Vector3Int.zero,
        };

        /// <summary>Ниша врезки готовой модели (мм, Ш×Г).</summary>
        public static Vector2Int ModelCutoutMM(string? model) => model switch
        {
            MODEL_BOSCH_PUE611BB5E => new Vector2Int(BOSCH_CUTOUT_WIDTH_MM, BOSCH_CUTOUT_DEPTH_MM),
            _ => Vector2Int.zero,
        };

        public static bool IsKnownModel(string? model) => ModelDimensionsMM(model).x > 0;

        [SerializeField] private string _model = "";
        [SerializeField] private string _attachedPartName = "";
        [SerializeField] private int _offsetXMM;
        [SerializeField] private int _offsetYMM;
        [SerializeField] private int _cutoutWidthMM = DEFAULT_CUTOUT_WIDTH_MM;
        [SerializeField] private int _cutoutDepthMM = DEFAULT_CUTOUT_DEPTH_MM;
        [SerializeField] private float _yawDeg;

        private readonly List<GameObject> _children = new List<GameObject>();
        private KitchenElement? _lastHost;
        private int _lastOffsetXMM = int.MinValue;
        private int _lastOffsetYMM = int.MinValue;
        private int _lastCutoutWidthMM = int.MinValue;
        private int _lastCutoutDepthMM = int.MinValue;
        private float _lastYawDeg = float.MinValue;

        private float _freeHeightMM;
        private Vector3 _appliedPos;
        private bool _hasAppliedPos;
        private Quaternion _appliedRot = Quaternion.identity;
        private bool _hasAppliedRot;
        private Vector3 _lastHostPosition;

        /// <summary>Идентификатор готовой модели («Bosch PUE611BB5E») или пусто —
        /// свободная варочная с редактируемыми размерами. Задаётся фабрикой при
        /// создании и восстанавливается из сейва; пользователь его не меняет.</summary>
        [NotUndoable("модель прибора задаётся при создании; отменять нечего — габариты от неё производные")]
        public string Model
        {
            get => _model;
            set
            {
                _model = value ?? "";
                ApplyDimensions();
            }
        }

        /// <summary>Готовая модель: габариты и вырез заданы производителем.</summary>
        public bool HasFixedSize => IsKnownModel(_model);

        [NotUndoable("служебная привязка к детали, вычисляется SnapToPart")]
        public string AttachedPartName { get => _attachedPartName; set => _attachedPartName = value ?? ""; }

        [NotUndoable("смещение от центра детали — производная позиции, откатывается MoveCommand")]
        public int OffsetXMM { get => _offsetXMM; set => _offsetXMM = value; }

        [NotUndoable("см. OffsetXMM")]
        public int OffsetYMM { get => _offsetYMM; set => _offsetYMM = value; }
        public bool IsAttached => _lastHost != null;

        /// <summary>СОБСТВЕННЫЙ разворот панели вокруг нормали столешницы (°,
        /// 0..360). Без него варочная поворотов не знала бы вовсе: поза целиком
        /// диктуется деталью-хозяином (см. AlignToPart), и любой поворот
        /// пользователя стирался бы на следующем же кадре — «повернулась и
        /// вернулась». Отсчёт — как у смещений: от базиса ДЕТАЛИ, поэтому
        /// повёрнутая столешница везёт панель вместе с собой.</summary>
        [NotUndoable("производная transform.rotation: копится в TrackRotation, откатывается вместе с позой (MoveCommand/ResizeCommand)")]
        public float YawDeg
        {
            get => _yawDeg;
            set => _yawDeg = Mathf.Repeat(value, 360f);
        }

        /// <summary>Габарит ниши в осях ДЕТАЛИ с учётом <see cref="YawDeg"/>: на
        /// четверть оборота ширина и глубина меняются местами. По нему считается
        /// ВСЁ, что живёт в осях детали, — проём, магниты, кламп смещений и
        /// пригодность самой детали.
        ///
        /// Разворот округляется до БЛИЖАЙШЕЙ четверти оборота, и это не лень:
        /// решётка <see cref="GrooveMesh"/> режет только прямоугольники по осям
        /// детали, а описанный прямоугольник ниши, повёрнутой на 30°, шире её
        /// самой в полтора раза — такой проём и столешницу продырявил бы насквозь,
        /// и панель выбило бы из врезки как «не помещается». Прямой угол — и
        /// единственный представимый здесь, и единственный осмысленный: врезают
        /// прибор вдоль или поперёк столешницы.</summary>
        public (int widthMM, int depthMM) CutoutExtentsMM =>
            (Mathf.RoundToInt(_yawDeg / 90f) & 1) == 0
                ? (CutoutWidthMM, CutoutDepthMM)
                : (CutoutDepthMM, CutoutWidthMM);

        // ── Редактируемые размеры ───────────────────────────────────────
        // Ширина/глубина/высота живут в общем DimensionsMM (их правит обычное
        // окно свойств и ResizeCommand) и клампятся в ApplyDimensions — через
        // него проходит любой путь правки. Вырез живёт в своих полях и
        // подрезается по плите на ЧТЕНИИ (см. ниже).

        [NotUndoable("проекция DimensionsMM.x — откатывается вместе с габаритом")]
        public int WidthMM
        {
            get => DimensionsMM.x;
            set => DimensionsMM = new Vector3Int(value, DimensionsMM.y, DimensionsMM.z);
        }

        [NotUndoable("проекция DimensionsMM.z — откатывается вместе с габаритом")]
        public int DepthMM
        {
            get => DimensionsMM.z;
            set => DimensionsMM = new Vector3Int(DimensionsMM.x, DimensionsMM.y, value);
        }

        /// <summary>ОБЩАЯ высота: плита 5 мм + короб выреза.</summary>
        [NotUndoable("проекция DimensionsMM.y — откатывается вместе с габаритом")]
        public int HeightMM
        {
            get => DimensionsMM.y;
            set => DimensionsMM = new Vector3Int(DimensionsMM.x, value, DimensionsMM.z);
        }

        /// <summary>Высота короба, уходящего в столешницу.</summary>
        public int BodyHeightMM => Mathf.Max(MIN_BODY_HEIGHT_MM, DimensionsMM.y - RIM_HEIGHT_MM);

        // Поля хранят НАМЕРЕНИЕ пользователя, а под текущую плиту вырез
        // подрезается на чтении. Разрушающий кламп здесь стоил бы дорого:
        // AddComponent вызывает Awake на заготовке PartData (толщина 18 мм), и
        // вырез схлопывался до минимума ещё до того, как фабрика выставит
        // настоящий габарит, — обратно он бы уже не вырос.
        // Не в общем снимке: геттер отдаёт значение, ПОДРЕЗАННОЕ по текущей плите,
        // а поле хранит намерение пользователя — снимок с чтения потерял бы его.
        // Вырез целиком ведёт SetCooktopCutoutCommand (он же зовёт SnapToPart).
        [NotUndoable("своя команда SetCooktopCutoutCommand: геттер клампится по плите, снимок был бы лоссовым")]
        public int CutoutWidthMM
        {
            get => HasFixedSize
                ? ModelCutoutMM(_model).x
                : ClampCutout(_cutoutWidthMM, DimensionsMM.x);
            set
            {
                if (HasFixedSize) return;   // ниша задана производителем
                _cutoutWidthMM = Mathf.Clamp(value, MIN_CUTOUT_MM, MAX_SIDE_MM);
                ApplyDimensions();
            }
        }

        [NotUndoable("см. CutoutWidthMM — SetCooktopCutoutCommand")]
        public int CutoutDepthMM
        {
            get => HasFixedSize
                ? ModelCutoutMM(_model).y
                : ClampCutout(_cutoutDepthMM, DimensionsMM.z);
            set
            {
                if (HasFixedSize) return;
                _cutoutDepthMM = Mathf.Clamp(value, MIN_CUTOUT_MM, MAX_SIDE_MM);
                ApplyDimensions();
            }
        }

        public static int ClampSide(int mm) => Mathf.Clamp(mm, MIN_SIDE_MM, MAX_SIDE_MM);

        public static int ClampHeight(int mm) =>
            Mathf.Clamp(mm, RIM_HEIGHT_MM + MIN_BODY_HEIGHT_MM, MAX_HEIGHT_MM);

        /// <summary>Вырез не шире плиты за вычетом перекрытия борта с двух
        /// сторон — иначе срез столешницы остался бы открытым.</summary>
        public static int ClampCutout(int mm, int outerMM) =>
            Mathf.Clamp(mm, MIN_CUTOUT_MM, Mathf.Max(MIN_CUTOUT_MM, outerMM - 2 * MIN_RIM_OVERLAP_MM));

        /// <summary>Минимальные габариты детали, в которую вырез помещается с
        /// запасом по краям. Считаются по ПОВЁРНУТОМУ вырезу: у панели,
        /// развёрнутой на 90°, ширина и глубина ниши меняются местами.</summary>
        public int MinPartWidthMM => CutoutExtentsMM.widthMM + 2 * MIN_EDGE_MM;
        public int MinPartDepthMM => CutoutExtentsMM.depthMM + 2 * MIN_EDGE_MM;

        protected override Vector3 EffectiveScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            RIM_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        // Плита приподнята над плоскостью врезки — сдвиг обязан ехать вместе с
        // примеряемой позицией, иначе снэп считает панель на полплиты ниже.
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

        /// <summary>Покадровая реакция на изменение позы. Два триггера ведут в
        /// SnapToPart: собственное движение (drag, стрелки, MCP) копится в
        /// TrackDrift и пересобирает проём в столешнице, а переезд хозяина
        /// тянет варочную за ним. Один только HostMoved оставлял вырез на
        /// старом месте при перетаскивании самой панели; один только PoseVersion
        /// не догонял бы переехавшую столешницу — нужны оба.
        /// Internal: Unity зовёт Update независимо от видимости, а тесты
        /// гоняют тот же путь, что и цикл кадра.</summary>
        internal void Update()
        {
            if (PoseVersion != _lastPoseVersion)
            {
                _lastPoseVersion = PoseVersion;
                SnapToPart();
            }

            if (HostMoved)
            {
                _lastHostPosition = _lastHost!.transform.position;
                SnapToPart();
            }
        }

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;

            // Габарит готовой модели возвращается на место здесь, а не в сеттерах:
            // через ApplyDimensions проходит ЛЮБОЙ путь правки (окно свойств,
            // ResizeCommand, MCP, загрузка сейва), и одна эта строка запирает все.
            var fixedDims = ModelDimensionsMM(_model);
            if (fixedDims.x > 0) Data.DimensionsMM = fixedDims;

            var dims = Data.DimensionsMM;
            int w = ClampSide(dims.x <= 0 ? DEFAULT_WIDTH_MM : dims.x);
            int d = ClampSide(dims.z <= 0 ? DEFAULT_DEPTH_MM : dims.z);
            int h = ClampHeight(dims.y <= 0 ? DEFAULT_HEIGHT_MM : dims.y);
            Data.DimensionsMM = new Vector3Int(w, h, d);

            UpdateCollider();
            if (SuppressVisualRebuild) return;
            EnsureChildren();
            RebuildGeometry();

            // Проём в столешнице считается от размеров выреза — сменили их,
            // значит меш детали устарел.
            if (_lastHost != null &&
                (_lastCutoutWidthMM != CutoutWidthMM || _lastCutoutDepthMM != CutoutDepthMM))
            {
                _lastCutoutWidthMM = CutoutWidthMM;
                _lastCutoutDepthMM = CutoutDepthMM;
                _lastHost.RebuildGrooveMesh();
            }
        }

        // ── Привязка к детали ───────────────────────────────────────────

        public void SnapToPart()
        {
            var host = _lastHost != null ? _lastHost : FindAttachedPart();
            TrackDrift(host);
            TrackRotation(host);

            if (host != null && !StillHolds(host)) { ReleaseFrom(host); host = null; }
            if (host == null) host = FindCatchingPart();
            if (host == null) return;

            if (host.PartName != _attachedPartName || !host.HasCutout(this))
            {
                // Проверяем фактическое членство, а не только имя: после загрузки
                // сцены имя уже восстановлено из сейва, но деталь варочную ещё не
                // знает — без регистрации проём не режется.
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
            _yawDeg = YawRelativeTo(part);
            AlignToPart(part);
        }

        internal void UnregisterFromPart()
        {
            var part = FindAttachedPart();
            _attachedPartName = "";
            _lastHost = null;
            if (part != null) part.UnregisterCutout(this);
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
            _lastYawDeg = float.MinValue;
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

        /// <summary>То же, что <see cref="TrackDrift"/>, но для ПОВОРОТА: разницу
        /// между текущей позой и последней применённой раскладываем на компоненту
        /// вокруг нормали детали и копим в <see cref="YawDeg"/>. Наклон (всё, что
        /// не вокруг нормали) отбрасывается сам собой — панель лежит в пласти, и
        /// AlignToPart тут же вернёт её туда.</summary>
        private void TrackRotation(KitchenElement? host)
        {
            if (!_hasAppliedRot) { _appliedRot = transform.rotation; _hasAppliedRot = true; return; }
            Quaternion delta = transform.rotation * Quaternion.Inverse(_appliedRot);
            _appliedRot = transform.rotation;
            if (host == null || Quaternion.Angle(delta, Quaternion.identity) < 0.01f) return;

            var (up, sign) = UpAxisOf(host);
            Vector3 axis = host.transform.rotation * (AxisVector(up) * sign);
            _yawDeg = Mathf.Repeat(_yawDeg + TwistAngle(delta, axis), 360f);
        }

        /// <summary>Компонента поворота ВОКРУГ оси (твист), градусы со знаком.
        /// Разложение swing-twist: мнимая часть кватерниона проецируется на ось,
        /// остаток — наклон, он нас не касается.</summary>
        private static float TwistAngle(Quaternion delta, Vector3 axis)
        {
            axis = axis.normalized;
            Vector3 proj = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), axis);
            var twist = new Quaternion(proj.x, proj.y, proj.z, delta.w);
            float len = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
            // Поворот ровно на 180° поперёк оси — твист вырождается в ноль.
            if (len < 1e-8f) return 0f;
            len = Mathf.Sqrt(len);
            twist = new Quaternion(twist.x / len, twist.y / len, twist.z / len, twist.w / len);
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(twistAxis, axis) < 0f ? -angle : angle;
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

        public int HoleAxisIn(KitchenElement part) => UpAxisOf(part).axis;

        private bool StillHolds(KitchenElement host) =>
            IsSuitableHost(host) &&
            _freeHeightMM >= -SNAP_RELEASE_MM && _freeHeightMM <= SNAP_CATCH_MM;

        /// <summary>Деталь годится под варочную: базовая «Деталь» с горизонтальной
        /// пластью, в которую вырез помещается с запасом по краям.</summary>
        public bool IsSuitableHost(KitchenElement part)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var (up, _) = UpAxisOf(part);
            float upness = Mathf.Abs((part.transform.rotation * AxisVector(up)).y);
            if (upness < 0.9f) return false;
            var (a, b) = PlaneAxes(up);
            var dims = part.DimensionsMM;
            return dims[a] >= MinPartWidthMM && dims[b] >= MinPartDepthMM;
        }

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

                float h = Mathf.Abs(heightMM);
                if (h >= bestHeight) continue;
                bestHeight = h;
                best = el;
                bestX = offX;
                bestY = offY;
            }
            if (best == null) return null;

            // Разворот берём из ТЕКУЩЕЙ позы: панель, повёрнутую на весу, врезка
            // не должна выкручивать обратно по осям столешницы.
            _yawDeg = YawRelativeTo(best);
            ClampOffsets(best, ref bestX, ref bestY);
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

        private void ClampOffsets(KitchenElement part, ref int offX, ref int offY)
        {
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            var (cutW, cutD) = CutoutExtentsMM;
            int maxX = (dims[a] - cutW) / 2 - MIN_EDGE_MM;
            int maxY = (dims[b] - cutD) / 2 - MIN_EDGE_MM;
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

        /// <summary>Что коробу выреза действительно мешает — КОРПУСНЫЕ детали:
        /// боковины, перегородки, стойки, полки. Всё, что висит на коробе снаружи
        /// или выезжает из него (фасад, дверца, ящик, ДВП), помехой не считается —
        /// но магнитом для выреза служит (см. <see cref="IsSnapTarget"/>).</summary>
        private static bool IsObstacle(KitchenElement el)
        {
            if (el is FacadeElement || el is DoorElement || el is WindowElement) return false;
            if (el is DrawerElement || el is PanelElement) return false;
            if (el is SinkElement || el is CooktopElement || el is LightSourceElement || el is FloorElement) return false;
            return el.GetComponent<BasePlate>() == null;
        }

        /// <summary>К чему вырез прилипает под столешницей: и к корпусным деталям,
        /// и к фасадам с ящиками — по их плоскости выравнивают технику вручную.</summary>
        private static bool IsSnapTarget(KitchenElement el)
        {
            if (el is SinkElement || el is CooktopElement || el is LightSourceElement
                || el is FloorElement || el is WindowElement) return false;
            return el.GetComponent<BasePlate>() == null;
        }

        public string? FirstBlocker(KitchenElement part, int offX, int offY)
        {
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || el == part || !IsObstacle(el)) continue;
                if (BodyOverlaps(part, offX, offY, el)) return el.PartName;
            }
            return null;
        }

        /// <summary>Габарит короба выреза в осях детали (мм по её плоскости и по
        /// её «вертикали»), плюс слой, который короб занимает в толще детали.</summary>
        private (float x0, float x1, float y0, float y1, float z0, float z1) BodyBoxIn(
            KitchenElement part, int offX, int offY)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = part.DimensionsMM;
            var (up, sign) = UpAxisOf(part);

            var (cutW, cutD) = CutoutExtentsMM;
            float x0 = (offX - cutW * 0.5f) * toU, x1 = (offX + cutW * 0.5f) * toU;
            float y0 = (offY - cutD * 0.5f) * toU, y1 = (offY + cutD * 0.5f) * toU;
            float halfT = dims[up] * 0.5f * toU;
            float body = BodyHeightMM * toU;
            // Слой короба: от ВЕРХНЕЙ грани детали вглубь на высоту короба (он
            // может быть и толще детали — тогда выходит под неё).
            float z0 = sign > 0f ? halfT - body : -halfT;
            float z1 = sign > 0f ? halfT : -halfT + body;
            return (x0, x1, y0, y1, z0, z1);
        }

        /// <summary>Габарит чужой детали в осях детали-хозяина.</summary>
        private static bool LocalBounds(KitchenElement part, KitchenElement other,
            out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var verts = other.GetVertices();
            if (verts.Length == 0) return false;

            var inv = Quaternion.Inverse(part.transform.rotation);
            foreach (var w in verts)
            {
                Vector3 l = inv * (w - part.transform.position);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
            }
            return true;
        }

        private bool BodyOverlaps(KitchenElement part, int offX, int offY, KitchenElement other)
        {
            if (!LocalBounds(part, other, out var min, out var max)) return false;

            var (up, _) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);
            var box = BodyBoxIn(part, offX, offY);

            // Касание кромкой не мешает — считается только реальное наложение.
            float eps = Tolerance.EpsilonUnits;
            if (max[a] <= box.x0 + eps || min[a] >= box.x1 - eps) return false;
            if (max[b] <= box.y0 + eps || min[b] >= box.y1 - eps) return false;
            if (max[up] <= box.z0 + eps || min[up] >= box.z1 - eps) return false;
            return true;
        }

        // ── Магнит выреза к боковинам и фасадам ─────────────────────────

        /// <summary>Подтянуть смещения так, чтобы край выреза встал заподлицо с
        /// гранью боковины или фасада под столешницей. Кандидат берётся только
        /// из деталей, которые действительно лежат в слое короба и перекрывают
        /// вырез по второй оси — иначе магнитила бы любая деталь кухни.
        ///
        /// Габарит берётся из <see cref="CutoutExtentsMM"/> — того же, по которому
        /// режется проём: повёрнутая панель магнитится кромкой СВОЕГО выреза, и
        /// магнит с проёмом не расходятся.</summary>
        private void SnapToNeighbours(KitchenElement part, ref int offX, ref int offY)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var (up, _) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);
            var (cutW, cutD) = CutoutExtentsMM;
            var box = BodyBoxIn(part, offX, offY);

            int bestXOff = offX, bestYOff = offY;
            float bestXDist = SNAP_PLANE_MM + 1f, bestYDist = SNAP_PLANE_MM + 1f;

            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || el == part || !IsSnapTarget(el)) continue;
                if (!LocalBounds(part, el, out var min, out var max)) continue;

                // Деталь должна попадать в слой короба — иначе это сосед сверху
                // или снизу, к нему вырез не выравнивают.
                if (max[up] <= box.z0 || min[up] >= box.z1) continue;

                TrySnapAxis(min[a], max[a], cutW, offX,
                    min[b], max[b], box.y0, box.y1, toU, ref bestXOff, ref bestXDist);
                TrySnapAxis(min[b], max[b], cutD, offY,
                    min[a], max[a], box.x0, box.x1, toU, ref bestYOff, ref bestYDist);
            }

            if (bestXDist <= SNAP_PLANE_MM) offX = bestXOff;
            if (bestYDist <= SNAP_PLANE_MM) offY = bestYOff;
        }

        /// <summary>Кандидат «край выреза заподлицо с гранью» по одной оси.
        /// crossMin/crossMax — габарит соседа по ВТОРОЙ оси; он обязан
        /// перекрывать вырез, иначе сосед стоит в стороне и не мешает.</summary>
        private static void TrySnapAxis(float nearMin, float nearMax, int cutoutMM, int currentOff,
            float crossMin, float crossMax, float crossLo, float crossHi, float toU,
            ref int bestOff, ref float bestDist)
        {
            if (crossMax <= crossLo || crossMin >= crossHi) return;

            float half = cutoutMM * 0.5f;
            // Вырез справа от соседа (его max = левый край выреза) и слева от него.
            SnapCandidate(nearMax / toU + half, currentOff, ref bestOff, ref bestDist);
            SnapCandidate(nearMin / toU - half, currentOff, ref bestOff, ref bestDist);
        }

        private static void SnapCandidate(float candidateOff, int currentOff,
            ref int bestOff, ref float bestDist)
        {
            int rounded = Mathf.RoundToInt(candidateOff);
            float dist = Mathf.Abs(rounded - currentOff);
            if (dist >= bestDist) return;
            bestDist = dist;
            bestOff = rounded;
        }

        /// <summary>Поза панели с НУЛЕВЫМ собственным разворотом: оси детали,
        /// «вверх» — её нормаль. Разворот пользователя накручивается поверх.</summary>
        private static Quaternion BaseRotationOn(KitchenElement part)
        {
            var (up, sign) = UpAxisOf(part);
            var (a, _) = PlaneAxes(up);
            Vector3 upLocal = AxisVector(up) * sign;
            Vector3 fwdLocal = Vector3.Cross(AxisVector(a), upLocal);
            var rot = part.transform.rotation;
            return Quaternion.LookRotation(rot * fwdLocal, rot * upLocal);
        }

        /// <summary>Разворот, который панель УЖЕ имеет относительно базиса детали.
        /// Нужен при захвате новой детали: повёрнутую на столе панель врезка не
        /// должна разворачивать обратно.</summary>
        private float YawRelativeTo(KitchenElement part)
        {
            var (up, sign) = UpAxisOf(part);
            Vector3 upWorld = part.transform.rotation * (AxisVector(up) * sign);
            Quaternion delta = transform.rotation * Quaternion.Inverse(BaseRotationOn(part));
            return Mathf.Repeat(TwistAngle(delta, upWorld), 360f);
        }

        private void AlignToPart(KitchenElement part)
        {
            var pt = part.transform;
            var dims = part.DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;

            var (up, sign) = UpAxisOf(part);
            var (a, b) = PlaneAxes(up);

            Vector3 upLocal = AxisVector(up) * sign;
            Quaternion targetRot =
                Quaternion.AngleAxis(_yawDeg, pt.rotation * upLocal) * BaseRotationOn(part);

            int offX = _offsetXMM, offY = _offsetYMM;
            // Магнит ДО клампа: подтянутое к боковине смещение всё равно обязано
            // остаться в пределах детали.
            SnapToNeighbours(part, ref offX, ref offY);
            ClampOffsets(part, ref offX, ref offY);

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
            _appliedRot = transform.rotation;
            _hasAppliedRot = true;

            // Проём перестраиваем только когда он реально изменился. Поворот
            // панели меняет и его: вырез разворачивается вместе с ней.
            if (_lastHost != part || _lastOffsetXMM != _offsetXMM || _lastOffsetYMM != _offsetYMM
                || _lastCutoutWidthMM != CutoutWidthMM || _lastCutoutDepthMM != CutoutDepthMM
                || !Mathf.Approximately(_lastYawDeg, _yawDeg))
            {
                _lastHost = part;
                _lastOffsetXMM = _offsetXMM;
                _lastOffsetYMM = _offsetYMM;
                _lastCutoutWidthMM = CutoutWidthMM;
                _lastCutoutDepthMM = CutoutDepthMM;
                _lastYawDeg = _yawDeg;
                _lastHostPosition = part.transform.position;
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

        /// <summary>Проём варочной в нормализованных координатах плоскости, в
        /// которой его режет GrooveMesh. Доли — от ТЕКУЩИХ габаритов детали,
        /// поэтому её ресайз двигает и перемасштабирует вырез сам. Разворот
        /// панели проём тоже учитывает — через <see cref="CutoutExtentsMM"/>.</summary>
        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var (a, b) = PlaneAxes(UpAxisOf(part).axis);
            var dims = part.DimensionsMM;
            if (dims[a] <= 0 || dims[b] <= 0) return default;

            var (cutW, cutD) = CutoutExtentsMM;
            float halfW = cutW * 0.5f;
            float halfD = cutD * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (_offsetXMM - halfW) / dims[a],
                xMax = (_offsetXMM + halfW) / dims[a],
                yMin = (_offsetYMM - halfD) / dims[b],
                yMax = (_offsetYMM + halfD) / dims[b],
            };
        }

        // ── Геометрия короба для валидации ──────────────────────────────

        /// <summary>Центр короба выреза в мировых координатах. Плита стоит на
        /// пласти, короб уходит от неё вниз на свою высоту.</summary>
        public Vector3 BodyCenter =>
            transform.position - transform.rotation *
                new Vector3(0f, BodyHeightMM * 0.5f * AppConstants.MM_TO_UNITS, 0f);

        /// <summary>Габарит короба выреза (мировые единицы, оси — локальные).</summary>
        public Vector3 BodySize => new Vector3(
            CutoutWidthMM * AppConstants.MM_TO_UNITS,
            BodyHeightMM * AppConstants.MM_TO_UNITS,
            CutoutDepthMM * AppConstants.MM_TO_UNITS);

        // ── Геометрия ───────────────────────────────────────────────────

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            var dims = Data.DimensionsMM;
            box.size = new Vector3(dims.x * toU, dims.y * toU, dims.z * toU);
            box.center = new Vector3(0f, (RIM_HEIGHT_MM - dims.y) * 0.5f * toU, 0f);
        }

        // 0 — верхняя плита, 1 — короб выреза; дальше — рисунок готовой модели:
        // 2..5 конфорки, 6 панель управления.
        private const int BaseChildCount = 2;
        private const int BurnerCount = 4;
        private const int DecorChildCount = BurnerCount + 1;

        /// <summary>Конфорки готовой модели: диаметр (мм) и центр на стекле в
        /// ЛОКАЛЬНЫХ мм от центра плиты. «Перёд» прибора — сторона +Z локальных
        /// осей, там же панель управления.</summary>
        private static readonly (int diameterMM, int x, int z)[] BoschBurners =
        {
            (180, -145, -130),   // задняя левая
            (145,  150, -130),   // задняя правая
            (180, -145,  110),   // передняя левая
            (210,  150,  110),   // передняя правая
        };

        /// <summary>Панель управления: полоса спереди по центру (Ш × Г, мм) и
        /// отступ её центра от переднего края стекла.</summary>
        private const int PANEL_WIDTH_MM = 260;
        private const int PANEL_DEPTH_MM = 20;
        private const int PANEL_EDGE_MM = 20;

        /// <summary>Толщина накладного рисунка над стеклом — плоские шайбы,
        /// приподнятые на доли миллиметра, чтобы не мерцать с плитой.</summary>
        private const float DECOR_THICKNESS_MM = 0.6f;

        private int RequiredChildCount => HasFixedSize ? BaseChildCount + DecorChildCount : BaseChildCount;

        private static string ChildName(int idx) => idx switch
        {
            0 => "Top",
            1 => "Body",
            BaseChildCount + BurnerCount => "Panel",
            _ => "Burner" + (idx - BaseChildCount + 1),
        };

        // Примитивы Unity: цилиндр высотой 2 (localScale.y = ПОЛУвысота) и куб.
        private static PrimitiveType ChildPrimitive(int idx) =>
            idx >= BaseChildCount && idx < BaseChildCount + BurnerCount
                ? PrimitiveType.Cylinder
                : PrimitiveType.Cube;

        private void EnsureChildren()
        {
            while (_children.Count < RequiredChildCount)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(ChildPrimitive(idx));
                child.name = ChildName(idx);
                // Коллайдер у детей не нужен (клик ловит BoxCollider корня), а в
                // WebGL-сборке примитив всё равно приходит без него.
                var col = child.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                child.transform.SetParent(transform, false);
                _children.Add(child);
            }
        }

        private void RebuildGeometry()
        {
            if (_children.Count < BaseChildCount) return;
            float toU = AppConstants.MM_TO_UNITS;
            var dims = Data.DimensionsMM;

            float rimH = RIM_HEIGHT_MM * toU;
            float bodyH = BodyHeightMM * toU;

            // Плита: её верх выступает над пластью на всю толщину.
            Cube(0, new Vector3(0f, rimH * 0.5f, 0f),
                new Vector3(dims.x * toU, rimH, dims.z * toU));
            // Короб: по центру плиты, уходит вниз от пласти.
            Cube(1, new Vector3(0f, -bodyH * 0.5f, 0f),
                new Vector3(CutoutWidthMM * toU, bodyH, CutoutDepthMM * toU));

            RebuildDecor(rimH, toU, dims);
            ApplyMaterials();
            // Размер изменился — «вырез» декора надо пересчитать, иначе рисунок
            // растягивается вместо того, чтобы повторяться в своём масштабе.
            MaterialManager.RefreshTiling(this);
        }

        private void Cube(int idx, Vector3 localPos, Vector3 localScale)
        {
            var go = _children[idx];
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
        }

        /// <summary>Конфорки и панель управления готовой модели. У свободной
        /// варочной этих детей просто нет — рисунок принадлежит модели, а не
        /// произвольному прямоугольнику стекла.</summary>
        private void RebuildDecor(float rimH, float toU, Vector3Int dims)
        {
            if (_children.Count < RequiredChildCount) return;
            if (!HasFixedSize)
            {
                // Модель сняли (такое бывает только в тестах) — рисунок прячем.
                for (int i = BaseChildCount; i < _children.Count; i++)
                    if (_children[i] != null) _children[i].SetActive(false);
                return;
            }

            float lift = DECOR_THICKNESS_MM * toU;
            for (int i = 0; i < BurnerCount; i++)
            {
                var (diameterMM, x, z) = BoschBurners[i];
                var go = _children[BaseChildCount + i];
                if (go == null) continue;
                go.SetActive(true);
                go.transform.localPosition = new Vector3(x * toU, rimH + lift * 0.5f, z * toU);
                go.transform.localRotation = Quaternion.identity;
                // Цилиндр Unity высотой 2 → по Y задаём ПОЛОВИНУ толщины.
                go.transform.localScale = new Vector3(diameterMM * toU, lift * 0.5f, diameterMM * toU);
            }

            var panel = _children[BaseChildCount + BurnerCount];
            if (panel == null) return;
            panel.SetActive(true);
            float panelZ = (dims.z * 0.5f - PANEL_EDGE_MM - PANEL_DEPTH_MM * 0.5f) * toU;
            panel.transform.localPosition = new Vector3(0f, rimH + lift * 0.5f, panelZ);
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(PANEL_WIDTH_MM * toU, lift, PANEL_DEPTH_MM * toU);
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

        private static Material? _decorMat;

        /// <summary>Конфорки и панель управления — чуть светлее стекла, иначе на
        /// чёрном их не видно вовсе.</summary>
        private static Material DecorMaterial()
        {
            if (_decorMat == null)
            {
                var color = new Color(0.19f, 0.19f, 0.20f, 1f);
                _decorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _decorMat.SetColor("_BaseColor", color);
                _decorMat.color = color;
                _decorMat.SetFloat("_Metallic", 0.05f);
                _decorMat.SetFloat("_Smoothness", 0.55f);
            }
            return _decorMat!;
        }

        /// <summary>Материал обеих коробок. Источник истины — MaterialId самой
        /// варочной: выбранный декор обязан пережить любую пересборку геометрии
        /// (ресайз, загрузка проекта), иначе после перезапуска текстура молча
        /// заменялась бы штатным чёрным стеклом. Конфорки и панель управления
        /// декору не подчиняются — это рисунок прибора, а не отделка.</summary>
        public void ApplyMaterials()
        {
            if (_children.Count < BaseChildCount) return;

            Material? mat = null;
            if (MaterialManager.HasCustomDecor(this))
                mat = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            mat ??= SurfaceMaterial();

            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = i < BaseChildCount ? mat : DecorMaterial();
            }
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

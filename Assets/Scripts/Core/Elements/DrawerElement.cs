using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Ящик с выдвижным коробом. Система выдвижения (DrawerSystem):
    /// GTV AXIS PRO — покупной металлический короб (4 панели DrawerMesh, в
    /// спецификации одной строкой); Movento — деревянный короб из 5 плитных
    /// деталей (MoventoDrawerMesh), каждая из которых уходит в спецификацию
    /// отдельной позицией, но самостоятельным элементом не является (раскрой
    /// автоматический). Фасад — отдельный элемент. Габариты элемента
    /// (DimensionsMM/localScale/коллайдер) — КОНТУРНЫЙ бокс проёма корпуса
    /// LW × минПроём × NL: по нему рисуются чёрные рёбра и работает снэп.</summary>
    // IFacadeHost: фасад ящику пристёгивается по имени. Тот же интерфейс носит
    // посудомоечная машина — на нём стоят DrawerLinks.Rename, строка «Фасад» в
    // окне свойств и дерево сцены.
    public class DrawerElement : KitchenElement, IFacadeHost, IOpenable
    {

        public override string DisplayTypeName => DrawerConstants.GetDefaultName(System);

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => FindPaired() != null
            ? DrawerConstants.GetCycleButtonLabel(DoubleState)
            : (IsOpen ? OpenLabels.CloseDrawer : OpenLabels.OpenDrawer);

        public void CycleOpenState()
        {
            if (FindPaired() != null) CycleDoubleState();
            else ToggleOpen();
        }
        private const float OpenSeconds = DrawerConstants.DRAWER_ANIM_DURATION;
        private const float DrawerSlideMeters = DrawerConstants.DRAWER_SLIDE_METERS;

        private MeshFilter? _filter;
        private Mesh? _ownedMesh;

        [SerializeField] private DrawerSystem _system = DrawerSystem.Gtv;
        [SerializeField] private DrawerType _type = DrawerType.A;
        [SerializeField] private int _nominalLength = 350;
        [SerializeField] private DrawerColor _color = DrawerColor.Anthracite;
        [SerializeField] private int _internalWidth = 400;
        [SerializeField] private bool _isDouble = false;
        [SerializeField] private bool _isUpperDrawer = false;
        [SerializeField] private string _pairedDrawerName = "";
        [SerializeField] private string _attachedFacadeName = "";
        [SerializeField] private DoubleDrawerState _doubleState = DoubleDrawerState.Closed;

        private bool _open;
        private float _t;
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        /// <summary>Система выдвижения. Меняет раскрой видимого короба и способ
        /// попадания в спецификацию (GTV — строкой, Movento — деталями).</summary>
        [Undoable]
        public DrawerSystem System
        {
            get => _system;
            set
            {
                if (_system == value) return;
                _system = value;
                RebuildMesh();
                // Обе коробки двойного ящика — одна система. Ведём от нижнего (как цвет).
                if (!_isUpperDrawer)
                {
                    var pair = FindPairedDrawer();
                    if (pair != null && pair._system != value) pair.System = value;
                }
            }
        }

        [Undoable]
        public DrawerType Type
        {
            get => _type;
            set { _type = value; ApplyDimensions(); }
        }

        [Undoable]
        public int NominalLength
        {
            get => _nominalLength;
            set
            {
                if (!DrawerConstants.IsValidLength(value) || value == _nominalLength) return;
                _nominalLength = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public DrawerColor Color
        {
            get => _color;
            set
            {
                _color = value;
                MaterialManager.ApplyById(this, DrawerConstants.GetColorMaterialId(value));
                // Верхний ящик пары настроек не имеет — цвет наследует от нижнего.
                if (!_isUpperDrawer)
                {
                    var pair = FindPairedDrawer();
                    if (pair != null && pair._color != value) pair.Color = value;
                }
            }
        }

        /// <summary>LW — ширина проёма корпуса «в свету», мм. Все размеры панелей
        /// считаются от неё по формулам каталога (дно LW−75, задник LW−87).</summary>
        [Undoable]
        public int InternalWidth
        {
            get => _internalWidth;
            set
            {
                int clamped = Mathf.Max(100, value);
                if (clamped == _internalWidth) return;
                _internalWidth = clamped;
                // ApplyDimensions читает ширину из Data.DimensionsMM.x —
                // синхронизируем её ДО пересчёта, иначе вернётся старое значение.
                var dims = Data.DimensionsMM;
                dims.x = clamped;
                Data.DimensionsMM = dims;
                ApplyDimensions();
            }
        }

        /// <summary>Реальная наружная ширина короба (мм). Для GTV = LW (изделие
        /// подбирается по проёму), для Movento = LW − 42 (SKW по Blum).</summary>
        public int BoxWidth =>
            _system == DrawerSystem.Movento
                ? _internalWidth - DrawerConstants.MOVENTO_WIDTH_INSET
                : _internalWidth;

        [NotUndoable("структура пары: ставится при создании/удалении второй коробки, откатывается Create/DeleteCommand")]
        public bool IsDouble
        {
            get => _isDouble;
            set => _isDouble = value;
        }

        [NotUndoable("см. IsDouble — роль в паре, а не правка свойств")]
        public bool IsUpperDrawer
        {
            get => _isUpperDrawer;
            set => _isUpperDrawer = value;
        }

        [NotUndoable("обратная ссылка пары, ведёт DrawerLinks")]
        public string PairedDrawerName
        {
            get => _pairedDrawerName;
            set => _pairedDrawerName = value;
        }

        [NotUndoable("обратная ссылка на фасад, ведёт DrawerLinks")]
        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value;
        }

        /// <summary>Фронт ящика и фасад стянуты винтами ЗАПОДЛИЦО — монтажного
        /// зазора у этой навески нет, и проверка контакта остаётся строгой (см.
        /// <see cref="IFacadeHost.FacadeMountGapMm"/>).</summary>
        public float FacadeMountGapMm => 0f;

        /// <summary>Ящик анимирует фасад по той же кинематике, что у самого
        /// ящика (линейное выдвижение по нормали), — режим «пассажир» здесь
        /// не нужен. Снимаем пассажира со СТАРОГО фасада на случай, если он
        /// раньше висел на посудомойке, и оставляем новый фасад как был.</summary>
        public void OnAttachedFacadeChanged(FacadeElement? oldFacade, FacadeElement? newFacade)
        {
            if (oldFacade != null && oldFacade.IsPassenger) oldFacade.IsPassenger = false;
        }

        // Семантика состояний (по подписям кнопок плана):
        //   Closed     — оба закрыты
        //   BothOpen   — оба открыты
        //   LowerOnly  — верхний закрыт, открыт только нижний («Закрыть верхний» из BothOpen)
        // Поэтому верхний ящик открыт ТОЛЬКО в BothOpen, нижний — во всех состояниях, кроме Closed.
        [NotUndoable("показ анимации выдвижения, а не правка документа")]
        public DoubleDrawerState DoubleState
        {
            get => _doubleState;
            set => ApplyDoubleState(value, syncPair: true);
        }

        // Применить состояние к себе и (опционально) синхронизировать парный ящик.
        // Синхронизация идёт по PairedDrawerName: циклим один — открывается/закрывается второй.
        private void ApplyDoubleState(DoubleDrawerState value, bool syncPair)
        {
            _doubleState = value;
            bool willOpen = _isUpperDrawer
                ? (value == DoubleDrawerState.BothOpen)
                : (value != DoubleDrawerState.Closed);
            // Источник истины — закрытая поза: захватываем ДО того, как трансформ «уедет»
            // при анимации (как SetOpen/FacadeElement), иначе после перезагрузки открытый
            // ящик анимируется от Vector3.zero и «улетает».
            if (willOpen && _t <= 0f) CaptureClosed();
            _open = willOpen;
            SyncAttachedFacade();

            if (syncPair)
            {
                var paired = FindPairedDrawer();
                if (paired != null && paired._doubleState != value)
                    paired.ApplyDoubleState(value, syncPair: false);
            }
        }

        private DrawerElement? FindPairedDrawer()
        {
            using var _ = PerfMarkers.DrawerFindPaired.Auto();
            if (string.IsNullOrEmpty(_pairedDrawerName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is DrawerElement d && d != this && d.PartName == _pairedDrawerName) return d;
            return null;
        }

        /// <summary>Парный ящик (по PairedDrawerName), либо null.</summary>
        public DrawerElement? FindPaired() => FindPairedDrawer();

        /// <summary>Фасад, прикреплённый по имени (AttachedFacadeName), либо null.</summary>
        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is FacadeElement f && f.PartName == _attachedFacadeName) return f;
            return null;
        }

        // Фасад — фронт ящика: едет вместе с коробом. Анимации ящика и фасада
        // идентичны (та же длительность, ход и синус-плавность), поэтому
        // достаточно синхронизировать целевое состояние.
        private void SyncAttachedFacade()
        {
            var f = FindAttachedFacade();
            if (f != null && f.IsOpen != _open) f.SetOpen(_open);
        }

        public Vector3 ClosedPosition => (!_open && _t <= 0f) ? transform.position : _closedPos;

        public Quaternion ClosedRotation => (!_open && _t <= 0f) ? transform.rotation : _closedRot;

        // Коллизии/связность считаются по ЗАКРЫТОЙ позе (как у фасада): выдвинутый
        // ящик не должен «пересекать» свой фасад/корпус — открывание транзитно.
        protected override Vector3 ValidationPosition => ClosedPosition;
        protected override Quaternion ValidationRotation => ClosedRotation;

        // Пока ящик выдвинут, поза заморожена в _closedPos и за трансформом не
        // идёт: примерка в другую позицию его геометрию не двигает вовсе — ровно
        // так же, как раньше её не двигала запись в transform.position.
        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            PoseFollowsTransform ? transformPosition : ClosedPosition;

        /// <summary>Выдвинутый (или едущий) ящик берёт геометрию от закрытой позы,
        /// а не от трансформа, — двигать и растягивать его нельзя, пока не задвинут.</summary>
        public override bool PoseFollowsTransform => !_open && _t <= 0f;

        public float AnimProgress => _t;

        public bool IsOpen => _open;

        public bool IsAnimating => !Mathf.Approximately(_t, _open ? 1f : 0f);

        public override void ApplyDimensions()
        {
            // Внешняя запись DimensionsMM (ручки ресайза, undo, «Применить»)
            // может менять только ширину (LW); высоту и глубину всегда диктуют
            // тип и номинальная длина.
            _internalWidth = Mathf.Max(100, Data.DimensionsMM.x);

            // Габарит элемента — контурный бокс проёма (не видимого короба):
            // зазоры направляющих и монтажный подъём входят в бокс.
            int openingHeight = DrawerConstants.GetMinOpeningHeight(_type);
            Data.DimensionsMM = new Vector3Int(
                _internalWidth,
                openingHeight,
                _nominalLength
            );
            transform.localScale = new Vector3(
                _internalWidth * AppConstants.MM_TO_UNITS,
                openingHeight * AppConstants.MM_TO_UNITS,
                _nominalLength * AppConstants.MM_TO_UNITS
            );
            RebuildMesh();

            // Ширина верхнего ящика пары всегда равна ширине нижнего.
            if (!_isUpperDrawer)
            {
                var pair = FindPairedDrawer();
                if (pair != null && pair._internalWidth != _internalWidth)
                    pair.InternalWidth = _internalWidth;
            }
        }

        /// <summary>Пересобрать процедурный меш короба. Раскрой зависит от системы:
        /// GTV — 4 металлические панели, Movento — 5 плитных деталей.</summary>
        public void RebuildMesh()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_filter == null) return;

            var mesh = _system == DrawerSystem.Movento
                ? MoventoDrawerMesh.Build(_internalWidth, _type, _nominalLength)
                : DrawerMesh.Build(_internalWidth, _type, _nominalLength);
            if (_ownedMesh != null) DestroyImmediate(_ownedMesh);
            _ownedMesh = mesh;
            _filter.sharedMesh = mesh;
        }

        /// <summary>Детали короба Movento для спецификации (боковины, перед, задник,
        /// дно). Только для системы Movento; у GTV короб покупной — деталей нет.</summary>
        public IEnumerable<AssembledFacadeMesh.Part> GetSpecParts()
        {
            if (_system != DrawerSystem.Movento)
                return new AssembledFacadeMesh.Part[0];
            return MoventoDrawerMesh.ComputeParts(_internalWidth, _type, _nominalLength);
        }

        private void OnDestroy()
        {
            // Скрывает KitchenElement.OnDestroy — повторяем его обязанность.
            PartRegistry.Unregister(this);
            if (_ownedMesh != null)
            {
                DestroyImmediate(_ownedMesh);
                _ownedMesh = null;
            }
        }

        private void Update() => StepAnimation(Time.deltaTime);

        private void LateUpdate()
        {
            if (_isUpperDrawer) SyncToLower();
        }

        /// <summary>Верхний ящик пары жёстко следует за нижним: закрытая поза —
        /// вплотную над контуром нижнего, поворот совпадает. Анимация выдвижения
        /// при этом своя (ApplyAnimPose от синхронизированной закрытой позы).</summary>
        public void SyncToLower()
        {
            using var _ = PerfMarkers.DrawerSyncToLower.Auto();
            var lower = FindPairedDrawer();
            if (lower == null || lower._isUpperDrawer) return;

            float step = (DrawerConstants.GetMinOpeningHeight(lower.Type)
                        + DrawerConstants.GetMinOpeningHeight(_type)) * 0.5f * AppConstants.MM_TO_UNITS;
            _closedPos = lower.ClosedPosition + lower.ClosedRotation * Vector3.up * step;
            _closedRot = lower.ClosedRotation;
            ApplyAnimPose();
        }

        public void StepAnimation(float dt)
        {
            using var _ = PerfMarkers.DrawerStepAnimation.Auto();
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target))
            {
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);

            if (_open && _t > 0f)
            {
                var exclude = new System.Collections.Generic.List<KitchenElement> { this };
                var f = FindAttachedFacade();
                if (f != null) exclude.Add(f);
                var pair = FindPairedDrawer();
                if (pair != null)
                {
                    exclude.Add(pair);
                    var pairFacade = pair.FindAttachedFacade();
                    if (pairFacade != null) exclude.Add(pairFacade);
                }
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds, exclude);
                if (safe < _t)
                {
                    _t = Mathf.Max(_t - step, safe);
                    // Синхронизируем верхний ящик пары
                    if (!_isUpperDrawer && pair != null && pair._isUpperDrawer && pair._t > _t)
                        pair._t = _t;
                }
            }

            ApplyAnimPose();
        }

        /// <summary>Мировые границы ящика (AABB) при заданном прогрессе открывания [0..1],
        /// включая прикреплённый фасад.</summary>
        public (Vector3 min, Vector3 max) GetOpenBounds(float progress)
        {
            float eased = 0.5f * (1f - Mathf.Cos(Mathf.PI * progress));
            Vector3 pos = _closedPos + _closedRot * Vector3.forward * (DrawerSlideMeters * eased);
            Quaternion rot = _closedRot;

            var scale = transform.localScale;
            var half = scale * 0.5f;
            var localCorners = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z), new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z), new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z), new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z), new Vector3(-half.x,  half.y,  half.z),
            };
            var world = new Vector3[8];
            for (int i = 0; i < 8; i++)
                world[i] = pos + rot * localCorners[i];
            var (min, max) = OpeningCollision.MinMax(world);

            var f = FindAttachedFacade();
            if (f != null)
            {
                var (fMin, fMax) = f.GetOpenBounds(progress);
                min = Vector3.Min(min, fMin);
                max = Vector3.Max(max, fMax);
            }

            return (min, max);
        }

        public void SetOpen(bool open)
        {
            if (open && _t <= 0f) CaptureClosed();
            _open = open;
            SyncAttachedFacade();
            // Держим активный FPS, пока ящик будет анимироваться.
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ToggleOpen() => SetOpen(!_open);

        public void ForceClose()
        {
            var f = FindAttachedFacade();
            if (f != null) f.ForceClose();
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);
        }

        public void CycleDoubleState() => DoubleState = DrawerConstants.NextCycleState(_doubleState);

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        private void ApplyAnimPose()
        {
            float eased = 0.5f * (1f - Mathf.Cos(Mathf.PI * _t));
            Vector3 offset = _closedRot * Vector3.forward * (DrawerSlideMeters * eased);
            transform.SetPositionAndRotation(_closedPos + offset, _closedRot);
        }
    }
}

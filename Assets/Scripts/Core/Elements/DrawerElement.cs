using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Ящик GTV AXIS PRO. Видимый короб — 4 панели (2 боковины, дно,
    /// задник, см. DrawerMesh); фасад — отдельный элемент. Габариты элемента
    /// (DimensionsMM/localScale/коллайдер) — КОНТУРНЫЙ бокс проёма корпуса
    /// LW × минПроём × NL: по нему рисуются чёрные рёбра и работает снэп.</summary>
    public class DrawerElement : KitchenElement
    {
        private const float OpenSeconds = DrawerConstants.DRAWER_ANIM_DURATION;
        private const float DrawerSlideMeters = DrawerConstants.DRAWER_SLIDE_METERS;

        private MeshFilter? _filter;
        private Mesh? _ownedMesh;

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

        public DrawerType Type
        {
            get => _type;
            set { _type = value; ApplyDimensions(); }
        }

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

        public DrawerColor Color
        {
            get => _color;
            set
            {
                _color = value;
                MaterialManager.ApplyById(this, DrawerConstants.GetColorMaterialId(value));
            }
        }

        /// <summary>LW — ширина проёма корпуса «в свету», мм. Все размеры панелей
        /// считаются от неё по формулам каталога (дно LW−75, задник LW−87).</summary>
        public int InternalWidth
        {
            get => _internalWidth;
            set
            {
                int clamped = Mathf.Max(100, value);
                if (clamped != _internalWidth)
                {
                    _internalWidth = clamped;
                    ApplyDimensions();
                }
            }
        }

        public bool IsDouble
        {
            get => _isDouble;
            set => _isDouble = value;
        }

        public bool IsUpperDrawer
        {
            get => _isUpperDrawer;
            set => _isUpperDrawer = value;
        }

        public string PairedDrawerName
        {
            get => _pairedDrawerName;
            set => _pairedDrawerName = value;
        }

        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value;
        }

        // Семантика состояний (по подписям кнопок плана):
        //   Closed     — оба закрыты
        //   BothOpen   — оба открыты
        //   LowerOnly  — верхний закрыт, открыт только нижний («Закрыть верхний» из BothOpen)
        // Поэтому верхний ящик открыт ТОЛЬКО в BothOpen, нижний — во всех состояниях, кроме Closed.
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
            if (string.IsNullOrEmpty(_pairedDrawerName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DrawerElement d && d != this && d.PartName == _pairedDrawerName) return d;
            return null;
        }

        /// <summary>Парный ящик (по PairedDrawerName), либо null.</summary>
        public DrawerElement? FindPaired() => FindPairedDrawer();

        /// <summary>Фасад, прикреплённый по имени (AttachedFacadeName), либо null.</summary>
        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.GetAll())
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

        public float AnimProgress => _t;

        public bool IsOpen => _open;

        public bool IsAnimating => !Mathf.Approximately(_t, _open ? 1f : 0f);

        public override void ApplyDimensions()
        {
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
        }

        /// <summary>Пересобрать процедурный меш короба (боковины + дно + задник).</summary>
        public void RebuildMesh()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_filter == null) return;

            var mesh = DrawerMesh.Build(_internalWidth, _type, _nominalLength);
            if (_ownedMesh != null) DestroyImmediate(_ownedMesh);
            _ownedMesh = mesh;
            _filter.sharedMesh = mesh;
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

        public void StepAnimation(float dt)
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target))
            {
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);
            ApplyAnimPose();
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

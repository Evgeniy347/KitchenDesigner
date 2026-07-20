using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement
    {
        public int GapLeft
        {
            get => Data.GapLeft;
            set { Data.GapLeft = value; ApplyDimensions(); }
        }

        public int GapRight
        {
            get => Data.GapRight;
            set { Data.GapRight = value; ApplyDimensions(); }
        }

        public int GapTop
        {
            get => Data.GapTop;
            set { Data.GapTop = value; ApplyDimensions(); }
        }

        public int GapBottom
        {
            get => Data.GapBottom;
            set { Data.GapBottom = value; ApplyDimensions(); }
        }

        public int GapMM => Data.GapMM;

        protected override Vector3 EffectiveScale => GappedBox.EffectiveScale(transform.localScale, Data);

        public override Vector3[] GetVertices()
            => GappedBox.Vertices(transform.localScale, Data, ClosedPosition, ClosedRotation);

        public override Face[] GetFaces()
            => GappedBox.Faces(transform.localScale, Data, ClosedPosition, ClosedRotation);

        private void CornerUnits(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
            => GappedBox.CornerUnits(transform.localScale, Data,
                out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

        /// <summary>Мировые границы фасада (AABB) при заданном прогрессе открывания [0..1].</summary>
        public (Vector3 min, Vector3 max) GetOpenBounds(float progress)
        {
            var cp = IsDoorClosed ? transform.position : _closedPos;
            var cr = IsDoorClosed ? transform.rotation : _closedRot;
            var halfExtents = transform.localScale * 0.5f;

            FacadeDoor.Pose(cp, cr, halfExtents, _mode, progress, out var pos, out var rot);

            CornerUnits(out var minX, out var maxX, out var minY, out var maxY, out var minZ, out var maxZ);
            var localCorners = new Vector3[]
            {
                new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ),
                new Vector3(maxX, minY, maxZ), new Vector3(minX, minY, maxZ),
                new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ),
                new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ),
            };

            var world = new Vector3[8];
            for (int i = 0; i < 8; i++)
                world[i] = pos + rot * localCorners[i];
            return OpeningCollision.MinMax(world);
        }

        // ── Открывание (дверца) ─────────────────────────────────────────
        // Дверца поворачивается вокруг выбранного ребра на 90° и обратно, с
        // плавностью по синусу (см. FacadeDoor). Позиция/поворот трансформа
        // вычисляются из ЗАКРЫТОЙ позы каждый кадр — без накопления ошибки.
        private const float OpenSeconds = 0.4f;

        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        private bool _open;                              // целевое состояние
        private float _t;                                // прогресс 0..1 (линейный по времени)
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        /// <summary>Режим открывания (4 ребра или ящик). Смена на лету
        /// перерисовывает уже открытый фасад.</summary>
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; if (_t > 0f) ApplyDoor(); }
        }

        /// <summary>Переключить режим по кругу (для кнопки-переключателя).</summary>
        public void CycleMode() => Mode = FacadeDoor.Next(_mode);

        public bool IsOpen => _open;
        public float DoorProgress => _t;
        public bool IsDoorClosed => !_open && _t <= 0f;

        /// <summary>Логическая ЗАКРЫТАЯ поза — ИСТОЧНИК ИСТИНЫ для сохранения.
        /// Открытая/анимируемая поза вычисляется из неё каждый кадр, поэтому в
        /// проект нужно писать именно её, а не текущий (смещённый) трансформ —
        /// иначе после перезагрузки дверца «уезжает». Когда дверца полностью
        /// закрыта, трансформ и есть закрытая поза (её база ещё могла не
        /// захватиться до первого Update — берём трансформ напрямую).</summary>
        public Vector3 ClosedPosition => IsDoorClosed ? transform.position : _closedPos;
        public Quaternion ClosedRotation => IsDoorClosed ? transform.rotation : _closedRot;

        public void ToggleDoor() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            if (open && _t <= 0f) CaptureClosed();
            _open = open;
            // Держим активный FPS, пока дверь будет анимироваться.
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        /// <summary>Мгновенно вернуть закрытую позу (перед правкой размеров/позиции/поворота).</summary>
        public void ForceClose()
        {
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);

            // Если фасад прикреплён к ящику — закрываем и ящик тоже,
            // чтобы состояния никогда не расходились (не то ящик открыт, не то закрыт).
            foreach (var el in PartRegistry.GetAll())
                if (el is DrawerElement d && d.AttachedFacadeName == PartName)
                {
                    d.ForceClose();
                    break;
                }
        }

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        private void Update() => StepDoor(Time.deltaTime);

        /// <summary>Один шаг анимации. Вынесен из Update, т.к. Update не зовётся
        /// в EditMode-тестах — так поведение двери можно проверять напрямую.</summary>
        public void StepDoor(float dt)
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target))
            {
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);

            // При открытии проверяем, не упирается ли фасад в другие объекты.
            // Исключаем ящик, к которому прикреплён фасад, — иначе фасад
            // видит уже открытый ящик как препятствие и блокирует себя.
            if (_open && _t > 0f)
            {
                var exclude = new System.Collections.Generic.List<KitchenElement>();
                foreach (var el in PartRegistry.GetAll())
                {
                    if (el is DrawerElement d && d.AttachedFacadeName == PartName)
                    {
                        exclude.Add(d);
                        var pair = d.FindPaired();
                        if (pair != null) exclude.Add(pair);
                        break;
                    }
                }
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds, exclude);
                if (safe < _t) _t = Mathf.Max(_t - step, safe);
            }

            ApplyDoor();
        }

        private void ApplyDoor()
        {
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _t, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Сдвинуть закрытую позу в мировых координатах.
        /// Используется, когда ящик двигает прикреплённый фасад вместе с собой
        /// (например, верхний ящик пары следует за нижним в LateUpdate).</summary>
        internal void ShiftClosedPose(Vector3 worldDelta)
        {
            _closedPos += worldDelta;
            ApplyDoor();
        }
    }
}

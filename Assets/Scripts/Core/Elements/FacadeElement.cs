using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement, IOpenable
    {

        public override string DisplayTypeName => "Фасад";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.AlignsCutout;

        public bool IsClosedPose => IsDoorClosed;

        public IOpenable OpenTarget()
        {
            if (string.IsNullOrEmpty(PartName)) return this;
            foreach (var el in PartRegistry.All)
                if (el is IFacadeHost host && el is IOpenable openable
                    && host.AttachedFacadeName == PartName)
                    return openable;
            return this;
        }

        public string OpenActionLabel
        {
            get
            {
                var host = OpenTarget();
                return ReferenceEquals(host, this)
                    ? (IsOpen ? OpenLabels.Close : OpenLabels.Open)
                    : host.OpenActionLabel;
            }
        }

        public void CycleOpenState()
        {
            var host = OpenTarget();
            if (ReferenceEquals(host, this)) ToggleOpen();
            else host.CycleOpenState();
        }
        // Фасад ни к чему не ПРИКРЕПЛЯЕТСЯ (AttachLinks.CanBeChild): он сам —
        // корень сборки, и своя кинематика открывания у него уже есть. Зато к
        // нему прикрепляют — ради этого механика и заведена.

        /// <summary>Зазор фасада по умолчанию со всех четырёх сторон, мм.
        /// Спереди/сзади — ноль: по толщине дверца в проём не утапливается.</summary>
        public const int DEFAULT_GAP_MM = 2;

        /// <summary>Зазор от проёма — то, ради чего фасад и отличается от доски.</summary>
        public override bool SupportsGaps => true;

        // Зазоры и весь габаритный бокс живут в KitchenElement (см. GappedBox).
        // Фасаду остаётся только его особая поза: у ОТКРЫТОЙ дверцы она
        // заморожена в _closedPos и за трансформом не идёт — примерка в другую
        // позицию её геометрию не двигает вовсе. Это ровно прежнее поведение:
        // запись в transform.position открытую дверцу тоже не сдвигала.
        protected override Vector3 ValidationPosition => _isPassenger ? transform.position : ClosedPosition;

        protected override Quaternion ValidationRotation => _isPassenger ? transform.rotation : ClosedRotation;

        protected override Vector3 ValidationPositionAt(Vector3 transformPosition)
            => _isPassenger ? transformPosition : (IsDoorClosed ? transformPosition : _closedPos);

        private void CornerUnits(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
            => GappedBox.CornerUnits(transform.localScale, Data.Gaps,
                out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

        /// <summary>Мировые границы фасада (AABB) при заданном прогрессе открывания [0..1].
        /// Для пассажира трансформ уже повёрнут хостом по СВОЕЙ петле —
        /// возвращаем AABB текущего трансформа, без своей кинематики.</summary>
        public (Vector3 min, Vector3 max) GetOpenBounds(float progress)
        {
            if (_isPassenger)
            {
                var half = transform.localScale * 0.5f;
                var c = transform.position;
                var r = transform.rotation;
                var corners = new Vector3[8];
                int n = 0;
                for (int i = 0; i < 8; i++)
                {
                    corners[n++] = c + r * new Vector3(
                        (i & 1) == 0 ? -half.x : half.x,
                        (i & 2) == 0 ? -half.y : half.y,
                        (i & 4) == 0 ? -half.z : half.z);
                }
                return OpeningCollision.MinMax(corners);
            }

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
        [SerializeField] private bool _isPassenger;
        private bool _open;                              // целевое состояние
        private float _t;                                // прогресс 0..1 (линейный по времени)
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        /// <summary>Фасад пристёгнут к хозяину, который САМ двигает его трансформом
        /// (сейчас — только полновстраиваемая посудомойка, откидная дверца которой
        /// едет по своей петле, и та же петля должна везти фасад). В этом режиме
        /// собственная анимация фасада выключена: <see cref="StepDoor"/> и
        /// <see cref="ApplyDoor"/> — no-op, <see cref="SetOpen"/> только
        /// обновляет <see cref="IsOpen"/>, <see cref="ForceClose"/> — тоже.
        /// Трансформ фасада = его текущая поза, и им распоряжается хост.
        ///
        /// Атрибут <c>NotUndoable</c>: ставится/снимается хостом
        /// (<see cref="IFacadeHost.OnAttachedFacadeChanged"/>) при пристёгивании
        /// и отстёгивании, а откатывается Create/DeleteCommand самого фасада —
        /// отдельной записи в стек отмены не нужно.</summary>
        [NotUndoable("режим пассажира — ставится хостом при пристёгивании")]
        public bool IsPassenger
        {
            get => _isPassenger;
            set => _isPassenger = value;
        }

        /// <summary>Режим открывания (4 ребра или ящик). Смена на лету
        /// перерисовывает уже открытый фасад.</summary>
        [Undoable]
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

        /// <summary>Открытая (или анимируемая) дверца берёт геометрию от _closedPos,
        /// а не от трансформа, — двигать и растягивать её нельзя, пока не закрыта.
        /// Пассажир не анимируется сам: трансформ = его текущая поза.</summary>
        public override bool PoseFollowsTransform => _isPassenger || IsDoorClosed;

        /// <summary>Логическая ЗАКРЫТАЯ поза — ИСТОЧНИК ИСТИНЫ для сохранения.
        /// Открытая/анимируемая поза вычисляется из неё каждый кадр, поэтому в
        /// проект нужно писать именно её, а не текущий (смещённый) трансформ —
        /// иначе после перезагрузки дверца «уезжает». Когда дверца полностью
        /// закрыта, трансформ и есть закрытая поза (её база ещё могла не
        /// захватиться до первого Update — берём трансформ напрямую).
        ///
        /// Пассажир — особый случай: его «закрытая» поза ВСЕГДА
        /// <c>_closedPos</c> (захвачена хостом при пристёгивании через
        /// <see cref="CaptureClosedPose"/>). Брать <c>transform.position</c>,
        /// когда дверца открыта, нельзя — трансформ уже повёрнут петлёй
        /// дверцы и при закрытии вернёт фасад не туда, откуда его взяли.</summary>
        public Vector3 ClosedPosition => _isPassenger ? _closedPos : (IsDoorClosed ? transform.position : _closedPos);
        public Quaternion ClosedRotation => _isPassenger ? _closedRot : (IsDoorClosed ? transform.rotation : _closedRot);

        public void ToggleOpen() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            if (_isPassenger)
            {
                // Захватываем текущую позу ДО ApplyFacadePose: тот использует
                // _closedPos как опорную точку петли дверцы, и если бы брал
                // transform.position после ApplyFacadePose — это была бы рекурсия
                // (фасад уже сдвинут петлёй). Захват на открытии покрывает и
                // случай, когда пользователь двигал фасад ручками между
                // закрытиями: следующее открытие крутит его уже от НОВОЙ позы.
                if (open && IsDoorClosed) CaptureClosed();
                _open = open;
                return;
            }
            if (open && _t <= 0f) CaptureClosed();
            _open = open;
            // Держим активный FPS, пока дверь будет анимироваться.
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        /// <summary>Мгновенно вернуть закрытую позу (перед правкой размеров/позиции/поворота).
        /// Для пассажира — no-op: трансформом владеет хост, и он же вернёт фасад
        /// в закрытую позу, когда приведёт свою дверцу.</summary>
        public void ForceClose()
        {
            if (_isPassenger) return;
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);

            // Если фасад прикреплён к ящику — закрываем и ящик тоже,
            // чтобы состояния никогда не расходились (не то ящик открыт, не то закрыт).
            foreach (var el in PartRegistry.All)
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

        /// <summary>Захватить текущую мировую позу как «закрытую» — хост
        /// (посудомойка) зовёт при пристёгивании, чтобы петля дверцы крутила
        /// фасад вокруг той точки, в которой пользователь его поставил, а не
        /// вокруг мирового нуля.</summary>
        internal void CaptureClosedPose() => CaptureClosed();

        private void Update() => StepDoor(Time.deltaTime);

        /// <summary>Один шаг анимации. Вынесен из Update, т.к. Update не зовётся
        /// в EditMode-тестах — так поведение двери можно проверять напрямую.</summary>
        public void StepDoor(float dt)
        {
            if (_isPassenger) return;
            using var _ = PerfMarkers.FacadeStepDoor.Auto();
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
                foreach (var el in PartRegistry.All)
                {
                    if (el is DrawerElement d && d.AttachedFacadeName == PartName)
                    {
                        exclude.Add(d);
                        var pair = d.FindPaired();
                        if (pair != null) exclude.Add(pair);
                        break;
                    }
                }
                // Прикреплённые детали (нестандартный ящик: дно, стенки,
                // боковины) едут вместе с фасадом — препятствием они быть не
                // могут. Иначе фасад упирался бы в собственный короб и
                // останавливал анимацию на первом же миллиметре.
                exclude.AddRange(AttachLinks.Descendants(this));
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds, exclude);
                if (safe < _t) _t = Mathf.Max(_t - step, safe);
            }

            ApplyDoor();
        }

        private void ApplyDoor()
        {
            if (_isPassenger) return;
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

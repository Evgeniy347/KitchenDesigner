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

        protected override Vector3 EffectiveScale
        {
            get
            {
                var physical = transform.localScale;
                var gapX = (Data.GapLeft + Data.GapRight) * AppConstants.MM_TO_UNITS;
                var gapY = (Data.GapTop + Data.GapBottom) * AppConstants.MM_TO_UNITS;
                return physical + new Vector3(gapX, gapY, 0);
            }
        }

        public override Vector3[] GetVertices()
        {
            CornerUnits(out var minX, out var maxX, out var minY, out var maxY, out var minZ, out var maxZ);
            var localCorners = new Vector3[]
            {
                new Vector3(minX, minY, minZ),
                new Vector3(maxX, minY, minZ),
                new Vector3(maxX, minY, maxZ),
                new Vector3(minX, minY, maxZ),
                new Vector3(minX, maxY, minZ),
                new Vector3(maxX, maxY, minZ),
                new Vector3(maxX, maxY, maxZ),
                new Vector3(minX, maxY, maxZ),
            };
            var pos = transform.position;
            var rot = transform.rotation;
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * localCorners[i];
            return result;
        }

        public override Face[] GetFaces()
        {
            CornerUnits(out var minX, out var maxX, out var minY, out var maxY, out var minZ, out var maxZ);
            var pos = transform.position;
            var rot = transform.rotation;
            var axes = new Vector3[]
            {
                rot * Vector3.right,
                rot * Vector3.up,
                rot * Vector3.forward
            };
            float w = maxX - minX;
            float h = maxY - minY;
            float d = maxZ - minZ;
            var localCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            // Из-за асимметричных зазоров AABB может быть НЕ центрирован вокруг transform.position.
            // Сдвигаем все центры граней на это смещение.
            var centerShift = rot * localCenter;
            var faceDims = new Vector2[]
            {
                new Vector2(h, d), new Vector2(w, d), new Vector2(w, h),
            };
            var half = new Vector3[]
            {
                new Vector3(w * 0.5f, 0, 0), new Vector3(0, h * 0.5f, 0), new Vector3(0, 0, d * 0.5f),
            };
            var offsets = new Vector3[]
            {
                 axes[0] * half[0].x, -axes[0] * half[0].x,
                 axes[1] * half[1].y, -axes[1] * half[1].y,
                 axes[2] * half[2].z, -axes[2] * half[2].z,
            };
            var normals = new Vector3[]
            {
                 axes[0], -axes[0],
                 axes[1], -axes[1],
                 axes[2], -axes[2],
            };
            var rightAxis = new Vector3[]
            {
                axes[1], axes[1], axes[0], axes[0], axes[0], axes[0],
            };
            var upAxis = new Vector3[]
            {
                axes[2], axes[2], axes[2], axes[2], axes[1], axes[1],
            };
            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
            {
                int dimIdx = i / 2;
                faces[i] = new Face(
                    pos + centerShift + offsets[i],
                    normals[i],
                    faceDims[dimIdx],
                    rightAxis[i],
                    upAxis[i]
                );
            }
            return faces;
        }

        private void CornerUnits(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            var phys = transform.localScale;
            float gl = Data.GapLeft  * AppConstants.MM_TO_UNITS;
            float gr = Data.GapRight * AppConstants.MM_TO_UNITS;
            float gt = Data.GapTop    * AppConstants.MM_TO_UNITS;
            float gb = Data.GapBottom * AppConstants.MM_TO_UNITS;
            minX = -phys.x * 0.5f - gl;
            maxX =  phys.x * 0.5f + gr;
            minY = -phys.y * 0.5f - gb;
            maxY =  phys.y * 0.5f + gt;
            minZ = -phys.z * 0.5f;
            maxZ =  phys.z * 0.5f;
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
                // Закрыта и в покое → база следует за реальным трансформом
                // (чтобы перетаскивание/поворот закрытой дверцы обновляли базу).
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);
            ApplyDoor();
        }

        private void ApplyDoor()
        {
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _t, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}

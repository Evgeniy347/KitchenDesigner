using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Размещение только что созданного объекта: он «висит» на курсоре, как при
    /// зажатой ЛКМ (хотя кнопка не нажата — пользователь лишь выбрал объект в
    /// сайдбаре). ЛКМ по сцене — поставить; ПКМ — отменить (объект исчезает).
    /// Выбор другого объекта в сайдбаре или клик по любой кнопке тулбара тоже
    /// отменяют размещение: выбор нового объекта вызывает <see cref="Begin"/>
    /// (который убирает предыдущий), а клик по UI ловится как «ЛКМ над UI» → отмена.
    /// Пока идёт размещение, <see cref="IsActive"/> глушит выделение, drag и
    /// мышь камеры.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        public static PlacementController? Instance { get; private set; }

        /// <summary>Идёт размещение — обработчики клика/drag/камеры должны молчать.</summary>
        public static bool IsActive => Instance != null && Instance._pending != null;

        private KitchenElement? _pending;
        private GameObject? _pendingGo;

        private void Awake() => Instance = this;

        /// <summary>Начать размещение нового объекта: он уже создан и
        /// зарегистрирован, но ещё НЕ занесён в стек отмены (заносится при
        /// установке). Предыдущий незавершённый объект убираем.</summary>
        public void Begin(KitchenElement element)
        {
            if (element == null) return;
            Cancel();
            _pending = element;
            _pendingGo = element.gameObject;
            SelectionManager.Instance?.DeselectAll();
            MoveToCursor();
        }

        private void Update()
        {
            if (_pending == null) return;

            // Объект убрали извне (напр. очистка сцены) — сбрасываем состояние.
            if (_pendingGo == null || !_pendingGo.activeInHierarchy)
            {
                _pending = null;
                _pendingGo = null;
                return;
            }

            MoveToCursor();

            if (Input.GetMouseButtonDown(1)) { Cancel(); return; } // ПКМ — отмена

            if (Input.GetMouseButtonDown(0))
            {
                // Клик по кнопке тулбара/сайдбара — объект не устанавливается.
                if (PointerOverUI) { Cancel(); return; }
                Commit();
            }
        }

        private void MoveToCursor()
        {
            var pending = _pending;
            var cam = Camera.main;
            if (cam == null || pending == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector3 point = ground.Raycast(ray, out float enter)
                ? ray.GetPoint(enter)
                : cam.transform.position + cam.transform.forward * 2f;

            // Свет подвешен под потолком, мойка садится на столешницу сама
            // (SnapToPart) — им держим текущую высоту; остальное ставим на пол
            // (центр по высоте = половина габарита).
            point.y = pending is LightSourceElement || pending is SinkElement || pending is CooktopElement
                ? pending.transform.position.y
                : pending.DimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS;

            Vector3 pos = GridManager.SnapToGridXZ(point);

            // Притягиваем к соседям, как при обычном перетаскивании.
            var others = PartRegistry.GetAll();
            others.Remove(pending);
            var snap = SnapSystem.TrySnap(pending, others, pos);
            pending.transform.position = WorldBounds.Clamp(snap.snapped ? snap.position : pos);

            if (pending is CooktopElement cooktop) cooktop.SnapToPart();
            if (pending is SinkElement sink) sink.SnapToPart();

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(pending);
        }

        private void Commit()
        {
            var go = _pendingGo;
            var element = _pending;
            _pending = null;
            _pendingGo = null;
            if (go == null || element == null) return;

            // Теперь объект попадает в стек отмены (создание можно откатить).
            CommandStack.Execute(new CreateCommand(go));
            SelectionManager.Instance?.Select(element);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        /// <summary>Отмена: объект не устанавливается — убираем со сцены.
        /// Как <see cref="DeleteCommand"/>: деактивируем и снимаем с учёта (без
        /// пула — незакоммиченный объект в стек отмены не попадал). Пул деталей
        /// не трогаем: вернуть в него пол/свет/окно значило бы «протечь» чужой
        /// тип в базовые детали.</summary>
        public void Cancel()
        {
            var go = _pendingGo;
            _pending = null;
            _pendingGo = null;
            if (go == null) return;

            var el = go.GetComponent<KitchenElement>();
            // Окно/дверь врезаны в стену списком — снимаем вырез до деактивации
            // (OnDestroy при SetActive(false) не вызывается).
            if (el is WindowElement win) win.UnregisterFromWall();
            if (el is DoorElement door) door.UnregisterFromWall();
            // Мойка врезана в деталь тем же списком — иначе в столешнице
            // осталась бы дыра от неустановленной мойки.
            if (el is SinkElement sink) sink.UnregisterFromPart();
            if (el is CooktopElement cooktop) cooktop.UnregisterFromPart();

            go.SetActive(false);
            if (el != null) PartRegistry.Unregister(el);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}

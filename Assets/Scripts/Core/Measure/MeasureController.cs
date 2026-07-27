using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    /// <summary>Ввод режима «Рулетка»: подсветка ближайшей вершины, фиксация
    /// концов замера, предпросмотр отрезка и выбор уже поставленных замеров.
    /// Рисованием занимается <see cref="MeasureRenderer"/>, подписями —
    /// MeasureLabelsUI; здесь только состояние и мышь.
    /// DefaultExecutionOrder=-50 — раньше SelectionManager/ElementMover, чтобы
    /// состояние текущего кадра было готово до их проверок режима.</summary>
    [DefaultExecutionOrder(-50)]
    public class MeasureController : MonoBehaviour
    {
        public static MeasureController? Instance { get; private set; }

        /// <summary>Радиус «помощи попадания» по вершине, пиксели.</summary>
        public const float VertexPickRadiusPx = 18f;
        /// <summary>Радиус «помощи попадания» по отрезку, пиксели.</summary>
        public const float SegmentPickRadiusPx = 10f;

        /// <summary>Вершина под курсором (розовая точка) — null, если далеко.</summary>
        public Vector3? Hint { get; private set; }
        /// <summary>Зафиксированный первый конец замера (красная точка).</summary>
        public Vector3? Anchor { get; private set; }
        /// <summary>Второй конец предпросмотра: либо предложенная вершина, либо
        /// проекция курсора на одну ось.</summary>
        public Vector3? PreviewEnd { get; private set; }
        /// <summary>Отрезок под курсором (светло-жёлтая подсветка).</summary>
        public MeasureSegment? Hovered { get; private set; }

        /// <summary>Идёт предпросмотр (есть якорь и второй конец).</summary>
        public bool HasPreview => Anchor.HasValue && PreviewEnd.HasValue;

        // Кандидаты пересобираются каждый кадр; списки переиспользуем, иначе на
        // сцене в сотни деталей это 8 Vector3 на элемент в мусор каждый кадр.
        private readonly List<Vector3> _worldVerts = new List<Vector3>();
        private readonly List<Vector2> _screenVerts = new List<Vector2>();

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!MeasureMode.Active)
            {
                ResetState();
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            HandleEscape();

            Vector2 mouse = Input.mousePosition;
            CollectVertices(cam);
            UpdateHint(mouse);
            UpdatePreview(cam, mouse);
            UpdateHover(cam, mouse);

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                HandleClick();
        }

        // Esc отменяет ровно одно: сперва незавершённый замер, затем выбор
        // отрезка (закрывает окно свойств), и только потом — весь режим.
        private void HandleEscape()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (Anchor.HasValue) Anchor = null;
            else if (MeasureStore.Selected != null) MeasureStore.Select(null);
            else MeasureMode.SetActive(false);
        }

        // Вершины всех видимых деталей и стен. Источник — GetVertices(): он уже
        // учитывает поворот, габарит составных элементов и полную высоту
        // опущенной стены. Опорная плита исключена — это техническая подложка.
        private void CollectVertices(Camera cam)
        {
            _worldVerts.Clear();
            _screenVerts.Clear();

            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                if (!SceneVisibility.AnyRendererEnabled(e)) continue;

                var verts = e.GetVertices();
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 screen = cam.WorldToScreenPoint(verts[v]);
                    if (screen.z <= 0f) continue; // за камерой
                    _worldVerts.Add(verts[v]);
                    _screenVerts.Add(new Vector2(screen.x, screen.y));
                }
            }
        }

        private void UpdateHint(Vector2 mouse)
        {
            int idx = MeasureGeometry.NearestIndex(_screenVerts, mouse, VertexPickRadiusPx);
            Hint = idx >= 0 ? _worldVerts[idx] : (Vector3?)null;

            // Собственный якорь предлагать вторым концом бессмысленно.
            if (Hint.HasValue && Anchor.HasValue &&
                (Hint.Value - Anchor.Value).sqrMagnitude < Tolerance.EpsilonSqr)
                Hint = null;
        }

        // Есть предложенная вершина — отрезок идёт к ней НАПРЯМУЮ (в том числе
        // по диагонали). Курсор увели — предложение пропадает, и отрезок снова
        // строится строго по одной оси.
        private void UpdatePreview(Camera cam, Vector2 mouse)
        {
            if (!Anchor.HasValue)
            {
                PreviewEnd = null;
                return;
            }

            if (Hint.HasValue)
            {
                PreviewEnd = Hint;
                return;
            }

            PreviewEnd = FreeEnd(cam, mouse, Anchor.Value);
        }

        // Свободный конец: курсор проецируется на плоскость через якорь,
        // перпендикулярную взгляду камеры (замер должен работать и в воздухе,
        // а не только там, где есть коллайдер), затем прижимается к одной оси.
        private static Vector3? FreeEnd(Camera cam, Vector2 mouse, Vector3 anchor)
        {
            var plane = new Plane(cam.transform.forward, anchor);
            Ray ray = cam.ScreenPointToRay(mouse);
            if (!plane.Raycast(ray, out float enter)) return null;
            return MeasureGeometry.ProjectOnDominantAxis(anchor, ray.GetPoint(enter));
        }

        // Пока курсор у вершины, выбор отрезка не предлагаем: постановка точки
        // важнее, иначе замер вдоль детали невозможно было бы начать.
        private void UpdateHover(Camera cam, Vector2 mouse)
        {
            Hovered = null;
            if (Hint.HasValue || Anchor.HasValue) return;

            float best = SegmentPickRadiusPx;
            foreach (var seg in MeasureStore.Segments)
            {
                Vector3 sa = cam.WorldToScreenPoint(seg.A);
                Vector3 sb = cam.WorldToScreenPoint(seg.B);
                if (sa.z <= 0f || sb.z <= 0f) continue;

                float d = MeasureGeometry.DistancePointToSegmentPx(
                    new Vector2(sa.x, sa.y), new Vector2(sb.x, sb.y), mouse);
                if (d <= best)
                {
                    best = d;
                    Hovered = seg;
                }
            }
        }

        private void HandleClick()
        {
            if (Anchor.HasValue)
            {
                // Второй конец ставится только на вершину; клик мимо — отмена.
                if (Hint.HasValue) MeasureStore.Add(new MeasureSegment(Anchor.Value, Hint.Value));
                Anchor = null;
                PreviewEnd = null;
                return;
            }

            if (Hint.HasValue)
            {
                Anchor = Hint;
                MeasureStore.Select(null);
                return;
            }

            MeasureStore.Select(Hovered); // null — клик в пустоту снимает выбор
        }

        private void ResetState()
        {
            Hint = null;
            Anchor = null;
            PreviewEnd = null;
            Hovered = null;
        }

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}

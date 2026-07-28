using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Маркер ручки области накладки: к какому краю относится.
    /// 0 = −U (лево), 1 = +U (право), 2 = −V (низ), 3 = +V (верх).</summary>
    public class TextureOverlayHandle : MonoBehaviour
    {
        public int edge;
    }

    /// <summary>Ручки области накладки текстуры прямо на поверхности.
    ///
    /// Включаются карандашом в строке накладки и работают в плоскости грани:
    /// • «Ручки: растяжение» — четыре ручки на серединах сторон области, тяга
    ///   двигает одну границу;
    /// • «Ручки: перенос» — те же четыре точки, но тяга везёт область целиком
    ///   вдоль соответствующей оси.
    /// Режим берётся из общего <see cref="ResizeHandleManager.Mode"/> — той самой
    /// кнопки тулбара; своего переключателя тут нет, чтобы у пользователя не было
    /// двух разных «режимов ручек» одновременно.
    ///
    /// Растяжение меняет ОБЛАСТЬ ПОКАЗА, а не картинку: UV накладки привязаны к
    /// началу грани (см. PlaneWithHolesMesh), поэтому рисунок стоит на месте, а
    /// область ползает по нему как окно.
    ///
    /// Итог перетаскивания — ОДНА команда на весь drag: промежуточные кадры в
    /// undo-стеке не нужны (правило 2 UI-GUIDELINES).</summary>
    [DefaultExecutionOrder(100)]
    public class TextureOverlayHandles : MonoBehaviour
    {
        /// <summary>Вынос кубика ручки от поверхности и его ребро, юниты.</summary>
        private const float StemLen = 0.07f;
        private const float TipSize = 0.045f;

        /// <summary>Габарит зоны захвата (её ось Z — нормаль грани). Зона заведомо
        /// крупнее кубика и НЕ уходит внутрь объекта: утопленный коллайдер ловил
        /// луч наравне со стеной, и попасть по ручке получалось через раз.</summary>
        private const float GrabWidth = 0.11f;
        private const float GrabDepth = 0.13f;

        private static KitchenElement? _element;
        private static int _index = -1;

        public static bool Active => _element != null && _index >= 0;

        public static bool IsEditing(KitchenElement element, int index) =>
            Active && _element == element && _index == index;

        /// <summary>Включить/выключить ручки для этой накладки. Повторный клик по
        /// карандашу той же строки — выключение.</summary>
        public static void Toggle(KitchenElement element, int index)
        {
            if (IsEditing(element, index)) { End(); return; }
            Begin(element, index);
        }

        public static void Begin(KitchenElement element, int index)
        {
            End();
            if (element == null || index < 0 || index >= element.TextureOverlays.Count) return;

            // «(все)» — это шесть граней сразу, и общей плоскости у них нет:
            // тянуть область не за что. Сторону надо сперва выбрать конкретную.
            if (element.TextureOverlays[index].side == OverlaySide.All)
            {
                UI.ToastNotification.Instance?.Show("Область правится только у одной стороны");
                return;
            }

            _element = element;
            _index = index;
        }

        public static void End()
        {
            _element = null;
            _index = -1;
            _instance?.ClearHandles();
        }

        /// <summary>Курсор над ручкой области — выделение, перетаскивание объекта
        /// и панорама камеры должны молчать, как и над ручками ресайза.</summary>
        public static bool PointerOverHandle() => PickHandle() != null;

        /// <summary>Ручка под курсором, или null.
        ///
        /// Именно RaycastAll, а не Raycast: ручка стоит ВПЛОТНУЮ к поверхности, и
        /// одиночный луч сплошь и рядом возвращал сначала саму стену — особенно у
        /// накладки во всю грань, где ручки сидят на самом краю. Ближайший ко всему
        /// прочему объект нас не интересует: если луч задел ручку, значит по ручке и
        /// кликнули.</summary>
        private static TextureOverlayHandle? PickHandle()
        {
            if (!Active) return null;
            var cam = Camera.main;
            if (cam == null) return null;

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition));
            TextureOverlayHandle? best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                var handle = hit.collider.GetComponentInParent<TextureOverlayHandle>();
                if (handle == null || hit.distance >= bestDist) continue;
                best = handle;
                bestDist = hit.distance;
            }
            return best;
        }

        // ── Экземпляр ───────────────────────────────────────────────────

        private static TextureOverlayHandles? _instance;

        private readonly List<TextureOverlayHandle> _handles = new List<TextureOverlayHandle>();
        private Material? _material;
        private ResizeHandleManager.HandleMode _builtMode;

        private bool _dragging;
        private int _dragEdge;
        private RectInt _rectBefore;
        private List<TextureOverlaySpec> _before = new List<TextureOverlaySpec>();
        private float _grabU, _grabV;         // точка захвата в координатах грани, мм
        private KitchenElement.Face _face;
        private Vector2Int _faceMM;

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            ClearHandles();
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (!Active || !StillValid())
            {
                if (_handles.Count > 0) { _dragging = false; ClearHandles(); }
                return;
            }

            if (!_dragging && (_handles.Count == 0 || _builtMode != ResizeHandleManager.Mode))
            {
                ClearHandles();
                BuildHandles();
            }
            if (!_dragging) PositionHandles();
        }

        /// <summary>Элемент жив, накладка на месте и сторона всё ещё одна.
        /// Список могли поменять undo, MCP или соседняя строка меню.</summary>
        private bool StillValid()
        {
            var el = _element;
            if (el == null || !el.gameObject.activeInHierarchy) return false;
            if (_index < 0 || _index >= el.TextureOverlays.Count) return false;
            return el.TextureOverlays[_index].side != OverlaySide.All;
        }

        private void Update()
        {
            if (!Active) { if (_dragging) FinishDrag(); return; }

            if (_dragging)
            {
                if (Input.GetMouseButton(0)) UpdateDrag();
                if (Input.GetMouseButtonUp(0)) FinishDrag();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (PointerOverUI()) return;
            var handle = PickHandle();
            if (handle != null) BeginDrag(handle.edge);
        }

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        // ── Перетаскивание ──────────────────────────────────────────────

        private void BeginDrag(int edge)
        {
            var el = _element;
            if (el == null || !StillValid()) return;

            CaptureFace(el);
            if (!PointOnFace(out float u, out float v)) return;

            _dragEdge = edge;
            _grabU = u;
            _grabV = v;
            // Область фиксируем в явных миллиметрах: накладка «во всю грань»
            // при первом же перетаскивании превращается в конкретный прямоугольник.
            _rectBefore = el.TextureOverlays[_index].Resolve(_faceMM);
            _before = new List<TextureOverlaySpec>(el.TextureOverlays);
            _dragging = true;
        }

        private void UpdateDrag()
        {
            var el = _element;
            if (el == null || !StillValid()) { FinishDrag(); return; }
            if (!PointOnFace(out float u, out float v)) return;

            var rect = ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Move
                ? MoveRect(_rectBefore, _dragEdge, u - _grabU, v - _grabV, _faceMM)
                : StretchRect(_rectBefore, _dragEdge, u, v, _faceMM);

            ApplyRect(el, rect);
        }

        private void FinishDrag()
        {
            _dragging = false;
            var el = _element;
            if (el == null || _index < 0 || _index >= el.TextureOverlays.Count) return;

            var after = new List<TextureOverlaySpec>(el.TextureOverlays);
            if (SameList(_before, after)) return;

            // Возвращаем состояние до драга и проводим итог одной командой —
            // так в undo-стеке лежит один шаг «область накладки», а не сотня.
            el.SetTextureOverlays(_before);
            CommandStack.Execute(new SetTextureOverlaysCommand(el, _before, after));
            PositionHandles();
        }

        private static bool SameList(List<TextureOverlaySpec> a, List<TextureOverlaySpec> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        /// <summary>Промежуточное состояние драга: пишем напрямую, без команды —
        /// в undo-стек попадёт только итог (см. FinishDrag).</summary>
        private void ApplyRect(KitchenElement el, RectInt rect)
        {
            var after = new List<TextureOverlaySpec>(el.TextureOverlays);
            after[_index] = after[_index].WithRect(rect);
            el.SetTextureOverlays(after);
            PositionHandles();
        }

        /// <summary>Тяга одной границы. Противоположная стоит на месте, минимальный
        /// размер и границы грани держатся жёстко — область не может вывернуться
        /// наизнанку или уехать со стены.</summary>
        public static RectInt StretchRect(RectInt rect, int edge, float u, float v, Vector2Int faceMM)
        {
            int x0 = rect.xMin, x1 = rect.xMax, y0 = rect.yMin, y1 = rect.yMax;
            int min = TextureOverlaySpec.MIN_SIZE_MM;
            switch (edge)
            {
                case 0: x0 = Mathf.Clamp(Round(u), 0, x1 - min); break;
                case 1: x1 = Mathf.Clamp(Round(u), x0 + min, faceMM.x); break;
                case 2: y0 = Mathf.Clamp(Round(v), 0, y1 - min); break;
                default: y1 = Mathf.Clamp(Round(v), y0 + min, faceMM.y); break;
            }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>Перенос области вдоль ОДНОЙ оси (какой — задаёт схваченная
        /// ручка), с упором в края грани. Размер при этом не меняется: область,
        /// упёршаяся в край, просто останавливается.</summary>
        public static RectInt MoveRect(RectInt rect, int edge, float du, float dv, Vector2Int faceMM)
        {
            bool alongU = edge <= 1;
            int dx = alongU ? Round(du) : 0;
            int dy = alongU ? 0 : Round(dv);
            int x0 = Mathf.Clamp(rect.xMin + dx, 0, Mathf.Max(0, faceMM.x - rect.width));
            int y0 = Mathf.Clamp(rect.yMin + dy, 0, Mathf.Max(0, faceMM.y - rect.height));
            return new RectInt(x0, y0, rect.width, rect.height);
        }

        private static int Round(float mm) => Mathf.RoundToInt(mm);

        // ── Геометрия грани ─────────────────────────────────────────────

        private void CaptureFace(KitchenElement el)
        {
            int faceIndex = (int)el.TextureOverlays[_index].side;
            var faces = el.GetFaces();
            _face = faces[Mathf.Clamp(faceIndex, 0, faces.Length - 1)];
            _faceMM = TextureOverlayGeometry.FaceSizeMM(el.DimensionsMM, faceIndex);
        }

        /// <summary>Точка под курсором в координатах грани (мм от её левого нижнего
        /// угла). false — луч мыши идёт вдоль плоскости и точки пересечения нет.</summary>
        private bool PointOnFace(out float u, out float v)
        {
            u = v = 0f;
            var cam = Camera.main;
            if (cam == null) return false;

            var plane = new Plane(_face.normal, _face.center);
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float dist)) return false;

            var delta = ray.GetPoint(dist) - _face.center;
            u = Vector3.Dot(delta, _face.rightAxis) / AppConstants.MM_TO_UNITS + _faceMM.x * 0.5f;
            v = Vector3.Dot(delta, _face.upAxis) / AppConstants.MM_TO_UNITS + _faceMM.y * 0.5f;
            return true;
        }

        /// <summary>Мировая точка середины стороны области.</summary>
        private Vector3 EdgeCenter(RectInt rect, int edge)
        {
            float u = edge switch
            {
                0 => rect.xMin,
                1 => rect.xMax,
                _ => (rect.xMin + rect.xMax) * 0.5f,
            };
            float v = edge switch
            {
                2 => rect.yMin,
                3 => rect.yMax,
                _ => (rect.yMin + rect.yMax) * 0.5f,
            };
            return _face.center
                + _face.rightAxis * ((u - _faceMM.x * 0.5f) * AppConstants.MM_TO_UNITS)
                + _face.upAxis * ((v - _faceMM.y * 0.5f) * AppConstants.MM_TO_UNITS);
        }

        // ── Ручки ───────────────────────────────────────────────────────

        private void BuildHandles()
        {
            _builtMode = ResizeHandleManager.Mode;
            for (int edge = 0; edge < 4; edge++)
            {
                var go = new GameObject($"TextureOverlayHandle_{edge}") { hideFlags = HideFlags.DontSave };
                var marker = go.AddComponent<TextureOverlayHandle>();
                marker.edge = edge;

                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0, GrabDepth * 0.5f);
                col.size = new Vector3(GrabWidth, GrabWidth, GrabDepth);

                var tip = new GameObject("Tip");
                tip.transform.SetParent(go.transform, false);
                tip.transform.localPosition = new Vector3(0, 0, StemLen);
                tip.transform.localScale = Vector3.one * TipSize;
                tip.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
                tip.AddComponent<MeshRenderer>().sharedMaterial = HandleMaterial();

                _handles.Add(marker);
            }
        }

        private void PositionHandles()
        {
            var el = _element;
            if (el == null || !StillValid() || _handles.Count == 0) return;

            CaptureFace(el);
            var rect = el.TextureOverlays[_index].Resolve(_faceMM);

            // Ручка стоит НАД поверхностью вдоль нормали грани: иначе она тонет
            // в стене и её нечем схватить.
            var n = _face.normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? _face.normal.normalized : Vector3.forward;
            foreach (var h in _handles)
            {
                if (h == null) continue;
                h.transform.SetPositionAndRotation(EdgeCenter(rect, h.edge),
                    Quaternion.LookRotation(n, _face.upAxis));
            }
        }

        private void ClearHandles()
        {
            foreach (var h in _handles)
            {
                if (h == null) continue;
                if (Application.isPlaying) Destroy(h.gameObject);
                else DestroyImmediate(h.gameObject);
            }
            _handles.Clear();
        }

        private Material? HandleMaterial()
        {
            if (_material != null) return _material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _material.color = UI.UIStyle.HighlightChanged;
            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", UI.UIStyle.HighlightChanged);
            return _material;
        }

        // Единичный куб. Свой, а не CreatePrimitive: примитивы вырезаются из
        // WebGL-сборки вместе с коллайдерами.
        private static Mesh? _cube;
        private static Mesh CubeMesh()
        {
            if (_cube != null) return _cube;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            const float h = 0.5f;
            Quad(new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h));
            Quad(new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h));
            Quad(new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h));
            Quad(new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h));
            Quad(new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h), new Vector3(-h, h, -h));
            Quad(new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h));

            _cube = new Mesh { name = "TextureOverlayHandleCube", hideFlags = HideFlags.DontSave };
            _cube.SetVertices(verts);
            _cube.SetTriangles(tris, 0);
            _cube.RecalculateNormals();
            return _cube;
        }
    }
}

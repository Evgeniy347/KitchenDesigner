using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TextureOverlayHandle : MonoBehaviour
    {
        public const int EdgeMinU = 0;
        public const int EdgeMaxU = 1;
        public const int EdgeMinV = 2;
        public const int EdgeMaxV = 3;
        public const int EdgeCount = 4;

        public int edge;

        /// <summary>Мировая точка, по которой ручку ловит курсор (её обновляет
        /// PositionHandles). Для кубика это сам кубик, для стрелки — её середина.</summary>
        public Vector3 grabPoint;
    }

    /// <summary>Итог перетаскивания — ОДНА команда на весь drag: промежуточные
    /// кадры в undo-стеке не нужны (правило 2 UI-GUIDELINES).</summary>
    [DefaultExecutionOrder(100)]
    public class TextureOverlayHandles : MonoBehaviour
    {
        private const float LiftAboveSurfaceUnits = 0.012f;

        private const float CubeEdgeUnits = 0.05f;

        // Стрелка переноса: тонкий стержень + конус на конце (как у ручек
        // перемещения объекта — ResizeHandleManager, чтобы жест читался одинаково).
        private const float ShaftLen = 0.09f;
        private const float ShaftRad = 0.012f;
        private const float TipLen = 0.05f;
        private const float TipSize = 0.04f;
        private const float ArrowLen = ShaftLen + TipLen;

        private const float GrabRadiusPixels = 26f;

        private static KitchenElement? _element;
        private static int _index = -1;

        public static bool Active => _element != null && _index >= 0;

        public static bool IsEditing(KitchenElement element, int index) =>
            Active && _element == element && _index == index;

        public static void Toggle(KitchenElement element, int index)
        {
            if (IsEditing(element, index)) { End(); return; }
            Begin(element, index);
        }

        public static void Begin(KitchenElement element, int index)
        {
            End();
            if (element == null || index < 0 || index >= element.TextureOverlays.Count) return;

            if (element.TextureOverlays[index].side == OverlaySide.All)
            {
                UI.ToastNotification.ShowIfAvailable("Область правится только у одной стороны");
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

        public static bool PointerOverHandle() => PickHandle() != null;

        /// <summary>Ручка под курсором, или null.
        ///
        /// Попадание считается НА ЭКРАНЕ, а не лучом по коллайдеру. Физика тут
        /// подводит трижды: ручка стоит вплотную к поверхности и луч возвращает
        /// стену; ручка на дальней грани оказывается за геометрией; а перемещённый
        /// в LateUpdate коллайдер до следующего FixedUpdate вообще стоит на старом
        /// месте (Physics.autoSyncTransforms по умолчанию выключен). Расстояние в
        /// пикселях от курсора до ручки ничем из этого не портится и заодно даёт
        /// одинаковый размер зоны захвата на любом зуме.</summary>
        private static TextureOverlayHandle? PickHandle()
        {
            if (!Active || _instance == null) return null;
            var cam = Camera.main;
            if (cam == null) return null;

            Vector2 mouse = Input.mousePosition;
            TextureOverlayHandle? best = null;
            float bestDist = GrabRadiusPixels;
            foreach (var h in _instance._handles)
            {
                if (h == null) continue;
                var sp = cam.WorldToScreenPoint(h.grabPoint);
                bool behindCamera = sp.z <= 0f;
                if (behindCamera) continue;
                float d = Vector2.Distance(mouse, new Vector2(sp.x, sp.y));
                if (d > bestDist) continue;
                best = h;
                bestDist = d;
            }
            return best;
        }

        private static TextureOverlayHandles? _instance;

        private readonly List<TextureOverlayHandle> _handles = new List<TextureOverlayHandle>();
        private Material? _material;
        private ResizeHandleManager.HandleMode _builtMode;

        private bool _dragging;
        private int _dragEdge;
        private RectInt _rectBefore;
        private List<TextureOverlaySpec> _before = new List<TextureOverlaySpec>();
        private float _grabUMM, _grabVMM;
        private Face _face;
        private int _faceIndex;
        private Vector2Int _faceMM;

        private List<int> _neighbourEdgesMM = new List<int>();

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

        private void BeginDrag(int edge)
        {
            var el = _element;
            if (el == null || !StillValid()) return;

            CaptureFace(el);
            if (!TryPointOnFace(out float uMM, out float vMM)) return;

            _dragEdge = edge;
            _grabUMM = uMM;
            _grabVMM = vMM;
            _rectBefore = el.TextureOverlays[_index].Resolve(_faceMM);
            _before = new List<TextureOverlaySpec>(el.TextureOverlays);
            _neighbourEdgesMM = TextureOverlaySnap.NeighbourEdges(
                el.TextureOverlays, _index, _faceIndex, _faceMM, IsAlongU(edge));
            _dragging = true;
        }

        private static bool IsAlongU(int edge) => edge <= TextureOverlayHandle.EdgeMaxU;

        private void UpdateDrag()
        {
            var el = _element;
            if (el == null || !StillValid()) { FinishDrag(); return; }
            if (!TryPointOnFace(out float uMM, out float vMM)) return;

            bool alongU = IsAlongU(_dragEdge);
            float threshold = TextureOverlaySnap.ThresholdMM();
            RectInt rect;

            if (ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Move)
            {
                rect = MoveRect(_rectBefore, _dragEdge, uMM - _grabUMM, vMM - _grabVMM, _faceMM);
                rect = TextureOverlaySnap.SnapMoved(rect, alongU, _neighbourEdgesMM, threshold, _faceMM);
            }
            else
            {
                if (TextureOverlaySnap.Nearest(_neighbourEdgesMM, alongU ? uMM : vMM, threshold,
                        out int snappedMM))
                {
                    if (alongU) uMM = snappedMM; else vMM = snappedMM;
                }
                rect = StretchRect(_rectBefore, _dragEdge, uMM, vMM, _faceMM);
            }

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

        public static RectInt StretchRect(RectInt rect, int edge, float uMM, float vMM, Vector2Int faceMM)
        {
            int x0 = rect.xMin, x1 = rect.xMax, y0 = rect.yMin, y1 = rect.yMax;
            int min = TextureOverlaySpec.MIN_SIZE_MM;
            switch (edge)
            {
                case TextureOverlayHandle.EdgeMinU:
                    x0 = Mathf.Clamp(Round(uMM), 0, x1 - min); break;
                case TextureOverlayHandle.EdgeMaxU:
                    x1 = Mathf.Clamp(Round(uMM), x0 + min, faceMM.x); break;
                case TextureOverlayHandle.EdgeMinV:
                    y0 = Mathf.Clamp(Round(vMM), 0, y1 - min); break;
                default:
                    y1 = Mathf.Clamp(Round(vMM), y0 + min, faceMM.y); break;
            }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        public static RectInt MoveRect(RectInt rect, int edge, float duMM, float dvMM, Vector2Int faceMM)
        {
            bool alongU = IsAlongU(edge);
            int dx = alongU ? Round(duMM) : 0;
            int dy = alongU ? 0 : Round(dvMM);
            int x0 = Mathf.Clamp(rect.xMin + dx, 0, Mathf.Max(0, faceMM.x - rect.width));
            int y0 = Mathf.Clamp(rect.yMin + dy, 0, Mathf.Max(0, faceMM.y - rect.height));
            return new RectInt(x0, y0, rect.width, rect.height);
        }

        private static int Round(float mm) => Mathf.RoundToInt(mm);

        private void CaptureFace(KitchenElement el)
        {
            var faces = el.GetFaces();
            _faceIndex = Mathf.Clamp((int)el.TextureOverlays[_index].side, 0, faces.Length - 1);
            _face = faces[_faceIndex];
            _faceMM = TextureOverlayGeometry.FaceSizeMM(el.DimensionsMM, _faceIndex);
        }

        private bool TryPointOnFace(out float uMM, out float vMM)
        {
            uMM = vMM = 0f;
            var cam = Camera.main;
            if (cam == null) return false;

            var plane = new Plane(_face.normal, _face.center);
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float dist)) return false;

            var delta = ray.GetPoint(dist) - _face.center;
            uMM = Vector3.Dot(delta, _face.rightAxis) / AppConstants.MM_TO_UNITS + _faceMM.x * 0.5f;
            vMM = Vector3.Dot(delta, _face.upAxis) / AppConstants.MM_TO_UNITS + _faceMM.y * 0.5f;
            return true;
        }

        private Vector3 EdgeCenterWorld(RectInt rect, int edge)
        {
            float uMM = edge switch
            {
                TextureOverlayHandle.EdgeMinU => rect.xMin,
                TextureOverlayHandle.EdgeMaxU => rect.xMax,
                _ => (rect.xMin + rect.xMax) * 0.5f,
            };
            float vMM = edge switch
            {
                TextureOverlayHandle.EdgeMinV => rect.yMin,
                TextureOverlayHandle.EdgeMaxV => rect.yMax,
                _ => (rect.yMin + rect.yMax) * 0.5f,
            };
            return _face.center
                + _face.rightAxis * ((uMM - _faceMM.x * 0.5f) * AppConstants.MM_TO_UNITS)
                + _face.upAxis * ((vMM - _faceMM.y * 0.5f) * AppConstants.MM_TO_UNITS);
        }

        /// <summary>Форма ручки говорит, что она делает, — как и у ручек объекта:
        /// «Ручки: растяжение» → кубик на границе области, «Ручки: перенос» →
        /// стрелка вдоль оси, по которой область поедет.</summary>
        private void BuildHandles()
        {
            _builtMode = ResizeHandleManager.Mode;
            bool move = _builtMode == ResizeHandleManager.HandleMode.Move;

            for (int edge = 0; edge < TextureOverlayHandle.EdgeCount; edge++)
            {
                var go = new GameObject($"TextureOverlayHandle_{edge}") { hideFlags = HideFlags.DontSave };
                var marker = go.AddComponent<TextureOverlayHandle>();
                marker.edge = edge;

                if (move) BuildArrowAlongLocalZ(go.transform);
                else BuildCube(go.transform);

                _handles.Add(marker);
            }
        }

        private void BuildCube(Transform parent)
        {
            var cube = new GameObject("Cube");
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = Vector3.one * CubeEdgeUnits;
            cube.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
            cube.AddComponent<MeshRenderer>().sharedMaterial = HandleMaterial();
        }

        private void BuildArrowAlongLocalZ(Transform parent)
        {
            var mat = HandleMaterial();

            var shaft = new GameObject("Shaft");
            shaft.transform.SetParent(parent, false);
            shaft.transform.localPosition = new Vector3(0, 0, ShaftLen * 0.5f);
            shaft.transform.localScale = new Vector3(ShaftRad * 2f, ShaftRad * 2f, ShaftLen);
            shaft.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
            shaft.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var tip = new GameObject("Tip");
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = new Vector3(0, 0, ShaftLen + TipLen * 0.5f);
            tip.transform.localScale = new Vector3(TipSize * 1.4f, TipSize * 1.4f, TipLen);
            tip.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            tip.AddComponent<MeshRenderer>().sharedMaterial = mat;
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
            bool move = _builtMode == ResizeHandleManager.HandleMode.Move;

            foreach (var h in _handles)
            {
                if (h == null) continue;
                Vector3 basePoint = EdgeCenterWorld(rect, h.edge) + n * LiftAboveSurfaceUnits;

                if (move)
                {
                    Vector3 dir = OutwardAxisOf(h.edge);
                    h.transform.SetPositionAndRotation(basePoint, Quaternion.LookRotation(dir, n));
                    h.grabPoint = basePoint + dir * (ArrowLen * 0.6f);
                }
                else
                {
                    h.transform.SetPositionAndRotation(basePoint, Quaternion.LookRotation(n, _face.upAxis));
                    h.grabPoint = basePoint;
                }
            }
        }

        private Vector3 OutwardAxisOf(int edge) => edge switch
        {
            TextureOverlayHandle.EdgeMinU => -_face.rightAxis,
            TextureOverlayHandle.EdgeMaxU => _face.rightAxis,
            TextureOverlayHandle.EdgeMinV => -_face.upAxis,
            _ => _face.upAxis,
        };

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

        private static Mesh? _cone;
        internal static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            const int seg = 16;
            var verts = new List<Vector3> { new Vector3(0, 0, 0.5f), new Vector3(0, 0, -0.5f) };
            int apex = 0, baseCenter = 1, ring = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, -0.5f));
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                int cur = ring + i, next = ring + (i + 1) % seg;
                tris.Add(apex); tris.Add(next); tris.Add(cur);
                tris.Add(baseCenter); tris.Add(cur); tris.Add(next);
            }
            _cone = new Mesh { name = "TextureOverlayHandleCone", hideFlags = HideFlags.DontSave };
            _cone.SetVertices(verts);
            _cone.SetTriangles(tris, 0);
            _cone.RecalculateNormals();
            return _cone;
        }

        // Свой, а не CreatePrimitive: примитивы вырезаются из WebGL-сборки
        // вместе с коллайдерами.
        private static Mesh? _cube;
        internal static Mesh CubeMesh()
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

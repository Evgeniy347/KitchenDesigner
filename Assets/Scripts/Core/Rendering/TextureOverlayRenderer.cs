using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Рисует накладки текстур поверх граней стен и полов.
    ///
    /// Каждая накладка — отдельный плоский меш (см. <see cref="PlaneWithHolesMesh"/>)
    /// с материалом декора из каталога. Меши НЕ парентятся к элементу: у
    /// KitchenElement в transform.localScale лежит физический габарит
    /// (см. ApplyDimensions), и накладка-ребёнок домножилась бы на него — по
    /// толщине стены в 0.1 раза, да ещё с перекосом от неравномерного масштаба.
    /// Держим их под собственным корнем в мировых координатах, как ElementOutline
    /// и EdgeSideHighlighter.
    ///
    /// Компонент нужен только ради LateUpdate: он догоняет накладками элемент,
    /// который подвинули, повернули, растянули или которому добавили проём.
    /// Сама пересборка статическая и работает без сцены — из тестов и загрузки
    /// проекта её зовут напрямую через <see cref="Refresh"/>.</summary>
    public class TextureOverlayRenderer : MonoBehaviour
    {
        /// <summary>Зазор над гранью на каждый слой накладок, мм. Накладки на
        /// одной стороне идут стопкой в порядке списка — без разноса по высоте
        /// они дрались бы за z-буфер и мерцали.</summary>
        private const float LayerLiftMM = 0.2f;

        private class Entry
        {
            public KitchenElement? element;
            public readonly List<GameObject> quads = new List<GameObject>();
            public Vector3 pos;
            public Quaternion rot = Quaternion.identity;
            public Vector3Int dims;
            public int fingerprint;
            public int openings;
            public bool hidden;
        }

        private static readonly Dictionary<KitchenElement, Entry> Entries =
            new Dictionary<KitchenElement, Entry>();
        private static GameObject? _root;

        private void LateUpdate() => SyncAll();

        private void OnDestroy() => ClearAll();

        /// <summary>Сколько накладочных мешей сейчас в сцене (для тестов).</summary>
        public static int QuadCount
        {
            get
            {
                int n = 0;
                foreach (var e in Entries.Values) n += e.quads.Count;
                return n;
            }
        }

        /// <summary>Накладки конкретного элемента (тестам нужен их МИРОВОЙ размер:
        /// на этом горит любая накладка, унаследовавшая масштаб элемента).</summary>
        public static IReadOnlyList<GameObject> QuadsOf(KitchenElement element) =>
            element != null && Entries.TryGetValue(element, out var e)
                ? e.quads : (IReadOnlyList<GameObject>)System.Array.Empty<GameObject>();

        /// <summary>Пересобрать накладки элемента. Зовётся из
        /// KitchenElement.SetTextureOverlays — единственной точки мутации списка.</summary>
        public static void Refresh(KitchenElement? element)
        {
            if (element == null) return;
            if (!Entries.TryGetValue(element, out var entry))
            {
                entry = new Entry { element = element };
                Entries[element] = entry;
            }
            Rebuild(entry);
        }

        /// <summary>Догнать накладками элементы: снести осиротевшие, пересобрать
        /// те, что сдвинули/повернули/растянули или у кого поменялись проёмы.</summary>
        public static void SyncAll()
        {
            List<KitchenElement>? dead = null;
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                var el = entry.element;
                if (el == null)
                {
                    ClearQuads(entry);
                    (dead ??= new List<KitchenElement>()).Add(pair.Key);
                    continue;
                }

                if (Changed(entry, el)) Rebuild(entry);
            }
            if (dead != null)
                foreach (var key in dead) Entries.Remove(key);
        }

        /// <summary>Снять все накладки (смена проекта, выгрузка сцены, тесты).</summary>
        public static void ClearAll()
        {
            foreach (var entry in Entries.Values) ClearQuads(entry);
            Entries.Clear();
        }

        private static bool Changed(Entry entry, KitchenElement el)
        {
            var t = el.transform;
            return t.position != entry.pos
                || t.rotation != entry.rot
                || el.DimensionsMM != entry.dims
                || Fingerprint(el.TextureOverlays) != entry.fingerprint
                || OpeningCount(el) != entry.openings
                || Hidden(el) != entry.hidden;
        }

        /// <summary>Накладки не показываем, когда сам элемент не виден, когда он
        /// объявлен прозрачным и когда стена опущена режимом обзора.
        ///
        /// Прозрачность здесь принципиальна: накладка — отдельный непрозрачный
        /// меш поверх грани, и сквозная стена с ней выглядела бы сплошной —
        /// выключатель «Прозрачный» просто переставал работать. «Прозрачный»
        /// значит «хочу видеть сквозь», поэтому накладки гаснут вместе с гранью.
        ///
        /// Опущенная стена — временный обрубок, накладка по ПОЛНОЙ грани висела
        /// бы в воздухе.</summary>
        private static bool Hidden(KitchenElement el)
        {
            if (!el.gameObject.activeInHierarchy) return true;
            if (!SceneVisibility.AnyRendererEnabled(el)) return true;
            if (PhotoMode.ResolveTransparent(el.Transparent)) return true;
            var wall = el.GetComponent<Wall>();
            return wall != null && wall.IsLowered;
        }

        private static void Rebuild(Entry entry)
        {
            ClearQuads(entry);

            var el = entry.element;
            if (el == null) return;

            var t = el.transform;
            entry.pos = t.position;
            entry.rot = t.rotation;
            entry.dims = el.DimensionsMM;
            entry.fingerprint = Fingerprint(el.TextureOverlays);
            entry.openings = OpeningCount(el);
            entry.hidden = Hidden(el);
            if (entry.hidden) return;

            var faces = el.GetFaces();
            var overlays = el.TextureOverlays;
            for (int i = 0; i < overlays.Count; i++)
            {
                var spec = overlays[i];
                var def = MaterialCatalog.Get(spec.MaterialId);
                var material = MaterialManager.GetSharedMaterial(def);
                if (material == null) continue; // нет шейдера — лучше ничего, чем розовое
                var tile = MaterialManager.TileMM(def);
                float lift = LayerLiftMM * (i + 1) * AppConstants.MM_TO_UNITS;

                foreach (int faceIndex in TextureOverlayGeometry.FaceIndices(spec.side))
                {
                    if (faceIndex < 0 || faceIndex >= faces.Length) continue;
                    var faceMM = TextureOverlayGeometry.FaceSizeMM(entry.dims, faceIndex);
                    var rect = spec.Resolve(faceMM);
                    var mesh = PlaneWithHolesMesh.Build(rect, OpeningHolesMM(el, faceIndex), tile);
                    if (mesh == null) continue;

                    entry.quads.Add(MakeQuad(faces[faceIndex], faceMM, rect, mesh, material, lift));
                }
            }
        }

        private static GameObject MakeQuad(in KitchenElement.Face face, Vector2Int faceMM,
            RectInt rect, Mesh mesh, Material material, float lift)
        {
            var go = new GameObject("TextureOverlay") { hideFlags = HideFlags.DontSave };
            // Корень стоит в позе identity, поэтому локальный трансформ квада и
            // есть мировой: масштаб элемента в накладку не просачивается.
            go.transform.SetParent(Root(), worldPositionStays: false);

            // Меш центрирован на середине области, а её центр смещён от центра
            // грани на столько миллиметров, на сколько область уехала от центра.
            float du = (rect.xMin + rect.xMax) * 0.5f - faceMM.x * 0.5f;
            float dv = (rect.yMin + rect.yMax) * 0.5f - faceMM.y * 0.5f;

            go.transform.position = face.center
                + face.rightAxis * (du * AppConstants.MM_TO_UNITS)
                + face.upAxis * (dv * AppConstants.MM_TO_UNITS)
                + face.normal * lift;
            // Именно LookRotation(normal, upAxis), а не (-normal, ...): нужен
            // локальный +X вдоль face.rightAxis, иначе накладка зеркалится и
            // направленный рисунок (грейн, плитка со швом) ложится наизнанку.
            go.transform.rotation = Quaternion.LookRotation(face.normal, face.upAxis);
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Накладка — плёнка на самой поверхности: собственная тень от неё
            // легла бы на стену, которую она и покрывает.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        /// <summary>Проёмы окон и дверей в координатах грани (мм). Режем только
        /// две широкие грани стены — ровно те, сквозь которые Wall.RebuildMesh
        /// прогоняет вырез. На торцах и на полу проёмов нет.</summary>
        private static List<RectInt>? OpeningHolesMM(KitchenElement el, int faceIndex)
        {
            var wall = el.GetComponent<Wall>();
            if (wall == null) return null;
            if (wall.AttachedWindows.Count == 0 && wall.AttachedDoors.Count == 0) return null;

            var dims = el.DimensionsMM;
            bool thickAlongX = dims.x <= dims.z;
            int wideA = thickAlongX ? 0 : 4;
            if (faceIndex != wideA && faceIndex != wideA + 1) return null;

            var face = el.GetFaces()[faceIndex];
            var faceMM = TextureOverlayGeometry.FaceSizeMM(dims, faceIndex);

            var holes = new List<RectInt>();
            foreach (var w in wall.AttachedWindows) AddHole(holes, w, face, faceMM);
            foreach (var d in wall.AttachedDoors) AddHole(holes, d, face, faceMM);
            return holes.Count > 0 ? holes : null;
        }

        private static void AddHole(List<RectInt> holes, KitchenElement? opening,
            in KitchenElement.Face face, Vector2Int faceMM)
        {
            if (opening == null) return;

            var delta = opening.transform.position - face.center;
            float uMM = Vector3.Dot(delta, face.rightAxis) / AppConstants.MM_TO_UNITS + faceMM.x * 0.5f;
            float vMM = Vector3.Dot(delta, face.upAxis) / AppConstants.MM_TO_UNITS + faceMM.y * 0.5f;

            // Габарит проёма на осях грани: проём повёрнут вместе со стеной, но
            // считать это через проекцию собственных осей надёжнее, чем угадывать
            // соответствие «ширина проёма ↔ ось грани».
            float halfU = HalfExtent(opening, face.rightAxis);
            float halfV = HalfExtent(opening, face.upAxis);

            holes.Add(new RectInt(
                Mathf.RoundToInt(uMM - halfU), Mathf.RoundToInt(vMM - halfV),
                Mathf.RoundToInt(halfU * 2f), Mathf.RoundToInt(halfV * 2f)));
        }

        private static float HalfExtent(KitchenElement opening, Vector3 axis)
        {
            var rot = opening.transform.rotation;
            var dims = opening.DimensionsMM;
            return 0.5f * (
                Mathf.Abs(Vector3.Dot(rot * Vector3.right, axis)) * dims.x +
                Mathf.Abs(Vector3.Dot(rot * Vector3.up, axis)) * dims.y +
                Mathf.Abs(Vector3.Dot(rot * Vector3.forward, axis)) * dims.z);
        }

        private static int OpeningCount(KitchenElement el)
        {
            var wall = el.GetComponent<Wall>();
            return wall == null ? 0 : wall.AttachedWindows.Count + wall.AttachedDoors.Count;
        }

        /// <summary>Дешёвый отпечаток набора накладок: ловим правки мимо рендера
        /// (undo/redo, MCP, загрузка проекта).</summary>
        private static int Fingerprint(IReadOnlyList<TextureOverlaySpec> overlays)
        {
            unchecked
            {
                int h = 17;
                foreach (var o in overlays) h = h * 31 + o.GetHashCode();
                return h;
            }
        }

        private static Transform Root()
        {
            if (_root == null)
            {
                _root = new GameObject("__TextureOverlays") { hideFlags = HideFlags.DontSave };
                _root.transform.SetParent(null, worldPositionStays: false);
            }
            return _root.transform;
        }

        private static void ClearQuads(Entry entry)
        {
            foreach (var q in entry.quads)
            {
                if (q == null) continue;
                var filter = q.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) DestroyNow(filter.sharedMesh);
                DestroyNow(q);
            }
            entry.quads.Clear();
        }

        /// <summary>В рантайме Destroy: DestroyImmediate из колбэка UI-события
        /// Unity запрещает. В EditMode-тестах Destroy не отрабатывает вовсе,
        /// поэтому там — DestroyImmediate.</summary>
        private static void DestroyNow(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}

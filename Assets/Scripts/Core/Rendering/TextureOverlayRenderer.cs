using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TextureOverlayRenderer : MonoBehaviour
    {
        public const float LayerLiftMM = 0.2f;

        internal const string RootName = "__TextureOverlays";

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

        private void LateUpdate()
        {
            using var _ = PerfMarkers.TextureOverlaySyncAll.Auto();
            SyncAll();
        }

        private void OnDestroy() => ClearAll();

        public static int QuadCount
        {
            get
            {
                int n = 0;
                foreach (var e in Entries.Values) n += e.quads.Count;
                return n;
            }
        }

        public static IReadOnlyList<GameObject> QuadsOf(KitchenElement element) =>
            element != null && Entries.TryGetValue(element, out var e)
                ? e.quads : (IReadOnlyList<GameObject>)System.Array.Empty<GameObject>();

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

        internal static bool Hidden(KitchenElement el)
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
                if (material == null) continue;
                var tile = MaterialManager.TileMM(def);
                float lift = LiftOfLayer(i);

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

        public static float LiftOfLayer(int layerIndex) =>
            LayerLiftMM * (layerIndex + 1) * AppConstants.MM_TO_UNITS;

        public static Vector2 OffsetFromFaceCentreMM(RectInt rect, Vector2Int faceMM) => new Vector2(
            (rect.xMin + rect.xMax) * 0.5f - faceMM.x * 0.5f,
            (rect.yMin + rect.yMax) * 0.5f - faceMM.y * 0.5f);

        private static GameObject MakeQuad(in Face face, Vector2Int faceMM,
            RectInt rect, Mesh mesh, Material material, float lift)
        {
            var go = new GameObject("TextureOverlay") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Root(), worldPositionStays: false);

            Vector2 offsetMM = OffsetFromFaceCentreMM(rect, faceMM);

            go.transform.position = face.center
                + face.rightAxis * (offsetMM.x * AppConstants.MM_TO_UNITS)
                + face.upAxis * (offsetMM.y * AppConstants.MM_TO_UNITS)
                + face.normal * lift;
            go.transform.rotation = Quaternion.LookRotation(face.normal, face.upAxis);
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static List<RectInt>? OpeningHolesMM(KitchenElement el, int faceIndex)
        {
            var wall = el.GetComponent<Wall>();
            if (wall == null) return null;
            if (wall.AttachedWindows.Count == 0 && wall.AttachedDoors.Count == 0) return null;

            var dims = el.DimensionsMM;
            if (!IsWideWallFace(dims, faceIndex)) return null;

            var face = el.GetFaces()[faceIndex];
            var faceMM = TextureOverlayGeometry.FaceSizeMM(dims, faceIndex);

            var holes = new List<RectInt>();
            foreach (var w in wall.AttachedWindows) AddHole(holes, w, face, faceMM);
            foreach (var d in wall.AttachedDoors) AddHole(holes, d, face, faceMM);
            return holes.Count > 0 ? holes : null;
        }

        public static bool IsWideWallFace(Vector3Int dims, int faceIndex)
        {
            bool thickAlongX = dims.x <= dims.z;
            int firstWideFace = thickAlongX ? 0 : 4;
            return faceIndex == firstWideFace || faceIndex == firstWideFace + 1;
        }

        private static void AddHole(List<RectInt> holes, KitchenElement? opening,
            in Face face, Vector2Int faceMM)
        {
            if (opening == null) return;

            var delta = opening.transform.position - face.center;
            float uMM = Vector3.Dot(delta, face.rightAxis) / AppConstants.MM_TO_UNITS + faceMM.x * 0.5f;
            float vMM = Vector3.Dot(delta, face.upAxis) / AppConstants.MM_TO_UNITS + faceMM.y * 0.5f;

            float halfU = HalfExtentAlong(opening, face.rightAxis);
            float halfV = HalfExtentAlong(opening, face.upAxis);

            holes.Add(new RectInt(
                Mathf.RoundToInt(uMM - halfU), Mathf.RoundToInt(vMM - halfV),
                Mathf.RoundToInt(halfU * 2f), Mathf.RoundToInt(halfV * 2f)));
        }

        public static float HalfExtentAlong(KitchenElement opening, Vector3 axis)
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

        private static int Fingerprint(IReadOnlyList<TextureOverlaySpec> overlays)
        {
            unchecked
            {
                int h = 17;
                foreach (var o in overlays) h = h * 31 + o.GetHashCode();
                return h;
            }
        }

        internal static Transform Root()
        {
            if (_root == null)
            {
                _root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
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
                if (filter != null && filter.sharedMesh != null) DestroyNow.The(filter.sharedMesh);
                DestroyNow.The(q);
            }
            entry.quads.Clear();
        }
    }
}

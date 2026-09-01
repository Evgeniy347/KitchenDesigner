using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ElementRoot
    {
        public const string ELEMENT_TAG = "KitchenElement";

        public static GameObject NewCube(string name, string fallbackName, Vector3 position) =>
            Prepare(GameObject.CreatePrimitive(PrimitiveType.Cube), name, fallbackName, position);

        public static GameObject NewEmpty(string name, string fallbackName, Vector3 position) =>
            Prepare(new GameObject(), name, fallbackName, position);

        private static GameObject Prepare(GameObject go, string name, string fallbackName, Vector3 position)
        {
            go.name = ElementNaming.Normalize(string.IsNullOrEmpty(name) ? fallbackName : name);
            go.tag = ELEMENT_TAG;
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            return go;
        }

        public static void SwapBoxColliderForMeshCollider(GameObject go)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box != null) Object.DestroyImmediate(box);
            go.AddComponent<MeshCollider>().convex = false;
        }

        public static void UseMeshCollider(GameObject go, Mesh mesh)
        {
            var existing = go.GetComponent<Collider>();
            if (existing != null && !(existing is MeshCollider)) Object.DestroyImmediate(existing);

            var meshCollider = go.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.convex = false;
            }
            meshCollider.sharedMesh = mesh;
        }

        public static GameObject Publish(GameObject go, KitchenElement element)
        {
            PartRegistry.Register(element);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return go;
        }
    }
}

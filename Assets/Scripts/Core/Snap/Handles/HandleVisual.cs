using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public enum HandleShaft { Cylinder, Box }

    public enum HandleTip { Box, Cone }

    /// <summary>Сборка стрелки-ручки вдоль локального +Z, без коллайдеров.</summary>
    public static class HandleVisual
    {
        public static void BuildArrow(Transform parent, Material? material,
            in HandleMetrics metrics, HandleShaft shaft, HandleTip tip)
        {
            Piece("Shaft", parent, material,
                shaft == HandleShaft.Cylinder ? HandleMeshes.Cylinder() : HandleMeshes.Cube(),
                new Vector3(0f, 0f, metrics.ShaftCenterZ), metrics.ShaftScale);

            Piece("Tip", parent, material,
                tip == HandleTip.Cone ? HandleMeshes.Cone() : HandleMeshes.Cube(),
                new Vector3(0f, 0f, metrics.TipCenterZ),
                tip == HandleTip.Cone ? metrics.ConeTipScale : metrics.BoxTipScale);
        }

        public static void BuildCube(Transform parent, Material? material, float edgeUnits) =>
            Piece("Cube", parent, material, HandleMeshes.Cube(),
                Vector3.zero, Vector3.one * edgeUnits);

        private static void Piece(string name, Transform parent, Material? material,
            Mesh mesh, Vector3 localPosition, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}

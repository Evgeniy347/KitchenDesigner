using UnityEngine;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class ElementData
    {
        public string name;
        public int[] dimensionsMM = new int[3];
        public float[] position = new float[3];
        public float[] rotation = new float[4];

        public ElementData() { }

        public static ElementData FromElement(KitchenElement element)
        {
            var d = new ElementData { name = element.BoardName };
            var dims = element.DimensionsMM;
            d.dimensionsMM = new[] { dims.x, dims.y, dims.z };

            var p = element.transform.position;
            d.position = new[] { p.x, p.y, p.z };

            var r = element.transform.rotation;
            d.rotation = new[] { r.x, r.y, r.z, r.w };
            return d;
        }

        public Vector3Int Dimensions =>
            new Vector3Int(dimensionsMM[0], dimensionsMM[1], dimensionsMM[2]);

        public Vector3 Position =>
            new Vector3(position[0], position[1], position[2]);

        public Quaternion Rotation =>
            new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
    }
}

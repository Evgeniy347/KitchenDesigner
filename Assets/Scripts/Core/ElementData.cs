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
        public bool movable = true;
        public bool isWall = false;
        public bool isFacade = false;
        public int gapLeft = 2;
        public int gapRight = 2;
        public int gapTop = 2;
        public int gapBottom = 2;
        public int groupId = 0;
        public string materialId = MaterialCatalog.DefaultId;
        public bool transparent = false;
        public int doorMode = 0;
        public bool doorOpen = false;

        public ElementData() { }

        public static ElementData FromElement(KitchenElement element)
        {
            var d = new ElementData { name = element.BoardName };
            var dims = element.DimensionsMM;
            d.dimensionsMM = new[] { dims.x, dims.y, dims.z };

            // Полускрытая (опущенная) стена временно смещена вниз — в сохранение
            // пишем её ПОЛНУЮ позицию, иначе после загрузки она «утонет» (станет ниже).
            var wall = element.GetComponent<Wall>();
            var p = wall != null ? wall.FullPosition : element.transform.position;
            d.position = new[] { p.x, p.y, p.z };

            var r = element.transform.rotation;
            d.rotation = new[] { r.x, r.y, r.z, r.w };

            d.movable = element.Movable;
            d.isWall = wall != null;
            var facade = element as FacadeElement;
            d.isFacade = facade != null;
            if (facade != null)
            {
                d.gapLeft = facade.GapLeft;
                d.gapRight = facade.GapRight;
                d.gapTop = facade.GapTop;
                d.gapBottom = facade.GapBottom;
                d.doorMode = (int)facade.Mode;
                d.doorOpen = facade.IsOpen;
            }
            d.groupId = element.GroupId;
            d.materialId = element.MaterialId;
            d.transparent = element.Transparent;
            return d;
        }

        // Геттеры устойчивы к повреждённому/неполному JSON (не кидают исключение,
        // а возвращают безопасные значения по умолчанию).
        public Vector3Int Dimensions =>
            dimensionsMM != null && dimensionsMM.Length >= 3
                ? new Vector3Int(dimensionsMM[0], dimensionsMM[1], dimensionsMM[2])
                : new Vector3Int(1, 1, 1);

        public Vector3 Position =>
            position != null && position.Length >= 3
                ? new Vector3(position[0], position[1], position[2])
                : Vector3.zero;

        public Quaternion Rotation =>
            rotation != null && rotation.Length >= 4
                ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])
                : Quaternion.identity;
    }
}

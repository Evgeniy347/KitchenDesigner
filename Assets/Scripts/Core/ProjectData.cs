using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class ProjectData
    {
        public int version = AppConstants.SAVE_FORMAT_VERSION;
        public ElementData[] elements = new ElementData[0];
        public GroupData[] groups = new GroupData[0];
        public CameraState camera = new CameraState();

        public ProjectData() { }

        public ProjectData(IEnumerable<ElementData> items)
        {
            elements = new List<ElementData>(items).ToArray();
        }
    }

    [System.Serializable]
    public class GroupData
    {
        public int id;
        public string name = "Группа";
        public bool movable = true;
    }

    /// <summary>Сериализуемое состояние камеры. valid=false у старых сейвов без камеры.</summary>
    [System.Serializable]
    public struct CameraState
    {
        public bool valid;
        public float targetX, targetY, targetZ;
        public float angleX, angleY, distance;
    }
}

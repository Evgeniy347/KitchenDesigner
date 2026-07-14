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

        // Режим ручек выделенного элемента (Resize / Move).
        public string handleMode = "Resize";

        /// <summary>true если поле basePlate сохранено (иначе JsonUtility сериализует
        /// null-ссылку как {} с нулями, и десериализация даёт new ElementData(), а не null).</summary>
        public bool basePlateValid = false;

        /// <summary>Пол (BasePlate): позиция, размеры, поворот.</summary>
        public ElementData? basePlate = null;

        // История отмены/повтора. elementIndex в записях ссылается на позицию в
        // массиве elements. Старые сейвы без истории → пустые массивы.
        public CommandRecord[] undoHistory = new CommandRecord[0];
        public CommandRecord[] redoHistory = new CommandRecord[0];

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

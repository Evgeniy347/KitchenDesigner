using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class ProjectData
    {
        public int version = AppConstants.SAVE_FORMAT_VERSION;
        public ElementData[] elements = new ElementData[0];
        public GroupData[] groups = new GroupData[0];
        public RoomData[] rooms = new RoomData[0];
        public FloorplanScopeData[] floorplans = new FloorplanScopeData[0];
        public CameraState camera = new CameraState();

        // Режим ручек выделенного элемента (Resize / Move).
        public string handleMode = "Resize";

        /// <summary>Свободный текст «инструкции проекта» (соглашения: толщины
        /// несущих/перегородок, толщина ЛДСП, зазоры и т.п.). Пусто у старых сейвов.</summary>
        public string projectInstructions = "";

        /// <summary>true если поле basePlate сохранено (иначе JsonUtility сериализует
        /// null-ссылку как {} с нулями, и десериализация даёт new ElementData(), а не null).</summary>
        public bool basePlateValid = false;

        /// <summary>Пол (BasePlate): позиция, размеры, поворот.</summary>
        public ElementData? basePlate = null;

        // История отмены/повтора. elementIndex в записях ссылается на позицию в
        // массиве elements. Старые сейвы без истории → пустые массивы.
        public CommandRecord[] undoHistory = new CommandRecord[0];
        public CommandRecord[] redoHistory = new CommandRecord[0];

        /// <summary>Настройки кухни (сетка, снап, автосейв, графика…).
        /// null у старых сейвов — тогда настройки берутся из умолчаний ScriptableObject.</summary>
        public KitchenSettingsData? settings = null;

        /// <summary>Тумблеры вида из тулбара. Инициализаторы = дефолты приложения:
        /// у старых сейвов этих полей в JSON нет, и JsonUtility оставит их как есть.</summary>
        public bool tintEnabled = true;
        public bool lightsOn = true;

        /// <summary>Окна проекта (спецификация, сцена, ошибки, настройки,
        /// инструкции, день/ночь): положение и открыто/закрыто. Пусто у старых
        /// сейвов — окна остаются на своих местах по умолчанию.</summary>
        public WindowStateData[] windows = new WindowStateData[0];

        public ProjectData() { }

        public ProjectData(IEnumerable<ElementData> items)
        {
            elements = new List<ElementData>(items).ToArray();
        }
    }

    /// <summary>Состояние одного окна проекта: где стоит и открыто ли.
    /// height = 0 у окон с фиксированной высотой.</summary>
    [System.Serializable]
    public class WindowStateData
    {
        public string id = "";
        public bool visible;
        public float x;
        public float y;
        public float height;
    }

    [System.Serializable]
    public class GroupData
    {
        public int id;
        public string name = "Группа";
        public bool movable = true;
        public string widthAxis = "x";
    }

    [System.Serializable]
    public class RoomData
    {
        public string floorplanId = "";
        public string id = "";
        public string floor = "";
        public string[] walls = System.Array.Empty<string>();
        public string[] openings = System.Array.Empty<string>();
        // World X,Z polygon pairs in MM, used to classify modules/furniture.
        public int[] polygonXZ = System.Array.Empty<int>();
    }

    [System.Serializable]
    public class FloorplanScopeData
    {
        public string id = "";
        public string[] elements = System.Array.Empty<string>();
    }

    /// <summary>Сериализуемое состояние камеры. valid=false у старых сейвов без камеры.</summary>
    [System.Serializable]
    public struct CameraState
    {
        public bool valid;
        public float targetX, targetY, targetZ;
        public float angleX, angleY, distance;
        public float photoDistance; // отдельный зум фоторежима (0 у старых сейвов)
        // Независимые позиция и угол фоторежима (0 у старых сейвов → fallback на обычные).
        public float photoTargetX, photoTargetY, photoTargetZ;
        public float photoAngleX, photoAngleY;
    }

    /// <summary>Сериализуемые настройки кухни (сетка, снап, автосейв, графика…).</summary>
    [System.Serializable]
    public class KitchenSettingsData
    {
        public int gridStep;
        public bool gridEnabled;
        public bool snapEnabled;
        public float snapThreshold;
        public bool blockOnViolation;
        public bool autoSave;
        public int autoSaveInterval;
        public bool spatialGrid;
        public bool windowedMode;
        public bool edgeOutline;
        public bool wallsEnabled;
        public bool lowerNearWalls;
        public bool cameraPanFree;

        // Отображение и управление. Инициализаторы задают дефолты для старых
        // проектов, где полей ещё нет в JSON (см. комментарий про JsonUtility ниже).
        public bool wallOutline = true;
        public bool hideOpeningsOnLoweredWalls = false;
        public bool objectsVisible = true;
        public bool hideLightSources = false;
        public float mouseSensitivity = 1f;
        public float wasdSpeed = 1f;
        public float arrowSpeed = 1f;

        // Фоторежим. Инициализаторы задают дефолты для старых проектов, где этих
        // полей нет в JSON: JsonUtility.FromJson создаёт объект (инициализаторы
        // срабатывают), затем перезаписывает только присутствующие поля.
        public int photoQuality = (int)PhotoQualityPreset.High;
        public bool photoShadows = true;
        public bool photoSoftShadows = true;
        public bool photoAntiAliasing = true;
        public bool photoSupersampling = true;
        public bool photoAmbientOcclusion = true;
        public bool photoBloom = true;
        public bool photoVignette = true;
        public bool photoCeiling = true;
        public bool photoSSGI = true;
    }
}

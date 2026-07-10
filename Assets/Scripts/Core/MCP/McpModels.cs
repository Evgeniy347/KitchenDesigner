using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class McpRequest
    {
        public string id;
        public string method;

        /// <summary>
        /// Параметры как прямой JSON-объект.
        /// Клиент присылает {id, method, params: {name: "...", x: 1.5, ...}}
        /// </summary>
        [Newtonsoft.Json.JsonProperty("params")]
        public Newtonsoft.Json.Linq.JObject Params { get; set; }

        /// <summary>Опциональные HTTP-подобные заголовки (If-None-Match и т.д.).</summary>
        [Newtonsoft.Json.JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; }
    }

    [Serializable]
    public class McpResponse
    {
        public string id;
        public string type; // "result" | "error" | "not_modified"
        public object data;

        /// <summary>ETag для кэширования (только для get_all_elements).</summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string etag;

        public static McpResponse Result(string id, object data) =>
            new McpResponse { id = id, type = "result", data = data };

        public static McpResponse Error(string id, int code, string message) =>
            new McpResponse { id = id, type = "error", data = new { code, message } };

        /// <summary>Данные не изменились — используй кэш.</summary>
        public static McpResponse NotModified(string id, string etag) =>
            new McpResponse { id = id, type = "not_modified", etag = etag };
    }

    // ── Parameter types per method ─────────────────────────────────────

    [Serializable]
    public class ParamsWithName
    {
        public string name;
        public string object_path;
    }

    [Serializable]
    public class ParamsFindObjects
    {
        public string name_filter;
    }

    [Serializable]
    public class ParamsSetActive
    {
        public string object_path;
        public bool active;
    }

    [Serializable]
    public class ParamsSetTransform
    {
        public string object_path;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class ParamsMoveElement
    {
        public string name;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class ParamsResizeElement
    {
        public string name;
        public int width;
        public int height;
        public int depth;
        public int dimX;
        public int dimY;
        public int dimZ;
    }

    [Serializable]
    public class ParamsRotateElement
    {
        public string name;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class ParamsElementLock
    {
        public string name;
        public bool locked; // true → Movable=false, false → Movable=true
    }

    [Serializable]
    public class ParamsCreateElement
    {
        public string template_name;
        public string name;
        public float x;
        public float y;
        public float z;
        public int width;
        public int height;
        public int depth;
        public bool is_wall;
        public bool is_floor;
        public bool is_facade;
        public int gapLeft = 2;
        public int gapRight = 2;
        public int gapTop = 2;
        public int gapBottom = 2;
    }

    [Serializable]
    public class ParamsMenuPath
    {
        public string menu_path;
    }

    [Serializable]
    public class ParamsLogCount
    {
        public int count;
    }

    [Serializable]
    public class ParamsExportCsv
    {
        public string path;
    }

    [Serializable]
    public class ParamsSetEnabled
    {
        public bool enabled;
    }

    [Serializable]
    public class ParamsSetSetting
    {
        public string name;
        public bool value;
    }

    [Serializable]
    public class ParamsSnapDiagnose
    {
        public string name;
        // Тестовая позиция; отсутствующие оси берутся из текущей позиции доски.
        public float? x;
        public float? y;
        public float? z;
    }

    [Serializable]
    public class ParamsSimulateMove
    {
        public string name;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class ParamsSimulateResize
    {
        public string name;
        public int width;
        public int height;
        public int depth;
        public int dimX;
        public int dimY;
        public int dimZ;
    }

    [Serializable]
    public class ParamsCreateModule
    {
        public string name;      // имя модуля («Тумба с ящиками»)
        public string[] members; // имена деталей (BoardName)
    }

    [Serializable]
    public class ParamsModule
    {
        public string module; // id (числом) или имя модуля
    }

    [Serializable]
    public class ParamsModuleElement
    {
        public string module;
        public string name; // имя детали
    }

    // ── Return types ──────────────────────────────────────────────────

    [Serializable]
    public class ElementInfo
    {
        public string name;
        public string type;
        public int dimX;
        public int dimY;
        public int dimZ;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public bool active;
        public int moduleId;      // 0 — не в модуле
        public string moduleName; // null — не в модуле
        public bool hasViolations; // true — элемент нарушает ограничения (пересечение/нет связи)
        public float aabbMinX, aabbMinY, aabbMinZ;
        public float aabbMaxX, aabbMaxY, aabbMaxZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
    }

    /// <summary>Конфигурация модуля: имя, состав, габариты. Через MCP видно,
    /// что «модуль X состоит из…».</summary>
    [Serializable]
    public class ModuleInfo
    {
        public int id;
        public string name;
        public bool movable;
        public bool editing;         // модуль сейчас в режиме редактирования
        public int elementCount;
        public float[] boundsCenter; // центр AABB, юниты (метры)
        public int[] boundsSizeMM;   // габариты AABB, мм
        public List<ElementInfo> elements;
    }

    [Serializable]
    public class SpecInfo
    {
        public List<SpecLineInfo> lines;
        public int totalCount;
        public float totalAreaM2;
    }

    [Serializable]
    public class SpecLineInfo
    {
        public string name;
        public int dimX;
        public int dimY;
        public int dimZ;
        public int count;
        public float areaPerBoardM2;
        public float totalAreaM2;
    }

    [Serializable]
    public class UndoStackInfo
    {
        public bool canUndo;
        public bool canRedo;
        public string undoDescription;
    }

    [Serializable]
    public class ConsoleLogEntry
    {
        public string type;
        public string message;
        public string stackTrace;
    }

    [Serializable]
    public class ObjectInfo
    {
        public string name;
        public string path;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float scaleX;
        public float scaleY;
        public float scaleZ;
        public bool active;
        public List<string> components;
        public List<string> children;
    }

    [Serializable]
    public class HierarchyNode
    {
        public string name;
        public string path;
        public bool active;
        public List<HierarchyNode> children;
    }

    [Serializable]
    public class StatusInfo
    {
        public string sceneName;
        public int objectCount;
        public bool isPlaying;
        public string platform;
    }

    // ── Новые типы для пространственной информации ─────────────────────

    [Serializable]
    public class AabbInfo
    {
        public float minX, minY, minZ;
        public float maxX, maxY, maxZ;
    }

    [Serializable]
    public class FaceInfo
    {
        public float centerX, centerY, centerZ;
        public float normalX, normalY, normalZ;
        public float sizeX, sizeY;
    }

    [Serializable]
    public class VertexInfo
    {
        public float x, y, z;
    }

    [Serializable]
    public class ElementDebugInfo
    {
        public string name;
        public string type;
        public AabbInfo aabb;
        public FaceInfo[] faces;
        public VertexInfo[] vertices;
        public int dimX, dimY, dimZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
    }

    [Serializable]
    public class AxisGapInfo
    {
        public string axis;
        public string neighbor;
        public float gapMM;
        public bool isOverlap;
    }

    [Serializable]
    public class ElementGapsResult
    {
        public string name;
        public List<AxisGapInfo> gaps;
    }

    [Serializable]
    public class SimulateResult
    {
        public string name;
        public AabbInfo currentAABB;
        public AabbInfo simulatedAABB;
        public List<string> overlapsWith;
        public List<AxisGapInfo> faceGaps;
        public bool wouldHaveViolations;
    }
}

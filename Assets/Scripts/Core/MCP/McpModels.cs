using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class McpRequest
    {
        public string id;
        public string method;
        public string parameters; // raw JSON string

        // Альтернативная форма: клиент прислал параметры объектом `params`
        // (а не строкой `parameters`). Нормализуется в ProcessLine.
        [Newtonsoft.Json.JsonProperty("params")]
        public Newtonsoft.Json.Linq.JObject paramsObject;
    }

    [Serializable]
    public class McpResponse
    {
        public string id;
        public string type; // "result" or "error"
        public object data;

        public static McpResponse Result(string id, object data) =>
            new McpResponse { id = id, type = "result", data = data };

        public static McpResponse Error(string id, int code, string message) =>
            new McpResponse { id = id, type = "error", data = new { code, message } };
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
    public class ParamsSnapDiagnose
    {
        public string name;
        // Тестовая позиция; отсутствующие оси берутся из текущей позиции доски.
        public float? x;
        public float? y;
        public float? z;
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
}

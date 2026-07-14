using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Wire protocol envelope (McpRequest/McpResponse) and RESULT types returned to the
// agent. The tool PARAMETER types live in Contract/McpToolParams.cs (shared with the
// server); results stay here because the server forwards them as opaque JSON.

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class McpRequest
    {
        public string id = string.Empty;
        public string method = string.Empty;

        /// <summary>
        /// Параметры команды как JSON-объект. Единый формат провода: все клиенты
        /// (MCP-сервер на TS и PowerShell-мост tools/unity-bridge.ps1) шлют
        /// {id, method, params: {name: "...", x: 1.5, ...}} — объектом, не строкой.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("params")]
        public JObject? Params { get; set; }

        /// <summary>Опциональные HTTP-подобные заголовки (If-None-Match и т.д.).</summary>
        [Newtonsoft.Json.JsonProperty("headers")]
        public Dictionary<string, string>? Headers { get; set; }
    }

    [Serializable]
    public class McpResponse
    {
        public string id = string.Empty;
        public string type = string.Empty; // "result" | "error" | "not_modified"
        public object? data;

        /// <summary>ETag для кэширования (только для get_all_elements).</summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string? etag;

        public static McpResponse Result(string id, object data) =>
            new McpResponse { id = id, type = "result", data = data };

        public static McpResponse Error(string id, int code, string message) =>
            new McpResponse { id = id, type = "error", data = new { code, message } };

        /// <summary>Данные не изменились — используй кэш.</summary>
        public static McpResponse NotModified(string id, string etag) =>
            new McpResponse { id = id, type = "not_modified", etag = etag };
    }

    // ── Return types ──────────────────────────────────────────────────

    [Serializable]
    public class ElementInfo
    {
        public string name = string.Empty;
        public string type = string.Empty;
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
        public string? moduleName; // null — не в модуле
        public string materialId = string.Empty;  // id декора/текстуры (см. list_materials)
        public bool hasViolations; // true — элемент нарушает ограничения (пересечение/нет связи)
        public float aabbMinX, aabbMinY, aabbMinZ;
        public float aabbMaxX, aabbMaxY, aabbMaxZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
        public List<AxisGapInfo>? faceGaps; // зазоры/пересечения с ближайшим соседом по каждой оси (всегда 3 оси)
        public int radius; // только для RadialShelfElement, иначе 0

        // ── Фасадная валидация (только для FacadeElement / AssembledFacadeElement) ──
        public float faceNormalX, faceNormalY, faceNormalZ; // мировая нормаль лицевой грани
        public bool faceInward; // true, если фасад развёрнут лицом внутрь модуля
        public List<FaceObstructionInfo>? faceObstructions; // детали вплотную перед лицевой гранью
        public List<OpeningViolationInfo>? openingViolations; // детали, пересекающие траекторию открывания
        public DrawerInfo? drawer; // свойства ящика, только для DrawerElement
    }

    [Serializable]
    public class FaceObstructionInfo
    {
        public string neighbor = string.Empty;
        public float distanceFromFaceMm;
        public float overlapWidthMm;
        public float overlapHeightMm;
    }

    [Serializable]
    public class OpeningViolationInfo
    {
        public string neighbor = string.Empty;
        public string openingMode = string.Empty;
        public float collisionAtProgress; // 0..1, где 1 = полностью открыто
        public float collisionOverlapMm;
    }

    /// <summary>Конфигурация модуля: имя, состав, габариты. Через MCP видно,
    /// что «модуль X состоит из…».</summary>
    [Serializable]
    public class ModuleInfo
    {
        public int id;
        public string name = string.Empty;
        public bool movable;
        public bool editing;         // модуль сейчас в режиме редактирования
        public int elementCount;
        public float[]? boundsCenter; // центр AABB, юниты (метры)
        public int[]? boundsSizeMM;   // габариты AABB, мм
        public List<ElementInfo> elements = new();
    }

    [Serializable]
    public class SpecInfo
    {
        public List<SpecLineInfo> lines = new();
        public int totalCount;
        public float totalAreaM2;
    }

    [Serializable]
    public class SpecLineInfo
    {
        public string name = string.Empty;
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
        public string undoDescription = string.Empty;
    }

    [Serializable]
    public class ConsoleLogEntry
    {
        public string type = string.Empty;
        public string message = string.Empty;
        public string stackTrace = string.Empty;
    }

    [Serializable]
    public class ObjectInfo
    {
        public string name = string.Empty;
        public string path = string.Empty;
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
        public List<string> components = new();
        public List<string> children = new();
    }

    [Serializable]
    public class HierarchyNode
    {
        public string name = string.Empty;
        public string path = string.Empty;
        public bool active;
        public List<HierarchyNode> children = new();
    }

    [Serializable]
    public class StatusInfo
    {
        public string sceneName = string.Empty;
        public int objectCount;
        public bool isPlaying;
        public string platform = string.Empty;
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
        public string name = string.Empty;
        public string type = string.Empty;
        public AabbInfo aabb = null!;
        public FaceInfo[] faces = null!;
        public VertexInfo[] vertices = null!;
        public int dimX, dimY, dimZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
    }

    [Serializable]
    public class AxisGapInfo
    {
        public string axis = string.Empty;
        public string neighbor = string.Empty;
        public float gapMM;
        public bool isOverlap;
    }

    [Serializable]
    public class ElementGapsResult
    {
        public string name = string.Empty;
        public List<AxisGapInfo> gaps = new();
    }

    [Serializable]
    public class SimulateResult
    {
        public string name = string.Empty;
        public AabbInfo currentAABB = null!;
        public AabbInfo simulatedAABB = null!;
        public List<string> overlapsWith = new();
        public List<AxisGapInfo> faceGaps = new();
        public bool wouldHaveViolations;
    }

    [Serializable]
    public class DrawerInfo
    {
        public string drawerType = string.Empty;
        public int drawerLength;
        public string drawerColor = string.Empty;
        public int internalWidth;
        public bool isDouble;
        public bool isUpper;
        public string pairedDrawerName = string.Empty;
        public string attachedFacadeName = string.Empty;
        public string doubleState = string.Empty;
        public bool isOpen;
    }
}

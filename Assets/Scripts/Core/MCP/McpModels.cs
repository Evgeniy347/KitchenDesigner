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
        public bool locked;       // true — move/resize/delete отклоняются (см. set_element_lock)
        public int moduleId;      // 0 — не в модуле
        public string? moduleName; // null — не в модуле
        public string materialId = string.Empty;  // id декора/текстуры (см. list_materials)
        public bool hasViolations; // true — элемент нарушает ограничения (пересечение/нет связи)
        public float aabbMinX, aabbMinY, aabbMinZ;
        public float aabbMaxX, aabbMaxY, aabbMaxZ;
        // Габариты в МИРОВЫХ осях (мм, из AABB) — в отличие от dimX/dimY/dimZ,
        // учитывают поворот элемента. Для повёрнутой на 90° стенки worldDimX=332.
        public int worldDimX, worldDimY, worldDimZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
        public List<AxisGapInfo>? faceGaps; // зазоры/пересечения с ближайшим соседом «напротив» по осям (ось без соседа опускается)
        public int cornerRadius; // радиус скругления: угол радиусной полки или углы табуретки, иначе 0
        public string? grooves;  // пазы детали "through:top, blind:left"; null, если пазов нет
        // Накладки текстур стены/пола "a:oak; b:white@100,200+800x600"; null, если их нет
        public string? textureOverlays;
        // Кромкование (только листовая «деталь»; у прочих типов поля опускаются).
        public bool? edgeBanding;        // кромковать открытые торцы
        public float? edgeThicknessMM;   // толщина кромочной ленты, мм
        public bool? edgeSkipValidation; // не выдавать EDG-01 по этой детали
        public string? edges;            // торцы с кромкой, вычислено: "L1,W1"
        // Прикрепление к другой детали/фасаду (AttachLinks): деталь едет за
        // родителем при переносе, повороте и открывании. null — не прикреплена.
        public string? attachedToName;
        // true — связь есть, а контакта нет: сборка разъехалась (ошибка ATT-01).
        public bool? attachDetached;

        // ── Фасадная валидация (только для FacadeElement / AssembledFacadeElement;
        //    null-поля опускаются сериализатором — обычные детали их не несут) ──
        public string? facadeMode; // режим открывания ("front_left", "drawer_out", …), null для не-фасадов
        public float? faceNormalX, faceNormalY, faceNormalZ; // мировая нормаль лицевой грани
        public bool? faceInward; // true, если фасад развёрнут лицом внутрь модуля
        public List<FaceObstructionInfo>? faceObstructions; // детали вплотную перед лицевой гранью
        public List<OpeningViolationInfo>? openingViolations; // детали, пересекающие траекторию открывания
        public DrawerInfo? drawer; // свойства ящика, только для DrawerElement
        public TableInfo? table; // свойства стола, только для TableElement
		public RadiusTableInfo? radiusTable; // свойства радиусного стола, только для RadiusTableElement
		public StoolInfo? stool;
		public PillarInfo? pillar; // свойства опоры, только для PillarElement
		public CooktopInfo? cooktop; // свойства варочной, только для CooktopElement
		public OvenInfo? oven; // свойства духовки, только для OvenElement
		public DishwasherInfo? dishwasher; // свойства посудомойки, только для DishwasherElement
		public WindowInfo? window; // свойства окна, только для WindowElement
		public DoorInfo? door; // свойства двери, только для DoorElement
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
        public string widthAxis = "x";
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
        public string projectInstructions = string.Empty;
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
        public AabbInfo? aabb;
        public FaceInfo[]? faces;
        public VertexInfo[]? vertices;
        public int dimX, dimY, dimZ;
        public int effectiveDimX, effectiveDimY, effectiveDimZ;
    }

    [Serializable]
    public class AxisGapInfo
    {
        public string axis = string.Empty;
        public string neighbor = string.Empty;
        public float gapMM;      // 0 при touching; > 0 — зазор; < 0 — глубина пересечения
        public bool touching;    // |зазор| < 0.5 мм — детали вплотную (это НЕ нарушение)
        public bool isOverlap;   // реальное пересечение глубже допуска
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
        public AabbInfo? currentAABB;
        public AabbInfo? simulatedAABB;
        public List<string> overlapsWith = new();
        public List<AxisGapInfo> faceGaps = new();
        public bool wouldHaveViolations;
    }

    [Serializable]
    public class DrawerInfo
    {
        public string system = string.Empty;   // "gtv" | "movento"
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

    [Serializable]
    public class TableInfo
    {
        public int legInsetMM;
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

    [Serializable]
    public class RadiusTableInfo
    {
        public int legInsetMM;
        public string shape = "capsule";
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

    [Serializable]
    public class StoolInfo
    {
        public int cornerRadiusMM;
        public string shape = string.Empty;
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

	[Serializable]
	public class PillarInfo
	{
		public int midHeightMM;
		public int diameterMM;
	}

	/// <summary>Варочная поверхность. dimX/dimY/dimZ — верхняя плита (dimY —
	/// ОБЩАЯ высота: плита 5 мм + короб), здесь — короб, уходящий в столешницу,
	/// и деталь, в которую он врезан.</summary>
	[Serializable]
	public class CooktopInfo
	{
		/// <summary>Готовая модель производителя («Bosch PUE611BB5E») или пусто.
		/// У модели размеры и вырез фиксированы — edit_elements их отклоняет.</summary>
		public string model = string.Empty;
		public bool fixedSize;
		public int cutoutWidthMM;
		public int cutoutDepthMM;
		public int plateHeightMM;
		public int bodyHeightMM;
		public string attachedPartName = string.Empty;
		public int offsetXMM;
		public int offsetYMM;
		/// <summary>Разворот панели вокруг нормали столешницы (°). Ставится через
		/// rot_y; rot_x/rot_z у техники отклоняются.</summary>
		public float yawDeg;
	}

	/// <summary>Духовой шкаф. dimX/dimY/dimZ — габарит целиком (фасад плюс
	/// корпус), здесь — разбивка на фасад и корпус в нише. Всё фиксировано
	/// моделью: edit_elements отклоняет любую правку размера.</summary>
	[Serializable]
	public class OvenInfo
	{
		public string model = string.Empty;
		public bool fixedSize;
		public float facadeThicknessMM;
		public int bodyWidthMM;
		public int bodyDepthMM;
		public int bodyHeightMM;
		public int controlPanelHeightMM;
		public int glassHeightMM;
		/// <summary>Ручка выступает ВПЕРЁД за габаритную коробку: коробка
		/// описывает то, что встаёт в нишу колонны.</summary>
		public int handleProtrusionMM;
		/// <summary>Дверца откинута (edit_elements {is_open}). Откидывается вниз
		/// вокруг нижней кромки фасада.</summary>
		public bool isOpen;
	}

	/// <summary>Полновстраиваемая посудомоечная машина. dimX/dimY/dimZ — сам
	/// прибор; ниша и мебельный фасад в габарит НЕ входят — фасад отдельный
	/// элемент, пристёгнутый по имени (attachedFacadeName). Всё фиксировано
	/// моделью: edit_elements отклоняет любую правку размера.</summary>
	[Serializable]
	public class DishwasherInfo
	{
		public string model = string.Empty;
		public bool fixedSize;
		/// <summary>Имя пристёгнутого мебельного фасада; пусто — фасада нет.</summary>
		public string attachedFacadeName = string.Empty;
		public int nicheWidthMM;
		public int nicheMinDepthMM;
		public int heightMinMM;
		public int heightMaxMM;
		public int facadeWidthMM;
		public int facadeMinHeightMM;
		public int facadeMaxHeightMM;
		public int facadeNominalHeightMM;
		/// <summary>Высота цоколя, которую оставляет ПРИСТЁГНУТЫЙ фасад
		/// (высота корпуса минус его высота); 0 — фасада нет.</summary>
		public int plinthMM;
		public int plinthMinMM;
		public int plinthMaxMM;
		/// <summary>Цоколь утоплен под фасад на столько мм.</summary>
		public int plinthSetbackMM;
		/// <summary>Высота ниши под МЕБЕЛЬНЫЙ цоколь — полосы ПЕРЕД основанием
		/// прибора. В проверку коллизий она не входит: там стоят цоколь и ножки
		/// модулей.</summary>
		public int plinthNicheMM;
		/// <summary>Высота собственного ОСНОВАНИЯ прибора (габарит минус дверца,
		/// 815 − 725 = 90). Им машина стоит на полу; опору под ним требует
		/// DWH-05.</summary>
		public int baseHeightMM;
		/// <summary>На столько мм основание утоплено вглубь от передней
		/// плоскости прибора — место для ног.</summary>
		public int baseSetbackMM;
		/// <summary>Монтажный зазор навески фасада, мм: фасад на кронштейнах
		/// считается пристёгнутым, даже если отстоит от прибора на столько.</summary>
		public float facadeMountGapMM;
		/// <summary>Дверца откинута (edit_elements {is_open}). Откидывается вниз
		/// вокруг нижней кромки, вместе с пристёгнутым фасадом.</summary>
		public bool isOpen;
	}

	[Serializable]
	public class WindowInfo
    {
        public string tint = "Clear";
        public int sillProtrusionMM;
        public string mode = string.Empty;
        public bool isOpen;
        public string attachedWallName = string.Empty;
    }

	[Serializable]
	public class DoorInfo
    {
        public string sashType = "Glass";
        public string mode = string.Empty;
        public bool isOpen;
        public string attachedWallName = string.Empty;
    }
}

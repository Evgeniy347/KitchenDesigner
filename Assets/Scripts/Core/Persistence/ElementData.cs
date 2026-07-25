using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Паз в файле проекта. Отдельный сериализуемый класс (а не сам
    /// GrooveSpec): формат файла не должен зависеть от того, что enum'ы —
    /// перечисления, а поля хранятся как int.</summary>
    [System.Serializable]
    public class GrooveEntry
    {
        public int kind;
        public int side;

        public GrooveEntry() { }

        public GrooveEntry(GrooveSpec spec)
        {
            kind = (int)spec.kind;
            side = (int)spec.side;
        }

        public GrooveSpec ToSpec() => new GrooveSpec((GrooveKind)kind, (GrooveSide)side);
    }

    [System.Serializable]
    public class ElementData
    {
        public string name = string.Empty;
        public int[] dimensionsMM = new int[3];
        public float[] position = new float[3];
        public float[] rotation = new float[4];
        public bool movable = true;
        public bool isWall = false;
        public bool isFacade = false;
        public bool isRadialShelf = false;
        public bool isTable = false;
        public bool isRadiusTable = false;
        public int legInsetMM = 100;
        public int gapLeft = 2;
        public int gapRight = 2;
        public int gapTop = 2;
        public int gapBottom = 2;
        public int groupId = 0;
        public string materialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
        public bool transparent = false;
        public int doorMode = 0;
        public bool doorOpen = false;
        public bool assembled = false;
        public int assembledFill = 0;
        public int grooveCount = AppConstants.ASSEMBLED_DEFAULT_GROOVES;
        // Радиус скругления угла радиусной полки; в файлах без этого поля
        // JsonUtility оставит дефолт.
        public int cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
		public bool isDrawer = false;
		public int drawerSystem = 0;   // 0 = GTV, 1 = Movento (DrawerSystem)
		public int drawerType = 0;
		public int drawerNominalLength = 350;
		public int drawerColor = 0;
		public int drawerInternalWidth = 400;
		public bool drawerIsDouble = false;
		public bool drawerIsUpper = false;
		public string drawerPairedName = "";
		public string drawerAttachedFacadeName = "";
		public int doubleDrawerState = 0;
		public bool isWindow = false;
		public int windowTint = 0;
		public int windowSillProtrusionMM = 50;
		public int windowDoorMode = 0;
		public bool windowIsOpen = false;
		public string windowAttachedWallName = "";
		public bool isDoor = false;
		public int doorSashType = 0;
		public int doorDoorMode = 0;
		public bool doorIsOpen = false;
		public string doorAttachedWallName = "";
		public bool isPillar = false;
		public int midHeightMM = 75;
		public bool isFloor = false;
		public bool isLightSource = false;
		// Параметры лампы. Инициализаторы = дефолты для старых сейвов без этих полей.
		public int lightTemperatureK = LightSourceElement.DEFAULT_TEMPERATURE_K;
		public int lightPowerW = LightSourceElement.DEFAULT_POWER_W;
		public int lightDiffusionPct = LightSourceElement.DEFAULT_DIFFUSION_PCT;
		public bool isPanel = false;
		// Пазы детали; в файлах без этого поля JsonUtility оставит пустой массив.
		public GrooveEntry[] grooves = System.Array.Empty<GrooveEntry>();

        public ElementData() { }

        /// <summary>Пазы из файла в виде спецификаций (устойчиво к null/мусору).</summary>
        public List<GrooveSpec> GrooveSpecs()
        {
            var result = new List<GrooveSpec>();
            if (grooves == null) return result;
            foreach (var g in grooves)
                if (g != null) result.Add(g.ToSpec());
            return result;
        }

        public static ElementData FromElement(KitchenElement element)
        {
            var d = new ElementData { name = element.PartName };
            var dims = element.DimensionsMM;
            d.dimensionsMM = new[] { dims.x, dims.y, dims.z };

			var drawer = element as DrawerElement;
			var wall = element.GetComponent<Wall>();
			var facade = element as FacadeElement;
			var radialShelf = element as RadialShelfElement;
			var radiusTable = element as RadiusTableElement;
			var tableEl2 = element as TableElement;
			var windowEl = element as WindowElement;
			var doorEl = element as DoorElement;
			var pillar = element as PillarElement;
			var panel = element as PanelElement;

            // Позицию/поворот пишем как ЛОГИЧЕСКУЮ, а не текущую (смещённую) позу:
            //  • полускрытая стена временно опущена вниз → берём FullPosition,
            //    иначе после загрузки она «утонет»;
            //  • открытая дверца отведена от петли → берём ЗАКРЫТУЮ позу, иначе
            //    после загрузки она отводится ещё раз и «уезжает».
            var p = wall != null ? wall.FullPosition
                  : windowEl != null ? windowEl.ClosedPosition
                  : doorEl != null ? doorEl.ClosedPosition
                  : facade != null ? facade.ClosedPosition
                  : drawer != null ? drawer.ClosedPosition
                  : element.transform.position;
            d.position = new[] { p.x, p.y, p.z };

            var r = windowEl != null ? windowEl.ClosedRotation
                  : doorEl != null ? doorEl.ClosedRotation
                  : facade != null ? facade.ClosedRotation
                  : drawer != null ? drawer.ClosedRotation
                  : element.transform.rotation;
            d.rotation = new[] { r.x, r.y, r.z, r.w };

            d.movable = element.Movable;
            d.isWall = wall != null;
            d.isFacade = facade != null;
            d.isRadialShelf = radialShelf != null;
            d.isRadiusTable = radiusTable != null;
            d.isTable = tableEl2 != null;
            if (tableEl2 != null)
                d.legInsetMM = tableEl2.LegInsetMM;
            else if (radiusTable != null)
                d.legInsetMM = radiusTable.LegInsetMM;
            d.legsMaterialId = tableEl2 != null ? tableEl2.LegsMaterialId
                : radiusTable != null ? radiusTable.LegsMaterialId : MaterialCatalog.DefaultId;
            d.cornerRadius = radialShelf != null ? radialShelf.CornerRadius : 0;

            if (facade != null)
            {
                d.gapLeft = facade.GapLeft;
                d.gapRight = facade.GapRight;
                d.gapTop = facade.GapTop;
                d.gapBottom = facade.GapBottom;
                d.doorMode = (int)facade.Mode;
                d.doorOpen = facade.IsOpen;
                if (facade is AssembledFacadeElement assembled)
                {
                    d.assembled = true;
                    d.assembledFill = (int)assembled.Fill;
                    d.grooveCount = assembled.GrooveCount;
                }
                else
                {
                    d.assembled = false;
                    d.assembledFill = 0;
                    d.grooveCount = 0;
                }
            }
            else if (panel != null)
            {
                // У ДВП/ХДФ зазоры значимы (входят в габарит), но дверцей она не является.
                d.gapLeft = panel.GapLeft;
                d.gapRight = panel.GapRight;
                d.gapTop = panel.GapTop;
                d.gapBottom = panel.GapBottom;
                d.doorMode = 0;
                d.doorOpen = false;
                d.assembled = false;
                d.assembledFill = 0;
                d.grooveCount = 0;
            }
            else
            {
                d.gapLeft = 0;
                d.gapRight = 0;
                d.gapTop = 0;
                d.gapBottom = 0;
                d.doorMode = 0;
                d.doorOpen = false;
                d.assembled = false;
                d.assembledFill = 0;
                d.grooveCount = 0;
            }

            if (drawer != null)
            {
                d.isDrawer = true;
                d.drawerSystem = (int)drawer.System;
                d.drawerType = (int)drawer.Type;
                d.drawerNominalLength = drawer.NominalLength;
                d.drawerColor = (int)drawer.Color;
                d.drawerInternalWidth = drawer.InternalWidth;
                d.drawerIsDouble = drawer.IsDouble;
                d.drawerIsUpper = drawer.IsUpperDrawer;
                d.drawerPairedName = drawer.PairedDrawerName ?? "";
                d.drawerAttachedFacadeName = drawer.AttachedFacadeName ?? "";
                d.doubleDrawerState = (int)drawer.DoubleState;
                // Одиночный ящик хранит открытость в doorOpen (как фасад);
                // у двойного состояние целиком описывает doubleDrawerState.
                d.doorOpen = drawer.IsOpen;
            }

			if (windowEl != null)
			{
				d.isWindow = true;
				d.windowTint = (int)windowEl.Tint;
				d.windowSillProtrusionMM = windowEl.SillProtrusionMM;
				d.windowDoorMode = (int)windowEl.Mode;
				d.windowIsOpen = windowEl.IsOpen;
				d.windowAttachedWallName = windowEl.AttachedWallName ?? "";
			}

			if (doorEl != null)
			{
				d.isDoor = true;
				d.doorSashType = (int)doorEl.SashType;
				d.doorDoorMode = (int)doorEl.Mode;
				d.doorIsOpen = doorEl.IsOpen;
				d.doorAttachedWallName = doorEl.AttachedWallName ?? "";
			}

			d.isPillar = pillar != null;
			d.isPanel = panel != null;
			d.isFloor = element is FloorElement;
			d.isLightSource = element is LightSourceElement;
			if (element is LightSourceElement lightEl)
			{
				d.lightTemperatureK = lightEl.TemperatureK;
				d.lightPowerW = lightEl.PowerW;
				d.lightDiffusionPct = lightEl.DiffusionPct;
			}
			d.midHeightMM = pillar != null ? pillar.MidHeightMM : PillarElement.MidHeightMM_Default;

			// Пазы есть только у базовой «Детали» — у остальных типов список пуст.
			var grooveSpecs = element.Grooves;
			d.grooves = new GrooveEntry[grooveSpecs.Count];
			for (int i = 0; i < grooveSpecs.Count; i++)
				d.grooves[i] = new GrooveEntry(grooveSpecs[i]);

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

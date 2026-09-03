using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementFactory
    {
        internal static IElementFactory Instance
        {
            get
            {
                if (GameContext.Services != null)
                    return GameContext.Services.ElementFactory;
                if (_fallback == null)
                    _fallback = new ElementFactoryInstance();
                return _fallback;
            }
            set => _fallback = value;
        }
        private static IElementFactory? _fallback;

        public static GameObject CreatePart(Vector3Int dimensionsMM, string name, Vector3 position) =>
            Instance.CreatePart(dimensionsMM, name, position);

        public static GameObject CreatePreset(int presetIndex, Vector3 position) =>
            Instance.CreatePreset(presetIndex, position);

        public static GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position) =>
            Instance.CreateWall(dimensionsMM, name, position);

        public static GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = FacadeElement.DEFAULT_GAP_MM, int gapRight = FacadeElement.DEFAULT_GAP_MM,
            int gapTop = FacadeElement.DEFAULT_GAP_MM, int gapBottom = FacadeElement.DEFAULT_GAP_MM,
            int gapFront = 0, int gapBack = 0) =>
            Instance.CreateFacade(dimensionsMM, name, position,
                gapLeft, gapRight, gapTop, gapBottom, gapFront, gapBack);

        public static GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name,
            Vector3 position, AssembledFill fill = AssembledFill.Blind) =>
            Instance.CreateAssembledFacade(dimensionsMM, name, position, fill);

        public static GameObject CreatePanel(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM,
            int gapFront = PanelElement.DEFAULT_GAP_MM, int gapBack = PanelElement.DEFAULT_GAP_MM) =>
            Instance.CreatePanel(dimensionsMM, name, position,
                gapLeft, gapRight, gapTop, gapBottom, gapFront, gapBack);

        public static GameObject CreateRadialShelf(int widthMM, int depthMM, int thicknessMM, int cornerRadiusMM, string name, Vector3 position) =>
            Instance.CreateRadialShelf(widthMM, depthMM, thicknessMM, cornerRadiusMM, name, position);

        public static GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color, int internalWidth, string name, Vector3 position,
            DrawerSystem system = DrawerSystem.Gtv) =>
            Instance.CreateDrawer(type, nominalLength, color, internalWidth, name, position, system);

        public static GameObject CreateTable(Vector3Int dimensionsMM, string name, Vector3 position) =>
            Instance.CreateTable(dimensionsMM, name, position);

		public static GameObject CreateRadiusTable(Vector3Int dimensionsMM, string name, Vector3 position) =>
			Instance.CreateRadiusTable(dimensionsMM, name, position);

		public static GameObject CreateStool(Vector3Int dimensionsMM, int cornerRadiusMM, string name, Vector3 position) =>
			Instance.CreateStool(dimensionsMM, cornerRadiusMM, name, position);

		public static GameObject CreateChair(Vector3Int dimensionsMM, int cornerRadiusMM, int seatHeightMM, string name, Vector3 position) =>
			Instance.CreateChair(dimensionsMM, cornerRadiusMM, seatHeightMM, name, position);

		public static GameObject CreateSofa(Vector3Int dimensionsMM, int cornerRadiusMM, int seatHeightMM, string name, Vector3 position) =>
			Instance.CreateSofa(dimensionsMM, cornerRadiusMM, seatHeightMM, name, position);

		public static GameObject CreatePouffe(Vector3Int dimensionsMM, int cornerRadiusMM, int seatThicknessMM, string name, Vector3 position) =>
			Instance.CreatePouffe(dimensionsMM, cornerRadiusMM, seatThicknessMM, name, position);

		public static GameObject CreateToilet(int seatHeightMM, string name, Vector3 position) =>
			Instance.CreateToilet(seatHeightMM, name, position);

		public static GameObject CreateWallHungToilet(int seatHeightMM, int flushPlateHeightMM, string name, Vector3 position) =>
			Instance.CreateWallHungToilet(seatHeightMM, flushPlateHeightMM, name, position);

		public static GameObject CreateBathtub(Vector3Int dimensionsMM, int rimWidthMM, int bowlDepthMM, int bowlRadiusMM, int bowlFilletMM, string name, Vector3 position) =>
			Instance.CreateBathtub(dimensionsMM, rimWidthMM, bowlDepthMM, bowlRadiusMM, bowlFilletMM, name, position);

		public static GameObject CreateBathMixer(BathMixerSpec spec, string name, Vector3 position) =>
			Instance.CreateBathMixer(spec, name, position);

		public static GameObject CreateShowerColumn(ShowerColumnSpec spec, string name, Vector3 position) =>
			Instance.CreateShowerColumn(spec, name, position);

		public static GameObject CreateSocket(WallDeviceSpec spec, string name, Vector3 position) =>
			Instance.CreateSocket(spec, name, position);

		public static GameObject CreateLightSwitch(WallDeviceSpec spec, bool isOn, string[]? lightNames, string name, Vector3 position) =>
			Instance.CreateLightSwitch(spec, isOn, lightNames, name, position);

		public static GameObject CreateBed(Vector3Int dimensionsMM, bool isDouble, bool hasHeadboard, string name, Vector3 position) =>
			Instance.CreateBed(dimensionsMM, isDouble, hasHeadboard, name, position);

		public static GameObject CreatePillar(int midHeightMM, string name, Vector3 position,
			int diameterMM = PillarElement.DiameterMM_Default) =>
			Instance.CreatePillar(midHeightMM, name, position, diameterMM);

		public static GameObject CreateFloor(Vector3Int dimensionsMM, string name, Vector3 position) =>
			Instance.CreateFloor(dimensionsMM, name, position);

		public static GameObject CreateLightSource(string name, Vector3 position) =>
			Instance.CreateLightSource(name, position);

		public static GameObject CreateSink(string name, Vector3 position) =>
			Instance.CreateSink(name, position);

		public static GameObject CreateCooktop(string name, Vector3 position, string model = "") =>
			Instance.CreateCooktop(name, position, model);

		public static GameObject CreateScrewLeg(string name, Vector3 position) =>
			Instance.CreateScrewLeg(name, position);

		public static GameObject CreateOven(string name, Vector3 position) =>
			Instance.CreateOven(name, position);

		public static GameObject CreateDishwasher(string name, Vector3 position) =>
			Instance.CreateDishwasher(name, position);

		public static GameObject CreateWindow(Vector3Int dimensionsMM, string name, Vector3 position,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50) =>
            Instance.CreateWindow(dimensionsMM, name, position, tint, sillProtrusionMM);

        public static GameObject CreateDoor(Vector3Int dimensionsMM, string name, Vector3 position,
            DoorSashType sashType = DoorSashType.Glass) =>
            Instance.CreateDoor(dimensionsMM, name, position, sashType);

        public static GameObject Duplicate(KitchenElement source) =>
            Instance.Duplicate(source);

        public static void DestroyPart(GameObject go) => Instance.DestroyPart(go);

        public static void DestroyFacade(GameObject go) => Instance.DestroyFacade(go);

        public static void DestroyElement(GameObject go) => Instance.DestroyElement(go);

        public static void ClearPools() => Instance.ClearPools();
    }
}

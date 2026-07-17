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
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2) =>
            Instance.CreateFacade(dimensionsMM, name, position, gapLeft, gapRight, gapTop, gapBottom);

        public static GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name,
            Vector3 position, AssembledFill fill = AssembledFill.Blind) =>
            Instance.CreateAssembledFacade(dimensionsMM, name, position, fill);

        public static GameObject CreateRadialShelf(int widthMM, int depthMM, int thicknessMM, int cornerRadiusMM, string name, Vector3 position) =>
            Instance.CreateRadialShelf(widthMM, depthMM, thicknessMM, cornerRadiusMM, name, position);

        public static GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color, int internalWidth, string name, Vector3 position) =>
            Instance.CreateDrawer(type, nominalLength, color, internalWidth, name, position);

        public static GameObject CreateTable(Vector3Int dimensionsMM, string name, Vector3 position) =>
            Instance.CreateTable(dimensionsMM, name, position);

		public static GameObject CreateRadiusTable(Vector3Int dimensionsMM, string name, Vector3 position) =>
			Instance.CreateRadiusTable(dimensionsMM, name, position);

		public static GameObject CreatePillar(int midHeightMM, string name, Vector3 position) =>
			Instance.CreatePillar(midHeightMM, name, position);

		public static GameObject CreateWindow(Vector3Int dimensionsMM, string name, Vector3 position,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50) =>
            Instance.CreateWindow(dimensionsMM, name, position, tint, sillProtrusionMM);

        public static GameObject CreateDoor(Vector3Int dimensionsMM, string name, Vector3 position) =>
            Instance.CreateDoor(dimensionsMM, name, position);

        public static GameObject Duplicate(KitchenElement source) =>
            Instance.Duplicate(source);

        public static void DestroyPart(GameObject go) => Instance.DestroyPart(go);

        public static void DestroyFacade(GameObject go) => Instance.DestroyFacade(go);

        public static void DestroyElement(GameObject go) => Instance.DestroyElement(go);

        public static void ClearPools() => Instance.ClearPools();
    }
}

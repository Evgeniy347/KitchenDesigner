using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IElementFactory
    {
        GameObject CreatePart(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreatePreset(int presetIndex, Vector3 position);
        GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = FacadeElement.DEFAULT_GAP_MM, int gapRight = FacadeElement.DEFAULT_GAP_MM,
            int gapTop = FacadeElement.DEFAULT_GAP_MM, int gapBottom = FacadeElement.DEFAULT_GAP_MM,
            int gapFront = 0, int gapBack = 0);
        GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            AssembledFill fill = AssembledFill.Blind);
        GameObject CreatePanel(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM,
            int gapFront = PanelElement.DEFAULT_GAP_MM, int gapBack = PanelElement.DEFAULT_GAP_MM);
        GameObject CreateRadialShelf(int widthMM, int depthMM, int thicknessMM, int cornerRadiusMM, string name, Vector3 position);
        GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color, int internalWidth, string name, Vector3 position,
            DrawerSystem system = DrawerSystem.Gtv);
        GameObject CreateTable(Vector3Int dimensionsMM, string name, Vector3 position);
		GameObject CreateRadiusTable(Vector3Int dimensionsMM, string name, Vector3 position);
		GameObject CreateStool(Vector3Int dimensionsMM, int cornerRadiusMM, string name, Vector3 position);
		GameObject CreateChair(Vector3Int dimensionsMM, int cornerRadiusMM, int seatHeightMM, string name, Vector3 position);
		GameObject CreateSofa(Vector3Int dimensionsMM, int cornerRadiusMM, int seatHeightMM, string name, Vector3 position);
		GameObject CreatePouffe(Vector3Int dimensionsMM, int cornerRadiusMM, int seatThicknessMM, string name, Vector3 position);
		GameObject CreateBed(Vector3Int dimensionsMM, bool isDouble, bool hasHeadboard, string name, Vector3 position);
		GameObject CreatePillar(int midHeightMM, string name, Vector3 position,
			int diameterMM = PillarElement.DiameterMM_Default);
		GameObject CreateFloor(Vector3Int dimensionsMM, string name, Vector3 position);
		GameObject CreateLightSource(string name, Vector3 position);
		GameObject CreateSink(string name, Vector3 position);
		GameObject CreateCooktop(string name, Vector3 position, string model = "");
		GameObject CreateScrewLeg(string name, Vector3 position);

		GameObject CreateOven(string name, Vector3 position);
		GameObject CreateDishwasher(string name, Vector3 position);
		GameObject CreateWindow(Vector3Int dimensionsMM, string name, Vector3 position,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50);
        GameObject CreateDoor(Vector3Int dimensionsMM, string name, Vector3 position,
            DoorSashType sashType = DoorSashType.Glass);
        GameObject Duplicate(KitchenElement source);
        void DestroyPart(GameObject go);
        void DestroyFacade(GameObject go);
        void DestroyElement(GameObject go);
        void ClearPools();
    }
}

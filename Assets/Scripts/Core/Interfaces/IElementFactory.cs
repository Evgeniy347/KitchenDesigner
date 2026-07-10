using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IElementFactory
    {
        GameObject CreatePart(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreatePreset(int presetIndex, Vector3 position);
        GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2);
        GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            AssembledFill fill = AssembledFill.Blind);
        GameObject CreateRadialShelf(int radiusMM, int thicknessMM, string name, Vector3 position);
        GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color, int internalWidth, string name, Vector3 position);
        GameObject Duplicate(KitchenElement source);
        void DestroyPart(GameObject go);
        void DestroyFacade(GameObject go);
        void DestroyElement(GameObject go);
        void ClearPools();
    }
}

using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IElementFactory
    {
        GameObject CreateBoard(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreatePreset(int presetIndex, Vector3 position);
        GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position);
        GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2);
        GameObject Duplicate(KitchenElement source);
        void DestroyBoard(GameObject go);
        void DestroyFacade(GameObject go);
        void DestroyElement(GameObject go);
        void ClearPools();
    }
}

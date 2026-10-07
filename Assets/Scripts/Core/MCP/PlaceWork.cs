using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceWork
    {
        public readonly KitchenElement Element;
        public readonly bool IsNew;
        public readonly Vector3 PositionBefore;
        public readonly Quaternion RotationBefore;

        public PlaceWork(KitchenElement element, bool isNew)
        {
            Element = element;
            IsNew = isNew;
            PositionBefore = element.transform.position;
            RotationBefore = element.transform.rotation;
        }

        public void Undo()
        {
            if (IsNew)
            {
                McpSceneRollback.Discard(Element);
                return;
            }
            Element.transform.position = PositionBefore;
            Element.transform.rotation = RotationBefore;
        }
    }
}

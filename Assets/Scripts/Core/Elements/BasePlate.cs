using UnityEngine;

namespace KitchenDesigner.Core
{
    public class BasePlate : MonoBehaviour
    {
        private KitchenElement? _element;
        public KitchenElement Element => _element!;

        public static BasePlate Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BasePlate";

            var element = go.AddComponent<KitchenElement>();
            element.PartName = "BasePlate";
            element.DimensionsMM = new Vector3Int(AppConstants.GROUND_QUAD_SIZE_MM,
                AppConstants.BOARD_THICKNESS_DEFAULT, AppConstants.GROUND_QUAD_SIZE_MM);
            go.transform.position = new Vector3(0,
                -AppConstants.HalfHeightUnits(AppConstants.BOARD_THICKNESS_DEFAULT), 0);
            go.tag = "Floor";

            var plate = go.AddComponent<BasePlate>();
            plate._element = element;
            return plate;
        }
    }
}

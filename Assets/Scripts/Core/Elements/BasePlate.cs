using UnityEngine;

namespace KitchenDesigner.Core
{
    public class BasePlate : MonoBehaviour
    {
        public const int PLATE_SIZE = 3000;

        private KitchenElement? _element;
        public KitchenElement Element => _element!;

        public static BasePlate Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BasePlate";

            var element = go.AddComponent<KitchenElement>();
            element.PartName = "BasePlate";
            element.DimensionsMM = new Vector3Int(PLATE_SIZE, AppConstants.BOARD_THICKNESS_DEFAULT, PLATE_SIZE);
            go.transform.position = new Vector3(0,
                -AppConstants.BOARD_THICKNESS_DEFAULT * 0.5f * AppConstants.MM_TO_UNITS, 0);

            var renderer = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.6f, 0.6f, 0.6f);
                renderer.material = mat;
            }

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            go.tag = "Floor";

            var plate = go.AddComponent<BasePlate>();
            plate._element = element;

            return plate;
        }

        private void Awake()
        {
            if (_element == null)
                _element = GetComponent<KitchenElement>();
        }
    }
}

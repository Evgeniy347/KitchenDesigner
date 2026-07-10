using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (KitchenSettings.Instance != null)
                KitchenSettings.Instance.Load();
        }

        private void Start()
        {
            if (FindAnyObjectByType<BasePlate>() == null)
                BasePlate.Create();

            if (FindAnyObjectByType<CameraController>() == null)
                gameObject.AddComponent<CameraController>();

            if (FindAnyObjectByType<SelectionManager>() == null)
                gameObject.AddComponent<SelectionManager>();

            if (FindAnyObjectByType<ElementMover>() == null)
                gameObject.AddComponent<ElementMover>();

            if (FindAnyObjectByType<InputCapture>() == null)
                gameObject.AddComponent<InputCapture>();

            if (FindAnyObjectByType<ElementHighlighter>() == null)
                gameObject.AddComponent<ElementHighlighter>();

            Debug.Log("[Bootstrap] Kitchen Designer initialized");
        }
    }
}

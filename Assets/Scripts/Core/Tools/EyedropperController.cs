using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Tools
{
    [DefaultExecutionOrder(RunsBeforeSelectionAndMove)]
    public class EyedropperController : MonoBehaviour
    {
        public const int RunsBeforeSelectionAndMove = -50;

        public static EyedropperController? Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!EyedropperMode.Active) return;
            if (PlacementController.IsActive) return;

            if (Input.GetKeyDown(KeyCode.Escape) && TryExitFromEscape())
                return;

            var cam = Camera.main;
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                ApplyAt(cam.ScreenPointToRay(Input.mousePosition), ShiftHeld());
        }

        public static void PickAt(Ray ray, bool shiftHeld)
        {
            var element = PickableUnderRay(ray, shiftHeld, out RaycastHit hit);
            if (element == null) return;
            PickFrom(element, hit.point, hit.normal);
        }

        internal static void PickFrom(KitchenElement element, Vector3 point, Vector3 normal)
        {
            EyedropperMode.Pick(
                TextureOverlayPicker.TryPick(element, point, normal, out _, out string overlayId)
                    ? overlayId
                    : MaterialManager.MaterialIdOf(element, VisibleDecorSlotOf(element)));
        }

        public static void ApplyAt(Ray ray, bool shiftHeld)
        {
            if (string.IsNullOrEmpty(EyedropperMode.PickedMaterialId)) return;

            var element = PickableUnderRay(ray, shiftHeld, out RaycastHit hit);
            if (element == null) return;
            ApplyTo(element, hit.point, hit.normal);
        }

        internal static void ApplyTo(KitchenElement element, Vector3 point, Vector3 normal)
        {
            string? picked = EyedropperMode.PickedMaterialId;
            if (string.IsNullOrEmpty(picked)) return;

            if (TextureOverlayPicker.TryPick(element, point, normal, out int index, out _))
            {
                var after = new List<TextureOverlaySpec>(element.TextureOverlays);
                if (after[index].MaterialId == picked) return;
                after[index] = after[index].WithMaterial(picked);
                CommandStack.Execute(new SetListCommand<TextureOverlaySpec>(
                    $"Textures {element.PartName}", element.TextureOverlays, after, element.SetTextureOverlays));
            }
            else
            {
                var slot = VisibleDecorSlotOf(element);
                if (MaterialManager.MaterialIdOf(element, slot) == picked) return;
                CommandStack.Execute(new SetMaterialCommand(element, slot, picked!));
            }

            if (SelectionManager.Instance != null) SelectionManager.Instance.RefreshHighlight(element);
        }

        private static KitchenElement? PickableUnderRay(Ray ray, bool shiftHeld, out RaycastHit hit)
        {
            var element = SelectionManager.RaycastTransparentAware(ray, shiftHeld, out hit);
            if (element == null) return null;
            if (element.GetComponent<BasePlate>() != null) return null;
            if (!EditModeManager.IsInteractable(element)) return null;
            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element)) return null;
            return element;
        }

        internal static MaterialSlot VisibleDecorSlotOf(KitchenElement element)
            => element is IHasTwoDecorSlots ? MaterialSlot.Tabletop : MaterialSlot.Base;

        private static bool ShiftHeld() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        internal bool TryExitFromEscape()
        {
            if (!OwnsEscape()) return false;
            EyedropperMode.SetActive(false);
            return true;
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                Eyedropping = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
            }) == EscapeOwner.Eyedropper;
    }
}

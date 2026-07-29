using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Tools
{
    /// <summary>Ввод режима «Пипетка»: ЛКМ красит подобранным декором, Esc
    /// выходит из режима. ПКМ (забор декора) приходит сюда из
    /// <see cref="CameraController"/> через <see cref="PickAt"/> — только он
    /// отличает клик правой кнопкой от орбиты.
    ///
    /// DefaultExecutionOrder=-50 — как у MeasureController: раньше
    /// SelectionManager/ElementMover, чтобы состояние текущего кадра было готово
    /// до их проверок режима.</summary>
    [DefaultExecutionOrder(-50)]
    public class EyedropperController : MonoBehaviour
    {
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EyedropperMode.SetActive(false);
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                ApplyAt(cam.ScreenPointToRay(Input.mousePosition), ShiftHeld());
        }

        /// <summary>Взять декор с объекта под лучом. Накладка текстуры под
        /// курсором приоритетнее базового свойства «Текстура»: человек целится в
        /// то, что видит.</summary>
        public static void PickAt(Ray ray, bool shiftHeld)
        {
            var element = Target(ray, shiftHeld, out RaycastHit hit);
            if (element == null) return;

            EyedropperMode.Pick(
                TextureOverlayPicker.TryPick(element, hit.point, hit.normal, out _, out string overlayId)
                    ? overlayId
                    : MaterialManager.MaterialIdOf(element, SlotOf(element)));
        }

        /// <summary>Надеть подобранный декор на объект под лучом. Попали в
        /// накладку — красится именно она (иначе накладка сверху закрыла бы
        /// результат, и правка выглядела бы несработавшей), мимо — базовое
        /// свойство «Текстура».</summary>
        public static void ApplyAt(Ray ray, bool shiftHeld)
        {
            string? picked = EyedropperMode.PickedMaterialId;
            if (string.IsNullOrEmpty(picked)) return;

            var element = Target(ray, shiftHeld, out RaycastHit hit);
            if (element == null) return;

            if (TextureOverlayPicker.TryPick(element, hit.point, hit.normal, out int index, out _))
            {
                var after = new List<TextureOverlaySpec>(element.TextureOverlays);
                if (after[index].MaterialId == picked) return;
                after[index] = after[index].WithMaterial(picked);
                CommandStack.Execute(new SetTextureOverlaysCommand(element, element.TextureOverlays, after));
            }
            else
            {
                var slot = SlotOf(element);
                if (MaterialManager.MaterialIdOf(element, slot) == picked) return;
                CommandStack.Execute(new SetMaterialCommand(element, slot, picked!));
            }

            if (SelectionManager.Instance != null) SelectionManager.Instance.RefreshHighlight(element);
        }

        /// <summary>Что под лучом: опорная плита и заблокированное режимом
        /// редактора пипеткой не берутся и не красятся.</summary>
        private static KitchenElement? Target(Ray ray, bool shiftHeld, out RaycastHit hit)
        {
            var element = SelectionManager.RaycastTransparentAware(ray, shiftHeld, out hit);
            if (element == null) return null;
            if (element.GetComponent<BasePlate>() != null) return null;
            if (!EditModeManager.IsInteractable(element)) return null;
            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element)) return null;
            return element;
        }

        /// <summary>У стола свойство «Текстура» скрыто, а видимая поверхность —
        /// столешница; её декор пипетка и берёт (так же решает окно свойств,
        /// см. ContextMenuUI.SlotFor).</summary>
        private static MaterialSlot SlotOf(KitchenElement element)
            => element is TableElement || element is RadiusTableElement
                ? MaterialSlot.Tabletop
                : MaterialSlot.Base;

        private static bool ShiftHeld() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}

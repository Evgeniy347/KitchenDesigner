using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.UI
{
    public sealed class PointerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action? Enter;
        public System.Action? Exit;

        public static PointerHover Attach(GameObject target,
            System.Action? onEnter, System.Action? onExit)
        {
            var self = target.GetComponent<PointerHover>() ?? target.AddComponent<PointerHover>();
            self.Enter = onEnter;
            self.Exit = onExit;
            return self;
        }

        public static PointerHover Attach(Component target,
            System.Action? onEnter, System.Action? onExit)
            => Attach(target.gameObject, onEnter, onExit);

        public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
    }
}

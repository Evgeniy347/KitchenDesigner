using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Вход и выход курсора — и НИЧЕГО больше.
    ///
    /// Нужен везде, где наведение на элемент интерфейса подсвечивает сторону
    /// детали в сцене (SideHighlighter): полосы схемы кромок, пункты списка
    /// стороны у накладок текстур, поля зазоров.
    ///
    /// Именно отдельный компонент, а не <see cref="EventTrigger"/>: тот
    /// реализует ВСЕ интерфейсы событий, включая IScrollHandler, поэтому колесо
    /// над объектом доставалось ему (а он ничего с ним не делал) и до ScrollRect
    /// списка уже не доходило. При списке из трёх десятков декоров это значило
    /// «видно семь, остальные недоступны».</summary>
    public sealed class PointerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action? Enter;
        public System.Action? Exit;

        /// <summary>Повесить обработку наведения на объект (повторный вызов
        /// переписывает обработчики, а не плодит компоненты).</summary>
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

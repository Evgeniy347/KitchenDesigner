using System;

namespace KitchenDesigner.Core.Tools
{
    /// <summary>Режим «Пипетка»: ПКМ берёт декор с объекта под курсором, ЛКМ
    /// красит им следующий. Пока режим включён, мышь принадлежит только пипетке —
    /// выделение, перетаскивание, ручки ресайза и контекстное меню отключены
    /// (см. <see cref="ToolMode"/>).
    ///
    /// Состояние рантайм-only, как у <see cref="Measure.MeasureMode"/>, в проект
    /// не сохраняется. Но, в отличие от рулетки, выход из режима подобранный
    /// декор НЕ стирает: обычный сценарий — взял декор, отвлёкся на камеру,
    /// вернулся и докрасил.</summary>
    public static class EyedropperMode
    {
        public static bool Active { get; private set; }

        /// <summary>Декор «в пипетке» — id из каталога. null, пока ничего не
        /// подобрали: ЛКМ в этом состоянии не делает ничего.</summary>
        public static string? PickedMaterialId { get; private set; }

        /// <summary>Срабатывает при смене состояния режима И при новом заборе
        /// декора (кнопка тулбара показывает образец подобранного).</summary>
        public static event Action? Changed;

        public static void Toggle() => SetActive(!Active);

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;

            if (on)
            {
                // Два режима-захватчика мыши одновременно не имеют смысла:
                // включение пипетки гасит рулетку.
                Measure.MeasureMode.SetActive(false);
                // Иначе у выделенной детали останутся ручки ресайза, ловящие
                // клики раньше пипетки. Сравнение через Unity-оператор (не `?.`):
                // после выгрузки сцены Instance — уничтоженный объект.
                if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();
            }

            Changed?.Invoke();
        }

        /// <summary>Положить в пипетку декор (null — опустошить).</summary>
        public static void Pick(string? materialId)
        {
            if (PickedMaterialId == materialId) return;
            PickedMaterialId = materialId;
            Changed?.Invoke();
        }

        /// <summary>Сброс глобального состояния для изоляции тестов
        /// (см. правила снапшотов в AGENTS.md).</summary>
        public static void Reset()
        {
            Active = false;
            PickedMaterialId = null;
        }
    }
}

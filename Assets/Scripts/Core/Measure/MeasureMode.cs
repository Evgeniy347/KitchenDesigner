using System;

namespace KitchenDesigner.Core.Measure
{
    /// <summary>Режим «Рулетка»: мышь измеряет расстояния между вершинами и
    /// больше ничего не делает — выделение, перетаскивание и ручки ресайза на
    /// время режима отключены. Состояние рантайм-only (как PhotoMode.Active),
    /// в проект не сохраняется; выход стирает все замеры.</summary>
    public static class MeasureMode
    {
        public static bool Active { get; private set; }

        /// <summary>Срабатывает при любой смене состояния (вход/выход).</summary>
        public static event Action? Changed;

        public static void Toggle() => SetActive(!Active);

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;

            // Иначе у выделенной детали останутся висеть ручки ресайза,
            // перехватывающие клики поверх вершин. Сравнение через Unity-оператор
            // (не `?.`): после выгрузки сцены Instance — уничтоженный объект.
            if (on)
            {
                if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();
            }
            else
            {
                MeasureStore.Clear();
            }

            Changed?.Invoke();
        }

        /// <summary>Сброс глобального состояния для изоляции тестов
        /// (см. правила снапшотов в AGENTS.md).</summary>
        public static void Reset()
        {
            Active = false;
            MeasureStore.Clear();
        }
    }
}

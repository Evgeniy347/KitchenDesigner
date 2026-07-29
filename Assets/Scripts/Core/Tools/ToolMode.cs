namespace KitchenDesigner.Core.Tools
{
    /// <summary>Общий вопрос «мышь сейчас занята инструментом?».
    ///
    /// Рулетка и пипетка забирают мышь себе целиком, и каждой системе, которая
    /// эту мышь читает (выделение, перенос детали, ручки ресайза, контекстное
    /// меню камеры), нужен один и тот же запрет. Раньше все они спрашивали
    /// напрямую MeasureMode.Active — с появлением второго такого режима условие
    /// живёт здесь, чтобы третий не пришлось снова разносить по пяти файлам.</summary>
    public static class ToolMode
    {
        public static bool MouseCaptured =>
            Measure.MeasureMode.Active || EyedropperMode.Active;
    }
}

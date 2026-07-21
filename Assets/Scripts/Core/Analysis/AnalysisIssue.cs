namespace KitchenDesigner.Core.Analysis
{
    /// <summary>Уровень серьёзности проблемы анализа. Error — коллизии (физически
    /// недопустимо), Warning — потенциальные дефекты сборки (зазоры, непосадка,
    /// нет фасада). Info зарезервирован.</summary>
    public enum IssueLevel
    {
        Error,
        Warning,
        Info,
    }

    /// <summary>Одна строка таблицы окна анализа ошибок: уровень, код, деталь и
    /// человекочитаемый текст. Неизменяемая — окно только читает результат
    /// <see cref="SceneAnalyzer.Analyze"/>. Target/Secondary — детали, к которым
    /// относится проблема (для клика по строке → выделение в сцене).</summary>
    public readonly struct AnalysisIssue
    {
        public readonly IssueLevel Level;
        /// <summary>Стабильный код вида «COL-01» (колонка и фильтр по кодам).</summary>
        public readonly string Code;
        /// <summary>Деталь(и), к которым относится проблема.</summary>
        public readonly string Detail;
        /// <summary>Текст ошибки для человека.</summary>
        public readonly string Message;
        /// <summary>Основная деталь для выделения по клику (может быть null).</summary>
        public readonly KitchenElement? Target;
        /// <summary>Вторая деталь пары (для пересечений/зазоров); иначе null.</summary>
        public readonly KitchenElement? Secondary;

        public AnalysisIssue(IssueLevel level, string code, string detail, string message,
            KitchenElement? target = null, KitchenElement? secondary = null)
        {
            Level = level;
            Code = code;
            Detail = detail;
            Message = message;
            Target = target;
            Secondary = secondary;
        }
    }
}

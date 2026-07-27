namespace KitchenDesigner.Core
{
    /// <summary>
    /// Свободный текст «инструкции проекта» — единый источник соглашений проекта
    /// (толщины несущих/перегородок, толщина ЛДСП, зазоры фасадов, правила посадки
    /// и т.п.). Хранится в файле проекта (ProjectData.projectInstructions), читается
    /// и пишется через MCP (get/set_project_instructions), выдаётся в get_status.
    /// Рантайм-холдер: единственная актуальная копия текста в памяти приложения.
    /// </summary>
    public static class ProjectInstructions
    {
        public static string Text { get; set; } = "";

        public static void Reset() => Text = "";
    }
}

namespace KitchenDesigner.Core
{
    public static class NewerVersionStrings
    {
        public const string Title = "Проект новее программы";

        public const string OpenButton = "Открыть";

        public const string CancelButton = "Отмена";

        public static string Message(string fileVersion, string appVersion) =>
            $"Проект сохранён более новой версией программы ({fileVersion} против вашей {appVersion}). "
            + "Объекты, которых эта версия не знает, будут показаны обычными деталями и помечены "
            + "нарушением; при сохранении они не потеряются.";
    }
}

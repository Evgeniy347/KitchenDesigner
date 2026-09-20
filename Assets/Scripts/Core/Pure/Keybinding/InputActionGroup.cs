namespace KitchenDesigner.Core.Keybinding
{
    public enum InputActionGroup
    {
        Camera,
        SelectionAndEditing,
        Catalog,
        Views,
        WindowsAndHelp,
        Diagnostics,
    }

    public static class InputActionGroupTitles
    {
        public static readonly string[] All =
        {
            "Камера", "Выделение и правка", "Каталог", "Виды", "Окна и справка", "Диагностика",
        };

        public static string Of(InputActionGroup group)
        {
            int index = (int)group;
            return index >= 0 && index < All.Length ? All[index] : All[(int)InputActionGroup.Camera];
        }
    }
}

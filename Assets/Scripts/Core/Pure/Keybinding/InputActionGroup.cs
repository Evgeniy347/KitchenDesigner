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
        Mouse,
        Levels,
    }

    public static class InputActionGroupTitles
    {
        private static readonly LocalizedCache<string[]> AllCache =
            new LocalizedCache<string[]>(() => new string[] {
            Loc.T("input.group.camera"), Loc.T("input.group.selectionEditing"), Loc.T("input.group.catalog"), Loc.T("input.group.views"), Loc.T("input.group.windowsHelp"), Loc.T("input.group.diagnostics"),
            Loc.T("input.group.mouse"), Loc.T("input.group.levels"),
        });

        public static string[] All => AllCache.Value;

        public static string Of(InputActionGroup group)
        {
            int index = (int)group;
            return index >= 0 && index < All.Length ? All[index] : All[(int)InputActionGroup.Camera];
        }
    }
}

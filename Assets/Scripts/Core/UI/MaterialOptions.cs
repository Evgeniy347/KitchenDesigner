using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal static class MaterialOptions
    {
        public const int UnknownMaterialIndex = 0;

        public static int IndexOf(string materialId)
        {
            var all = MaterialCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].id == materialId) return i;
            return UnknownMaterialIndex;
        }

        public static List<string> DisplayNames()
        {
            var names = new List<string>();
            foreach (var m in MaterialCatalog.All) names.Add(m.displayName);
            return names;
        }

        public static void Fill(TMP_Dropdown? dropdown)
        {
            if (dropdown == null) return;
            var opts = new List<TMP_Dropdown.OptionData>();
            foreach (var m in MaterialCatalog.All)
                opts.Add(new TMP_Dropdown.OptionData(m.displayName));
            dropdown.options = opts;
            UIFactory.FitDropdownItems(dropdown);
        }
    }
}

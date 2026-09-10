namespace KitchenDesigner.Core
{
    public static class HoverAnchor
    {
        public static bool IsGone(KitchenElement? element)
        {
            if (element == null) return true;
            if (!element.gameObject.activeInHierarchy) return true;
            if (!SceneVisibility.AnyRendererEnabled(element)) return true;
            return !InTheProject(element);
        }

        private static bool InTheProject(KitchenElement element)
        {
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (ReferenceEquals(all[i], element)) return true;
            return false;
        }
    }
}

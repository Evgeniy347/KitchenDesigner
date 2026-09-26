namespace KitchenDesigner.Core
{
    public enum PageKeyOwner
    {
        LevelSwitch,
        ErrorPanel,
    }

    public struct PageKeyClaims
    {
        public bool ErrorPanelOpenWithIssues;
    }

    public static class PageKeyOwnership
    {
        public static PageKeyOwner Resolve(PageKeyClaims claims)
        {
            if (claims.ErrorPanelOpenWithIssues) return PageKeyOwner.ErrorPanel;
            return PageKeyOwner.LevelSwitch;
        }
    }
}

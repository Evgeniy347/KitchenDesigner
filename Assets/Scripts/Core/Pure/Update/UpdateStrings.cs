namespace KitchenDesigner.Core.Update
{
    public static class UpdateStrings
    {
        public static string UpdateTitle => Loc.T("update.available.title");
        public static string UpdateMessage => Loc.T("update.available.message");
        public static string UpdateAcceptButton => Loc.T("update.available.accept");
        public static string UpdateCancelButton => Loc.T("common.cancel");
    }

    public enum StatusLevel { Info, Success, Warning, Error }
}

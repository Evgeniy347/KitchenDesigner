namespace KitchenDesigner.Core.UI
{
    internal static class PhotoLookMigrationNotice
    {
        internal static string Text => Loc.T("toast.photoLookReset");

        private const float Seconds = 8f;

        public static void ShowIfPending()
        {
            var s = KitchenSettings.Instance;
            if (s == null || !s.ConsumePhotoLookMigratedNotice()) return;
            ToastNotification.ShowIfAvailable(Text, Seconds);
        }
    }
}

namespace KitchenDesigner.Core.UI
{
    internal static class PhotoLookMigrationNotice
    {
        internal const string Text =
            "Настройки фоторежима сохранены в версии, где постобработка была отключена, "
            + "и ни на что не влияли. Они возвращены к значениям по умолчанию.";

        private const float Seconds = 8f;

        public static void ShowIfPending()
        {
            var s = KitchenSettings.Instance;
            if (s == null || !s.ConsumePhotoLookMigratedNotice()) return;
            ToastNotification.ShowIfAvailable(Text, Seconds);
        }
    }
}

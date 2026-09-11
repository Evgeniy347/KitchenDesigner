namespace KitchenDesigner.Core
{
    public static class HoverPreviewGate
    {
        public static void HideAll()
        {
            ScenePreview.Leave();
            PartHighlighter.Hide();
            HoverTint.Hide();
        }

        public static void Sync()
        {
            ScenePreview.Sync();
            PartHighlighter.Sync();
            HoverTint.Sync();
        }
    }
}

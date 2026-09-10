namespace KitchenDesigner.Core
{
    public sealed class ScenePreviewState
    {
        public string? ShownKey { get; private set; }

        public bool IsShowing => ShownKey != null;

        public ScenePreviewStep Hover(string? key)
        {
            if (string.IsNullOrEmpty(key)) return Leave();
            if (string.Equals(ShownKey, key, System.StringComparison.Ordinal))
                return ScenePreviewStep.None;

            var step = ShownKey == null ? ScenePreviewStep.Build : ScenePreviewStep.Rebuild;
            ShownKey = key;
            return step;
        }

        public ScenePreviewStep Leave()
        {
            if (ShownKey == null) return ScenePreviewStep.None;
            ShownKey = null;
            return ScenePreviewStep.Clear;
        }

        public ScenePreviewStep Commit()
        {
            ShownKey = null;
            return ScenePreviewStep.Commit;
        }
    }
}

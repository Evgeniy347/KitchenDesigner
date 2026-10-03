using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    [DisallowMultipleComponent]
    public sealed class RightToLeftLabel : MonoBehaviour, ITextPreprocessor
    {
        private TMP_Text? _text;
        private bool _keepAlignment;

        public static void AttachIfNeeded(TMP_Text text)
        {
            if (!Loc.IsRightToLeft) return;
            Attach(text, keepAlignment: false);
        }

        public static void AttachKeepingAlignment(TMP_Text text) => Attach(text, keepAlignment: true);

        public static string Rendered(TMP_Text text) =>
            text.textPreprocessor == null ? text.text : text.textPreprocessor.PreprocessText(text.text);

        public string PreprocessText(string text) => RightToLeftLayout.Prepare(text);

        private static void Attach(TMP_Text text, bool keepAlignment)
        {
            if (!text.TryGetComponent<RightToLeftLabel>(out var label))
                label = text.gameObject.AddComponent<RightToLeftLabel>();
            label._keepAlignment = keepAlignment;
            label.Bind(text);
        }

        private void Awake()
        {
            if (TryGetComponent<TMP_Text>(out var text)) Bind(text);
        }

        private void Start()
        {
            if (_keepAlignment) return;
            if (_text is { } text && text.horizontalAlignment == HorizontalAlignmentOptions.Left)
                text.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }

        private void Bind(TMP_Text text)
        {
            _text = text;
            text.textPreprocessor = this;
            text.isRightToLeftText = true;
            text.fontFeatures.Clear();
        }
    }
}

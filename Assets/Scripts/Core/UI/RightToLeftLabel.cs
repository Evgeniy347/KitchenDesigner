using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    [DisallowMultipleComponent]
    public sealed class RightToLeftLabel : MonoBehaviour, ITextPreprocessor
    {
        private TMP_Text? _text;

        public static void AttachIfNeeded(TMP_Text text)
        {
            if (!Loc.IsRightToLeft) return;
            if (!text.TryGetComponent<RightToLeftLabel>(out var label))
                label = text.gameObject.AddComponent<RightToLeftLabel>();
            label.Bind(text);
        }

        public static string Rendered(TMP_Text text) =>
            text.textPreprocessor == null ? text.text : text.textPreprocessor.PreprocessText(text.text);

        public string PreprocessText(string text) => RightToLeftLayout.Prepare(text);

        private void Awake()
        {
            if (TryGetComponent<TMP_Text>(out var text)) Bind(text);
        }

        private void Start()
        {
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

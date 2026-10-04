using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class ProjectInstructionsPanelUI : MonoBehaviour, IProjectWindow
    {
        private static readonly Vector2 Size = new Vector2(640f, 560f);

        private WindowChrome? _chrome;
        private TMP_InputField? _text;
        private TMP_Text? _unsaved;

        public string WindowId => "projectInstructions";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;

        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "ProjectInstructionsPanel",
                Loc.T("window.instructions.title"), Size, new WindowChromeOptions
                {
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });
            ProjectWindows.Register(this);

            var body = _chrome.CreateBody();
            float width = _chrome.BodyWidth;
            var stack = new VerticalStack(body.Content, width);
            var intro = stack.Text("PiHint", Loc.T("window.instructions.intro"), UIStyle.FontSmall,
                UIStyle.TextSecondary);
            stack.Gap(UIStyle.Space2);

            _text = UIFactory.CreateInputField("PiText", body.Content, ProjectInstructions.Text,
                Vector2.zero, new Vector2(width, body.VisibleHeight - stack.Height - WindowBody.BottomPadPx));
            _text.lineType = TMP_InputField.LineType.MultiLineNewline;
            _text.textComponent!.alignment = TextAlignmentOptions.TopLeft;
            _text.textComponent.fontSize = UIStyle.FontMono;
            stack.Place((RectTransform)_text.transform);
            _text.onValueChanged.AddListener(_ => SyncUnsaved());
            body.Fit();
            intro.raycastTarget = false;

            var footer = _chrome.Footer!;
            _unsaved = footer.AddLeftText("PiUnsaved", Loc.T("window.instructions.unsaved"));
            footer.AddSecondary("PiCancel", Loc.T("common.cancel"), () => SetVisible(false));
            footer.AddPrimary("PiSave", Loc.T("window.instructions.save"), Save);
            SyncUnsaved();
            _chrome.Panel.gameObject.SetActive(false);
        }

        private void OnDestroy() => ProjectWindows.Unregister(this);

        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            if (_chrome == null) return;
            if (visible && _text != null) _text.text = ProjectInstructions.Text;
            SyncUnsaved();
            _chrome.Panel.gameObject.SetActive(visible);
        }

        internal bool HasUnsavedChanges => _text != null && _text.text != ProjectInstructions.Text;

        private void SyncUnsaved()
        {
            if (_unsaved != null) _unsaved.gameObject.SetActive(HasUnsavedChanges);
        }

        private void Save()
        {
            ProjectInstructions.Text = _text?.text ?? "";
            SetVisible(false);
        }
    }
}

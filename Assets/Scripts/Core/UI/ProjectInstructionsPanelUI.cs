using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Editor for the project-owned conventions shared by the user and MCP.</summary>
    public class ProjectInstructionsPanelUI : MonoBehaviour
    {
        private GameObject? _root;
        private TMP_InputField? _text;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ProjectInstructionsPanel", canvas,
                Vector2.zero, new Vector2(640, 560));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            WindowDrag.Attach(panel.rectTransform, 44f);
            _root = panel.gameObject;

            UIFactory.CreateLabel("PiTitle", panel.transform, "Инструкции проекта", 24,
                new Vector2(0, 245), new Vector2(590, 34), TextAnchor.MiddleCenter);
            UIFactory.CreateLabel("PiHint", panel.transform,
                "Соглашения для пользователя и агента. Числа для геометрии задавайте строками " +
                "key: value, например bearing_wall_thickness_mm: 200.", 14,
                new Vector2(0, 202), new Vector2(590, 52), TextAnchor.UpperLeft);

            _text = UIFactory.CreateInputField("PiText", panel.transform, ProjectInstructions.Text,
                new Vector2(0, 0), new Vector2(590, 350));
            _text.lineType = TMP_InputField.LineType.MultiLineNewline;
            _text.textComponent!.alignment = TextAlignmentOptions.TopLeft;

            UIFactory.CreateButton("PiSave", panel.transform, "Сохранить",
                new Vector2(-90, -242), new Vector2(160, 38), Save);
            UIFactory.CreateButton("PiCancel", panel.transform, "Отмена",
                new Vector2(90, -242), new Vector2(160, 38), () => SetVisible(false));
            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));
            _root.SetActive(false);
        }

        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible && _text != null) _text.text = ProjectInstructions.Text;
            _root.SetActive(visible);
        }

        private void Save()
        {
            ProjectInstructions.Text = _text?.text ?? "";
            SetVisible(false);
        }
    }
}

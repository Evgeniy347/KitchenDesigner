using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Панель настроек, связанная с KitchenSettings.Instance.</summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        private GameObject _root;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(420, 360));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("SetTitle", panel.transform, "Настройки", 24,
                new Vector2(0, 150), new Vector2(400, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root.SetActive(false);
                return;
            }

            UIFactory.CreateToggle("TglGrid", panel.transform, "Сетка", s.GridEnabled,
                new Vector2(0, 100), new Vector2(360, 30), v => { s.GridEnabled = v; s.Save(); });

            UIFactory.CreateLabel("LblStep", panel.transform, "Шаг сетки, мм", 16,
                new Vector2(-110, 55), new Vector2(180, 28));
            var stepField = UIFactory.CreateInputField("StepField", panel.transform, s.GridStep.ToString(),
                new Vector2(120, 55), new Vector2(120, 28));
            stepField.contentType = InputField.ContentType.IntegerNumber;
            stepField.onEndEdit.AddListener(t =>
            {
                if (int.TryParse(t, out int v)) { s.GridStep = v; stepField.text = s.GridStep.ToString(); s.Save(); }
            });

            UIFactory.CreateToggle("TglSnap", panel.transform, "Снэппинг", s.SnapEnabled,
                new Vector2(0, 15), new Vector2(360, 30), v => { s.SnapEnabled = v; s.Save(); });

            UIFactory.CreateLabel("LblThr", panel.transform, "Порог снэпа, мм", 16,
                new Vector2(-110, -30), new Vector2(180, 28));
            var thrField = UIFactory.CreateInputField("ThrField", panel.transform, s.SnapThreshold.ToString("F0"),
                new Vector2(120, -30), new Vector2(120, 28));
            thrField.contentType = InputField.ContentType.DecimalNumber;
            thrField.onEndEdit.AddListener(t =>
            {
                if (float.TryParse(t, out float v)) { s.SnapThreshold = v; thrField.text = s.SnapThreshold.ToString("F0"); s.Save(); }
            });

            UIFactory.CreateToggle("TglBlock", panel.transform, "Блокировать ошибки", s.BlockOnViolation,
                new Vector2(0, -75), new Vector2(360, 30), v => { s.BlockOnViolation = v; s.Save(); });

            UIFactory.CreateButton("SetClose", panel.transform, "Закрыть",
                new Vector2(0, -150), new Vector2(160, 40), () => SetVisible(false));

            _root.SetActive(false);
        }

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}

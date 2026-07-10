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
            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(420, 640));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("SetTitle", panel.transform, "Настройки", 24,
                new Vector2(0, 240), new Vector2(400, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root.SetActive(false);
                return;
            }

            UIFactory.CreateToggle("TglGrid", panel.transform, "Сетка", s.GridEnabled,
                new Vector2(0, 190), new Vector2(360, 30), v => { s.GridEnabled = v; s.Save(); });

            UIFactory.CreateLabel("LblStep", panel.transform, "Шаг сетки, мм", 16,
                new Vector2(-110, 148), new Vector2(180, 28));
            var stepField = UIFactory.CreateInputField("StepField", panel.transform, s.GridStep.ToString(),
                new Vector2(120, 148), new Vector2(120, 28));
            stepField.contentType = InputField.ContentType.IntegerNumber;
            stepField.onEndEdit.AddListener(t =>
            {
                if (int.TryParse(t, out int v)) { s.GridStep = v; stepField.text = s.GridStep.ToString(); s.Save(); }
            });

            UIFactory.CreateToggle("TglSnap", panel.transform, "Снэппинг", s.SnapEnabled,
                new Vector2(0, 106), new Vector2(360, 30), v => { s.SnapEnabled = v; s.Save(); });

            UIFactory.CreateLabel("LblThr", panel.transform, "Порог снэпа, мм", 16,
                new Vector2(-110, 64), new Vector2(180, 28));
            var thrField = UIFactory.CreateInputField("ThrField", panel.transform, s.SnapThreshold.ToString("F0"),
                new Vector2(120, 64), new Vector2(120, 28));
            thrField.contentType = InputField.ContentType.DecimalNumber;
            thrField.onEndEdit.AddListener(t =>
            {
                if (float.TryParse(t, out float v)) { s.SnapThreshold = v; thrField.text = s.SnapThreshold.ToString("F0"); s.Save(); }
            });

            UIFactory.CreateToggle("TglBlock", panel.transform, "Блокировать ошибки", s.BlockOnViolation,
                new Vector2(0, 22), new Vector2(360, 30), v => { s.BlockOnViolation = v; s.Save(); });

            UIFactory.CreateToggle("TglAutoSave", panel.transform, "Автосохранение (при изменениях)", s.AutoSave,
                new Vector2(0, -18), new Vector2(360, 30), v => { s.AutoSave = v; s.Save(); });

            UIFactory.CreateLabel("LblAutoInt", panel.transform, "Интервал автосейва, с", 16,
                new Vector2(-110, -56), new Vector2(200, 28));
            var autoIntField = UIFactory.CreateInputField("AutoIntField", panel.transform, s.AutoSaveInterval.ToString(),
                new Vector2(140, -56), new Vector2(100, 28));
            autoIntField.contentType = InputField.ContentType.IntegerNumber;
            autoIntField.onEndEdit.AddListener(t =>
            {
                if (int.TryParse(t, out int v)) { s.AutoSaveInterval = v; autoIntField.text = s.AutoSaveInterval.ToString(); s.Save(); }
            });

            UIFactory.CreateToggle("TglSpatialGrid", panel.transform, "Пространственная сетка", s.SpatialGrid,
                new Vector2(0, -96), new Vector2(360, 30), v => { s.SpatialGrid = v; s.Save(); });

            UIFactory.CreateToggle("TglWindowed", panel.transform, "Оконный режим", s.WindowedMode,
                new Vector2(0, -136), new Vector2(360, 30), v => { s.WindowedMode = v; s.Save(); DisplaySettings.ApplyWindowMode(); });

            UIFactory.CreateToggle("TglEdgeOutline", panel.transform, "Контур (чёрные рёбра)", s.EdgeOutline,
                new Vector2(0, -176), new Vector2(360, 30), v => { s.EdgeOutline = v; s.Save(); });

            UIFactory.CreateToggle("TglWalls", panel.transform, "Стены", s.WallsEnabled,
                new Vector2(0, -216), new Vector2(360, 30), v => { s.WallsEnabled = v; s.Save(); });

            UIFactory.CreateToggle("TglLowerWalls", panel.transform, "Опускать ближние стены", s.LowerNearWalls,
                new Vector2(0, -256), new Vector2(360, 30), v => { s.LowerNearWalls = v; s.Save(); });

            UIFactory.CreateButton("SetClose", panel.transform, "Закрыть",
                new Vector2(0, -300), new Vector2(160, 40), () => SetVisible(false));

            _root.SetActive(false);
        }

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}

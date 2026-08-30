using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ToolbarUI
    {
        private const float BarHeight = 52f;
        private const float ButtonY = -6f;
        private const float ButtonH = 40f;
        private const float ButtonGap = 6f;
        private const float SwatchSize = 14f;

        private readonly List<(Button button, Func<bool> pressed)> _toggles = new();
        private readonly SceneSettleThrottle _issueBadgeThrottle = new();

        private IToolbarHost? _host;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _gotoIssueButton;
        private Image? _gotoIssueIcon;
        private RawImage? _eyedropperSwatch;
        private string _swatchMaterialId = string.Empty;
        private TMP_Text? _handleModeLabel;
        private TMP_Text? _editModeLabel;
        private TMP_Text? _issueCountLabel;

        public void Build(Transform canvas, IToolbarHost host)
        {
            _host = host;

            var bar = UIFactory.CreatePanel("Toolbar", canvas, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, BarHeight);

            float x = 8f;

            AddPanelToggle(bar.transform, "Spec", "Спецификация", ToolbarPanel.Specification, ref x, 150);
            AddPanelToggle(bar.transform, "Hierarchy", "Сцена", ToolbarPanel.Hierarchy, ref x, 90);
            var errorsButton = AddPanelToggle(bar.transform, "Errors", "Ошибки", ToolbarPanel.Errors, ref x, 110);
            _issueCountLabel = errorsButton.GetComponentInChildren<TMP_Text>();
            _gotoIssueButton = AddIconButton(bar.transform, "GotoIssue", IconFactory.Warning,
                ref x, GotoFirstIssue, "Перейти к первой проблеме");
            _gotoIssueIcon = _gotoIssueButton.transform.Find("GotoIssue_Icon")?.GetComponent<Image>();
            AddPanelToggle(bar.transform, "ProjectInstructions", "Инструкции",
                ToolbarPanel.ProjectInstructions, ref x, 120);
            AddSeparator(bar.transform, ref x);

            AddPanelToggle(bar.transform, "Settings", IconFactory.Gear, ToolbarPanel.Settings, ref x, "Настройки");
            AddIconButton(bar.transform, "Save", IconFactory.Floppy, ref x, host.SaveCurrent, "Сохранить");
            AddIconButton(bar.transform, "SaveAs", IconFactory.FloppyPlus, ref x, host.SaveAs, "Сохранить как");
            AddIconButton(bar.transform, "Load", IconFactory.Folder, ref x, host.LoadDialog, "Загрузить");
            AddSeparator(bar.transform, ref x);

            _undoButton = AddIconButton(bar.transform, "Undo", IconFactory.Undo, ref x, Undo, "Отменить");
            _redoButton = AddIconButton(bar.transform, "Redo", IconFactory.Redo, ref x, Redo, "Повторить");
            AddSeparator(bar.transform, ref x);

            var modeBtn = AddBarButton(bar.transform, "HandleMode", HandleModeLabel(), ref x, 176, ToggleHandleMode);
            _handleModeLabel = modeBtn.GetComponentInChildren<TMP_Text>();
            var measureButton = AddIconButton(bar.transform, "MeasureToggle", IconFactory.Ruler,
                ref x, Measure.MeasureMode.Toggle, "Рулетка");
            _toggles.Add((measureButton, () => Measure.MeasureMode.Active));
            var eyedropperButton = AddIconButton(bar.transform, "Eyedropper", IconFactory.Eyedropper,
                ref x, Tools.EyedropperMode.Toggle, "Пипетка: ПКМ — взять текстуру, ЛКМ — применить");
            _toggles.Add((eyedropperButton, () => Tools.EyedropperMode.Active));
            _eyedropperSwatch = AddSwatch(eyedropperButton.transform);
            AddSeparator(bar.transform, ref x);

            var tintButton = AddBarButton(bar.transform, "TintToggle", "Тонировка", ref x, 110, ToggleTint);
            _toggles.Add((tintButton, () => ElementHighlighter.TintEnabled));
            var lightsButton = AddIconButton(bar.transform, "LightsToggle", IconFactory.Bulb,
                ref x, ToggleLights, "Свет");
            _toggles.Add((lightsButton, () => LightSourceElement.GlobalOn));
            AddPanelToggle(bar.transform, "DayNight", IconFactory.Sun, ToolbarPanel.DayNight, ref x, "Солнце");
            AddSeparator(bar.transform, ref x);

            var editModeBtn = AddBarButton(bar.transform, "EditMode",
                EditModeManager.Label(EditModeManager.Mode), ref x, 190, EditModeManager.Cycle);
            _editModeLabel = editModeBtn.GetComponentInChildren<TMP_Text>();

            EditModeManager.Changed += RefreshEditModeLabel;
        }

        public void Dispose() => EditModeManager.Changed -= RefreshEditModeLabel;

        public void Refresh()
        {
            if (_undoButton != null) _undoButton.interactable = CommandStack.CanUndo;
            if (_redoButton != null) _redoButton.interactable = CommandStack.CanRedo;

            foreach (var (button, pressed) in _toggles)
                SetPressed(button, pressed());

            RefreshEyedropperSwatch();

            if (_issueBadgeThrottle.DueAfterSceneSettled(SceneRevision.Version, Time.unscaledTime))
                RefreshIssueBadge();
        }

        private Button AddPanelToggle(Transform parent, string name, string label,
            ToolbarPanel panel, ref float x, float w)
        {
            var btn = AddBarButton(parent, name, label, ref x, w, () => _host!.TogglePanel(panel));
            _toggles.Add((btn, () => _host!.IsPanelVisible(panel)));
            return btn;
        }

        private Button AddPanelToggle(Transform parent, string name, Sprite icon,
            ToolbarPanel panel, ref float x, string tooltip)
        {
            var btn = AddIconButton(parent, name, icon, ref x, () => _host!.TogglePanel(panel), tooltip);
            _toggles.Add((btn, () => _host!.IsPanelVisible(panel)));
            return btn;
        }

        private static Button AddBarButton(Transform parent, string name, string label,
            ref float x, float w, Action onClick)
        {
            var btn = UIFactory.CreateButton(name, parent, label, new Vector2(x, ButtonY),
                new Vector2(w, ButtonH), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, ButtonY);
            x += w + ButtonGap;
            return btn;
        }

        private static Button AddIconButton(Transform parent, string name, Sprite icon,
            ref float x, Action onClick, string tooltip)
        {
            var btn = UIFactory.CreateIconButton(name, parent, icon, new Vector2(x, ButtonY),
                new Vector2(ButtonH, ButtonH), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, ButtonY);
            TooltipUI.Attach(btn.gameObject, tooltip);
            x += ButtonH + ButtonGap;
            return btn;
        }

        private static void AddSeparator(Transform parent, ref float x)
        {
            x += 4;
            var sep = UIFactory.CreatePanel("Separator", parent, Vector2.zero,
                new Vector2(2, ButtonH - 8), UIStyle.Separator);
            UIFactory.AnchorTopLeft(sep.rectTransform);
            sep.rectTransform.anchoredPosition = new Vector2(x, ButtonY - 4);
            sep.raycastTarget = false;
            x += 12;
        }

        private static void SetPressed(Button? btn, bool on)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = on ? UIStyle.SurfaceActive : UIStyle.Surface;
        }

        private static RawImage AddSwatch(Transform button)
        {
            var rect = UIFactory.CreateRect("Swatch", button);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(SwatchSize, SwatchSize);
            rect.anchoredPosition = new Vector2(-3f, 3f);
            var img = rect.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return img;
        }

        private void RefreshEyedropperSwatch()
        {
            if (_eyedropperSwatch == null) return;
            var id = Tools.EyedropperMode.PickedMaterialId ?? string.Empty;
            if (id == _swatchMaterialId) return;
            _swatchMaterialId = id;

            if (id.Length == 0)
            {
                _eyedropperSwatch.gameObject.SetActive(false);
                return;
            }

            var def = MaterialCatalog.Get(id);
            var tex = MaterialManager.ResolveTexture(def);
            _eyedropperSwatch.texture = tex;
            _eyedropperSwatch.color = tex != null ? Color.white : def.baseColor;
            _eyedropperSwatch.gameObject.SetActive(true);
        }

        private void RefreshIssueBadge()
        {
            int errors = 0, warnings = 0;
            foreach (var iss in SceneAnalyzer.Analyze())
            {
                if (iss.Level == IssueLevel.Error) errors++;
                else if (iss.Level == IssueLevel.Warning) warnings++;
            }

            int total = errors + warnings;
            Color color = errors > 0 ? UIStyle.HighlightError : UIStyle.HighlightWarning;

            if (_issueCountLabel != null)
            {
                _issueCountLabel.text = total == 0
                    ? "Ошибки"
                    : $"Ошибки <color=#{ColorUtility.ToHtmlStringRGB(color)}>({total})</color>";
            }

            if (_gotoIssueButton != null) _gotoIssueButton.interactable = total > 0;
            if (_gotoIssueIcon != null)
                _gotoIssueIcon.color = total == 0 ? UIStyle.TextDisabled : color;
        }

        private static void GotoFirstIssue()
        {
            foreach (var iss in SceneAnalyzer.Analyze())
            {
                if (iss.Level != IssueLevel.Error && iss.Level != IssueLevel.Warning) continue;

                IssueDisplay.RevealIssue(iss);
                StatusBarUI.Instance?.ShowTransient(
                    $"{iss.Code} · {iss.Detail} · {iss.Message}",
                    IssueDisplay.LevelColor(iss.Level), 3f);
                return;
            }
        }

        private static string HandleModeLabel() =>
            ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize
                ? "Ручки: растяжение"
                : "Ручки: перенос";

        private void ToggleHandleMode()
        {
            ResizeHandleManager.ToggleMode();
            if (_handleModeLabel != null) _handleModeLabel.text = HandleModeLabel();
        }

        private void RefreshEditModeLabel()
        {
            if (_editModeLabel != null)
                _editModeLabel.text = EditModeManager.Label(EditModeManager.Mode);
        }

        private static void ToggleTint()
        {
            ElementHighlighter.TintEnabled = !ElementHighlighter.TintEnabled;
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static void ToggleLights() => LightSourceElement.SetGlobalOn(!LightSourceElement.GlobalOn);

        private static void Undo()
        {
            if (!CommandStack.CanUndo) return;
            CommandStack.Undo();
            AfterUndoRedo();
        }

        private static void Redo()
        {
            if (!CommandStack.CanRedo) return;
            CommandStack.Redo();
            AfterUndoRedo();
        }

        private static void AfterUndoRedo()
        {
            var sel = SelectionManager.Instance;
            if (sel != null && sel.Selected != null && !sel.Selected.gameObject.activeInHierarchy)
                sel.Deselect();
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }
    }
}

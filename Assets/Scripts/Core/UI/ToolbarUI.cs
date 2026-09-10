using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ToolbarUI
    {
        internal const float BarHeight = 52f;
        private const float ButtonY = -6f;
        private const float ButtonH = 40f;
        private const float ButtonGap = 6f;
        private const float SwatchSize = 14f;
        private const float TextButtonPad = 20f;

        private static readonly string[] HandleModeTooltips =
        {
            "Ручки: растяжение",
            "Ручки: перенос",
        };

        private readonly List<(Button button, Func<bool> pressed)> _toggles = new();
        private readonly SceneSettleThrottle _issueBadgeThrottle = new();

        private IToolbarHost? _host;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _gotoIssueButton;
        private Image? _gotoIssueIcon;
        private RawImage? _eyedropperSwatch;
        private string _swatchMaterialId = string.Empty;
        private Button? _handleModeButton;
        private Image? _handleModeIcon;
        private Image? _errorsIcon;
        private TMP_Text? _issueCountLabel;
        private int _issueBadgeRevision = -1;

        public int IssueBadgeRevision => _issueBadgeRevision;

        public void Build(Transform canvas, IToolbarHost host)
        {
            _host = host;

            var bar = UIFactory.CreatePanel("Toolbar", canvas, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, BarHeight);

            float x = 8f;

            AddPanelToggle(bar.transform, "Spec", IconFactory.Document, ToolbarPanel.Specification, ref x,
                "Спецификация");
            AddPanelToggle(bar.transform, "Hierarchy", IconFactory.SceneTree, ToolbarPanel.Hierarchy, ref x,
                "Сцена");
            var errorsButton = AddIconButton(bar.transform, "Errors", IconFactory.Warning,
                ref x, () => _host!.TogglePanel(ToolbarPanel.Errors), "Ошибки");
            _toggles.Add((errorsButton, () => _host!.IsPanelVisible(ToolbarPanel.Errors)));
            _errorsIcon = errorsButton.transform.Find("Errors_Icon")?.GetComponent<Image>();
            _issueCountLabel = AddBadge(errorsButton.transform);
            _gotoIssueButton = AddIconButton(bar.transform, "GotoIssue", IconFactory.FindIssue,
                ref x, GotoFirstIssue, "Перейти к первой проблеме");
            _gotoIssueIcon = _gotoIssueButton.transform.Find("GotoIssue_Icon")?.GetComponent<Image>();
            AddPanelToggle(bar.transform, "ProjectInstructions", IconFactory.Book,
                ToolbarPanel.ProjectInstructions, ref x, "Инструкции");
            AddSeparator(bar.transform, ref x);

            AddPanelToggle(bar.transform, "Settings", IconFactory.Gear, ToolbarPanel.Settings, ref x, "Настройки");
            AddIconButton(bar.transform, "Save", IconFactory.Floppy, ref x, host.SaveCurrent, "Сохранить");
            AddIconButton(bar.transform, "SaveAs", IconFactory.FloppyPlus, ref x, host.SaveAs, "Сохранить как");
            AddIconButton(bar.transform, "Load", IconFactory.Folder, ref x, host.LoadDialog, "Загрузить");
            AddSeparator(bar.transform, ref x);

            _undoButton = AddIconButton(bar.transform, "Undo", IconFactory.Undo, ref x, Undo, "Отменить");
            _redoButton = AddIconButton(bar.transform, "Redo", IconFactory.Redo, ref x, Redo, "Повторить");
            AddSeparator(bar.transform, ref x);

            _handleModeButton = AddIconButton(bar.transform, "HandleMode", HandleModeIcon(), ref x,
                ToggleHandleMode, HandleModeTooltip());
            _handleModeIcon = _handleModeButton.transform.Find("HandleMode_Icon")?.GetComponent<Image>();
            var measureButton = AddIconButton(bar.transform, "MeasureToggle", IconFactory.Ruler,
                ref x, Measure.MeasureMode.Toggle, "Рулетка");
            _toggles.Add((measureButton, () => Measure.MeasureMode.Active));
            var eyedropperButton = AddIconButton(bar.transform, "Eyedropper", IconFactory.Eyedropper,
                ref x, Tools.EyedropperMode.Toggle, "Пипетка: ПКМ — взять текстуру, ЛКМ — применить");
            _toggles.Add((eyedropperButton, () => Tools.EyedropperMode.Active));
            _eyedropperSwatch = AddSwatch(eyedropperButton.transform);
            AddSeparator(bar.transform, ref x);

            var lightsButton = AddIconButton(bar.transform, "LightsToggle", IconFactory.Bulb,
                ref x, ToggleLights, "Свет");
            _toggles.Add((lightsButton, () => LightSourceElement.GlobalOn));
            AddPanelToggle(bar.transform, "DayNight", IconFactory.Sun, ToolbarPanel.DayNight, ref x, "Солнце");
            AddSeparator(bar.transform, ref x);

            var normalModeButton = AddBarButton(bar.transform, "ModeNormal", "Обычный", ref x,
                () => EditModeManager.SetMode(EditMode.Normal));
            _toggles.Add((normalModeButton, () => EditModeManager.LastNonPhotoMode == EditMode.Normal));
            var roomModeButton = AddBarButton(bar.transform, "ModeRoom", "Помещение", ref x,
                () => EditModeManager.SetMode(EditMode.Room));
            _toggles.Add((roomModeButton, () => EditModeManager.LastNonPhotoMode == EditMode.Room));
            var photoModeButton = AddBarButton(bar.transform, "ModePhoto", "Фото", ref x, PhotoMode.Toggle);
            _toggles.Add((photoModeButton, () => PhotoMode.Active));

            AddRightPanelToggle(bar.transform, "Music", IconFactory.Note, ToolbarPanel.Music, "Музыка");
        }

        public void Dispose() { }

        public void Refresh()
        {
            using var _ = PerfMarkers.ToolbarRefresh.Auto();

            if (_undoButton != null) _undoButton.interactable = CommandStack.CanUndo;
            if (_redoButton != null) _redoButton.interactable = CommandStack.CanRedo;

            foreach (var (button, pressed) in _toggles)
                SetPressed(button, pressed());

            RefreshEyedropperSwatch();

            if (_issueBadgeThrottle.DueAfterSceneSettled(SceneRevision.Version, Time.unscaledTime))
                RefreshIssueBadge();
        }

        private Button AddPanelToggle(Transform parent, string name, Sprite icon,
            ToolbarPanel panel, ref float x, string tooltip)
        {
            var btn = AddIconButton(parent, name, icon, ref x, () => _host!.TogglePanel(panel), tooltip);
            _toggles.Add((btn, () => _host!.IsPanelVisible(panel)));
            return btn;
        }

        private Button AddRightPanelToggle(Transform parent, string name, Sprite icon,
            ToolbarPanel panel, string tooltip)
        {
            const float insetFromTheRightEdge = -8f;
            var btn = UIFactory.CreateIconButton(name, parent, icon, Vector2.zero,
                new Vector2(ButtonH, ButtonH), () => _host!.TogglePanel(panel));
            var rt = btn.GetComponent<RectTransform>();
            UIFactory.AnchorTopRight(rt);
            rt.anchoredPosition = new Vector2(insetFromTheRightEdge, ButtonY);
            TooltipUI.Attach(btn.gameObject, tooltip);
            _toggles.Add((btn, () => _host!.IsPanelVisible(panel)));
            return btn;
        }

        private static Button AddBarButton(Transform parent, string name, string label,
            ref float x, Action onClick, params string[] extraWidthCandidates)
        {
            var btn = UIFactory.CreateButton(name, parent, label, new Vector2(x, ButtonY),
                new Vector2(ButtonH, ButtonH), onClick);
            var caption = btn.GetComponentInChildren<TMP_Text>();

            float w = TextButtonPad + PreferredCaptionWidth(caption, label);
            foreach (var candidate in extraWidthCandidates)
                w = Mathf.Max(w, TextButtonPad + PreferredCaptionWidth(caption, candidate));

            var rect = btn.GetComponent<RectTransform>();
            UIFactory.AnchorTopLeft(rect);
            rect.sizeDelta = new Vector2(w, ButtonH);
            rect.anchoredPosition = new Vector2(x, ButtonY);
            x += w + ButtonGap;
            return btn;
        }

        private static float PreferredCaptionWidth(TMP_Text? caption, string text) =>
            caption != null && caption.font != null
                ? caption.GetPreferredValues(text).x
                : text.Length * 9f;

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

        private static TMP_Text AddBadge(Transform button)
        {
            var label = UIFactory.CreateLabel("Badge", button, string.Empty, 11,
                new Vector2(-1f, -1f), new Vector2(18f, 14f), TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot
                = new Vector2(1, 1);
            label.raycastTarget = false;
            return label;
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
            _issueBadgeRevision = SceneRevision.Version;

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
                _issueCountLabel.text = total == 0 ? string.Empty : total.ToString();
                _issueCountLabel.color = color;
                _issueCountLabel.gameObject.SetActive(total > 0);
            }

            if (_errorsIcon != null) _errorsIcon.color = total == 0 ? UIStyle.TextDisabled : color;
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
                    IssueDisplay.StatusLevelOf(iss.Level), 3f);
                return;
            }
        }

        private static bool IsResizeMode() => ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize;

        private static Sprite HandleModeIcon() =>
            IsResizeMode() ? IconFactory.ResizeHandles : IconFactory.MoveHandles;

        private static string HandleModeTooltip() =>
            IsResizeMode() ? HandleModeTooltips[0] : HandleModeTooltips[1];

        private void ToggleHandleMode()
        {
            ResizeHandleManager.ToggleMode();
            if (_handleModeIcon != null) _handleModeIcon.sprite = HandleModeIcon();
            if (_handleModeButton != null) ReattachTooltip(_handleModeButton.gameObject, HandleModeTooltip());
        }

        private static void ReattachTooltip(GameObject target, string tooltip)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger != null) trigger.triggers.Clear();
            TooltipUI.Attach(target, tooltip);
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

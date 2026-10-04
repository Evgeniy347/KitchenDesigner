using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ToolbarUI
    {
        internal const float BarHeight = ToolbarMetrics.BarHeight;
        private const float LevelLabelMinFontSize = UIStyle.FontCaption;

        private static readonly LocalizedCache<string[]> HandleModeTooltipsCache =
            new LocalizedCache<string[]>(() => new string[] {
            Loc.T("toolbar.handleMode.resize"),
            Loc.T("toolbar.handleMode.move"),
        });

        private static string[] HandleModeTooltips => HandleModeTooltipsCache.Value;

        private sealed class PressedToggle
        {
            public Button Button = null!;
            public Func<bool> Pressed = null!;
            public bool Shown;
        }

        private readonly List<PressedToggle> _toggles = new();
        private readonly SceneSettleThrottle _issueBadgeThrottle = new();
        private readonly List<(RectTransform rect, float width, float gapAfter)> _rightGroup = new();

        private IToolbarHost? _host;
        private Canvas? _canvas;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _gotoIssueButton;
        private Image? _gotoIssueIcon;
        private RawImage? _eyedropperSwatch;
        private string _swatchMaterialId = string.Empty;
        private Button? _handleModeButton;
        private Image? _handleModeIcon;
        private Image? _errorsIcon;
        private ToolbarIssueBadge? _issueBadge;
        private SegmentedControl? _viewMode;
        private int _viewModeShown = -1;
        private int _issueBadgeRevision = -1;
        private TMP_Text? _levelLabel;
        private Button? _levelUpButton;
        private Button? _levelDownButton;
        private Button? _levelsWindowButton;
        private readonly List<(RectTransform rect, float arrowsX)> _afterLevelSwitcher = new();
        private float _levelsWindowArrowsX;
        private float _levelSwitcherGroupWidth;
        private LevelSwitcherState _levelSwitcher;
        private int _levelSwitcherVersion = -1;
        private int _levelSwitcherLocRevision = -1;

        public int IssueBadgeRevision => _issueBadgeRevision;

        public void Build(Transform canvas, IToolbarHost host)
        {
            _host = host;
            _canvas = canvas.GetComponentInParent<Canvas>();

            var bar = UIFactory.CreatePanel("Toolbar", canvas, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, BarHeight);
            AddBottomRule(bar.transform);

            float x = ToolbarMetrics.EdgeInset;
            BuildFileGroup(bar.transform, host, ref x);
            BuildEditGroup(bar.transform, ref x);
            AddLevelSwitcher(bar.transform, ref x);
            int firstAfterLevelSwitcher = bar.transform.childCount;
            AddSeparator(bar.transform, ref x);
            BuildToolsGroup(bar.transform, ref x);
            AddSeparator(bar.transform, ref x);
            AddViewModeSegment(bar.transform, ref x);

            BuildRightGroup(bar.transform);

            for (int i = firstAfterLevelSwitcher; i < bar.transform.childCount; i++)
            {
                var rect = (RectTransform)bar.transform.GetChild(i);
                if (rect.anchorMin.x == 0f) _afterLevelSwitcher.Add((rect, rect.anchoredPosition.x));
            }

            if (_gotoIssueButton != null) _gotoIssueButton.interactable = false;
            RefreshLevelSwitcher();
        }

        private void BuildFileGroup(Transform bar, IToolbarHost host, ref float x)
        {
            AddIconButton(bar, "New", OutlineIconPaths.New, ref x, host.NewProject, Loc.T("toolbar.new"));
            AddPanelToggle(bar, "Load", OutlineIconPaths.Open, ToolbarPanel.LoadProject, ref x, Loc.T("toolbar.load"));
            AddIconButton(bar, "Save", OutlineIconPaths.Save, ref x, host.SaveCurrent, Loc.T("toolbar.save"));
            AddIconButton(bar, "SaveAs", OutlineIconPaths.SaveAs, ref x, host.SaveAs, Loc.T("toolbar.saveAs"));
            AddSeparator(bar, ref x);
        }

        private void BuildEditGroup(Transform bar, ref float x)
        {
            _undoButton = AddIconButton(bar, "Undo", OutlineIconPaths.Undo, ref x, Undo, Loc.T("toolbar.undo"));
            _redoButton = AddIconButton(bar, "Redo", OutlineIconPaths.Redo, ref x, Redo, Loc.T("toolbar.redo"));
            AddSeparator(bar, ref x);
        }

        private void BuildToolsGroup(Transform bar, ref float x)
        {
            _handleModeButton = AddIconButton(bar, "HandleMode", HandleModeIconName(), ref x,
                ToggleHandleMode, HandleModeTooltip());
            _handleModeIcon = ToolbarButtons.IconOf(_handleModeButton);
            var measureButton = AddIconButton(bar, "MeasureToggle", OutlineIconPaths.Measure,
                ref x, Measure.MeasureMode.Toggle, Loc.T("toolbar.measure"));
            AddToggle(measureButton, () => Measure.MeasureMode.Active);
            var eyedropperButton = AddIconButton(bar, "Eyedropper", OutlineIconPaths.Eyedropper,
                ref x, Tools.EyedropperMode.Toggle, Loc.T("toolbar.eyedropper"));
            AddToggle(eyedropperButton, () => Tools.EyedropperMode.Active);
            _eyedropperSwatch = AddSwatch(eyedropperButton.transform);
            var lightsButton = AddIconButton(bar, "LightsToggle", OutlineIconPaths.Bulb,
                ref x, ToggleLights, Loc.T("toolbar.lights"));
            AddToggle(lightsButton, () => LightSourceElement.GlobalOn);
        }

        private void BuildRightGroup(Transform bar)
        {
            AddRightPanelToggle(bar, "Hierarchy", OutlineIconPaths.Scene, ToolbarPanel.Hierarchy, Loc.T("toolbar.scene"));
            var errorsButton = AddRightPanelToggle(bar, "Errors", OutlineIconPaths.Warning,
                ToolbarPanel.Errors, Loc.T("toolbar.errors"));
            _errorsIcon = ToolbarButtons.IconOf(errorsButton);
            _issueBadge = ToolbarIssueBadge.Create(errorsButton.transform);
            _gotoIssueButton = AddRightIconButton(bar, "GotoIssue", OutlineIconPaths.FindIssue,
                GotoFirstIssue, Loc.T("toolbar.gotoFirstIssue"));
            _gotoIssueIcon = ToolbarButtons.IconOf(_gotoIssueButton);
            AddRightPanelToggle(bar, "Spec", OutlineIconPaths.Specification, ToolbarPanel.Specification,
                Loc.T("toolbar.specification"));
            AddRightPanelToggle(bar, "ProjectInstructions", OutlineIconPaths.Instructions,
                ToolbarPanel.ProjectInstructions, Loc.T("toolbar.instructions"));
            AddRightSeparator(bar);
            AddRightPanelToggle(bar, "DayNight", OutlineIconPaths.Sun, ToolbarPanel.DayNight, Loc.T("toolbar.sun"));
            AddRightPanelToggle(bar, "Music", OutlineIconPaths.Music, ToolbarPanel.Music, Loc.T("toolbar.music"));
            AddRightPanelToggle(bar, "Settings", OutlineIconPaths.Settings, ToolbarPanel.Settings,
                Loc.T("toolbar.settings"), lastInGroup: true);
            PlaceRightGroup();
        }

        public void Dispose() { }

        public void Refresh()
        {
            using var _ = PerfMarkers.ToolbarRefresh.Auto();

            if (_undoButton != null) _undoButton.interactable = CommandStack.CanUndo;
            if (_redoButton != null) _redoButton.interactable = CommandStack.CanRedo;

            foreach (var toggle in _toggles) SyncPressed(toggle);
            SyncViewMode();
            KeepHudBelowTheBar();

            RefreshEyedropperSwatch();

            if (_issueBadgeThrottle.DueAfterSceneSettled(SceneRevision.Version, Time.unscaledTime))
                RefreshIssueBadge();

            if (_levelSwitcherVersion != LevelRegistry.Version || _levelSwitcherLocRevision != Loc.Revision)
                RefreshLevelSwitcher();

            if (_levelSwitcher.ShowArrows && !CameraController.IsTypingInInputField() && LevelSwitchOwnsPageKeys())
            {
                if (_levelSwitcher.CanGoUp && InputMap.Down(InputAction.LevelUp)) LevelSwitch.Up();
                else if (_levelSwitcher.CanGoDown && InputMap.Down(InputAction.LevelDown)) LevelSwitch.Down();
            }
        }

        private void KeepHudBelowTheBar()
        {
            if (_canvas == null) return;
            PerfHud.ToolbarBottomY = BarHeight * _canvas.scaleFactor;
        }

        private static void SyncPressed(PressedToggle toggle)
        {
            bool on = toggle.Pressed();
            if (on == toggle.Shown) return;
            toggle.Shown = on;
            ToolbarButtons.SetPressed(toggle.Button, on);
        }

        private void SyncViewMode()
        {
            if (_viewMode == null) return;
            int shown = PhotoMode.Active ? 2 : EditModeManager.LastNonPhotoMode == EditMode.Room ? 1 : 0;
            if (shown == _viewModeShown) return;
            _viewModeShown = shown;
            _viewMode.SetValueWithoutNotify(shown);
        }

        private void RefreshLevelSwitcher()
        {
            _levelSwitcherVersion = LevelRegistry.Version;
            _levelSwitcherLocRevision = Loc.Revision;

            var levels = LevelRegistry.Snapshot();
            string currentId = LevelRegistry.CurrentId;
            var state = LevelSwitcherState.Of(levels, currentId);

            if (_levelLabel != null) _levelLabel.text = LevelResolution.ResolveElementLevel(currentId, levels).name;
            if (_levelUpButton != null) _levelUpButton.interactable = state.CanGoUp;
            if (_levelDownButton != null) _levelDownButton.interactable = state.CanGoDown;
            ApplyLevelSwitcherLayout(state.ShowArrows);
            _levelSwitcher = state;
        }

        private void ApplyLevelSwitcherLayout(bool showArrows)
        {
            if (_levelUpButton != null) _levelUpButton.gameObject.SetActive(showArrows);
            if (_levelLabel != null) _levelLabel.gameObject.SetActive(showArrows);
            if (_levelDownButton != null) _levelDownButton.gameObject.SetActive(showArrows);

            float shift = showArrows ? 0f : _levelSwitcherGroupWidth;
            if (_levelsWindowButton != null)
            {
                var rect = (RectTransform)_levelsWindowButton.transform;
                rect.anchoredPosition = new Vector2(_levelsWindowArrowsX - shift, rect.anchoredPosition.y);
            }

            foreach (var (rect, arrowsX) in _afterLevelSwitcher)
                rect.anchoredPosition = new Vector2(arrowsX - shift, rect.anchoredPosition.y);
        }

        internal static bool LevelSwitchOwnsPageKeys() =>
            PageKeyOwnership.Resolve(new PageKeyClaims
            {
                ErrorPanelOpenWithIssues = ErrorPanelUI.Instance != null && ErrorPanelUI.Instance.ClaimsPageNavigation,
            }) == PageKeyOwner.LevelSwitch;

        private void AddLevelSwitcher(Transform parent, ref float x)
        {
            float groupStartX = x;
            _levelUpButton = AddIconButton(parent, "LevelUp", OutlineIconPaths.ChevronUp, ref x, LevelSwitch.Up,
                Loc.T("toolbar.levelUp"));

            _levelLabel = UIFactory.CreateLabel("LevelLabel", parent, "", UIStyle.FontSmall,
                Vector2.zero, new Vector2(ToolbarMetrics.LevelLabelW, ToolbarMetrics.Button), TextAnchor.MiddleCenter);
            ToolbarButtons.PlaceFromLeft(_levelLabel.rectTransform, x, ToolbarMetrics.ButtonY);
            _levelLabel.enableWordWrapping = false;
            _levelLabel.overflowMode = TextOverflowModes.Ellipsis;
            _levelLabel.enableAutoSizing = true;
            _levelLabel.fontSizeMin = LevelLabelMinFontSize;
            _levelLabel.fontSizeMax = UIStyle.FontSmall;
            x += ToolbarMetrics.LevelLabelW + ToolbarMetrics.ButtonGap;

            _levelDownButton = AddIconButton(parent, "LevelDown", OutlineIconPaths.ChevronDown, ref x, LevelSwitch.Down,
                Loc.T("toolbar.levelDown"));

            _levelSwitcherGroupWidth = x - groupStartX;
            _levelsWindowArrowsX = x;
            _levelsWindowButton = AddPanelToggle(parent, "LevelsWindow", OutlineIconPaths.Levels, ToolbarPanel.Levels,
                ref x, Loc.T("toolbar.levels"));
        }

        private void AddViewModeSegment(Transform parent, ref float x)
        {
            string[] captions =
            {
                Loc.T("toolbar.mode.normal"), Loc.T("toolbar.mode.room"), Loc.T("toolbar.mode.photo"),
            };
            var widths = new float[captions.Length];
            for (int i = 0; i < captions.Length; i++)
                widths[i] = ToolbarButtons.MeasureCaption(parent, captions[i]) + ToolbarMetrics.SegmentPadX * 2f;

            _viewMode = SegmentedControl.Create("ViewMode", parent, captions, 0, widths, ToolbarMetrics.SegmentH, null);
            ToolbarButtons.PlaceFromLeft((RectTransform)_viewMode.transform, x, ToolbarMetrics.SegmentY);
            x += SegmentedControl.WidthFor(widths) + ToolbarMetrics.ButtonGap;

            string[] names = { "ModeNormal", "ModeRoom", "ModePhoto" };
            Action[] actions =
            {
                () => EditModeManager.SetMode(EditMode.Normal),
                () => EditModeManager.SetMode(EditMode.Room),
                PhotoMode.Toggle,
            };
            for (int i = 0; i < names.Length; i++)
            {
                var segment = _viewMode.Segments[i];
                segment.gameObject.name = names[i];
                var action = actions[i];
                segment.onClick.AddListener(() => action());
            }
        }

        private Button AddPanelToggle(Transform parent, string name, string icon,
            ToolbarPanel panel, ref float x, string tooltip)
        {
            var btn = AddIconButton(parent, name, icon, ref x, () => _host!.TogglePanel(panel), tooltip);
            AddToggle(btn, () => _host!.IsPanelVisible(panel));
            return btn;
        }

        private Button AddRightPanelToggle(Transform parent, string name, string icon,
            ToolbarPanel panel, string tooltip, bool lastInGroup = false)
        {
            var btn = AddRightIconButton(parent, name, icon, () => _host!.TogglePanel(panel), tooltip, lastInGroup);
            AddToggle(btn, () => _host!.IsPanelVisible(panel));
            return btn;
        }

        private Button AddRightIconButton(Transform parent, string name, string icon, Action onClick,
            string tooltip, bool lastInGroup = false)
        {
            var btn = ToolbarButtons.CreateIcon(parent, name, icon, tooltip, onClick);
            _rightGroup.Add(((RectTransform)btn.transform, ToolbarMetrics.Button,
                lastInGroup ? 0f : ToolbarMetrics.ButtonGap));
            return btn;
        }

        private void AddRightSeparator(Transform parent)
        {
            float margin = (ToolbarMetrics.GroupSlot - ToolbarMetrics.SeparatorW) * 0.5f;
            var line = ToolbarButtons.CreateSeparator(parent);
            int last = _rightGroup.Count - 1;
            var (rect, width, _) = _rightGroup[last];
            _rightGroup[last] = (rect, width, margin);
            _rightGroup.Add((line, ToolbarMetrics.SeparatorW, margin));
        }

        private void PlaceRightGroup()
        {
            float x = -ToolbarMetrics.EdgeInset;
            for (int i = _rightGroup.Count - 1; i >= 0; i--)
            {
                var (rect, width, _) = _rightGroup[i];
                UIFactory.AnchorTopRight(rect);
                rect.anchoredPosition = new Vector2(x, -(BarHeight - rect.sizeDelta.y) * 0.5f);
                x -= width;
                if (i > 0) x -= _rightGroup[i - 1].gapAfter;
            }
        }

        private void AddToggle(Button button, Func<bool> pressed)
        {
            var toggle = new PressedToggle { Button = button, Pressed = pressed };
            _toggles.Add(toggle);
            SyncPressed(toggle);
        }

        private static Button AddIconButton(Transform parent, string name, string icon,
            ref float x, Action onClick, string tooltip)
        {
            var btn = ToolbarButtons.CreateIcon(parent, name, icon, tooltip, onClick);
            ToolbarButtons.PlaceFromLeft((RectTransform)btn.transform, x, ToolbarMetrics.ButtonY);
            x += ToolbarMetrics.Button + ToolbarMetrics.ButtonGap;
            return btn;
        }

        private static void AddSeparator(Transform parent, ref float x)
        {
            x -= ToolbarMetrics.ButtonGap;
            var line = ToolbarButtons.CreateSeparator(parent);
            ToolbarButtons.PlaceFromLeft(line,
                x + (ToolbarMetrics.GroupSlot - ToolbarMetrics.SeparatorW) * 0.5f,
                -(BarHeight - ToolbarMetrics.SeparatorH) * 0.5f);
            x += ToolbarMetrics.GroupSlot;
        }

        private static void AddBottomRule(Transform bar)
        {
            var rule = UIFactory.CreatePanel("BottomRule", bar, Vector2.zero, Vector2.zero, UIStyle.Divider);
            rule.raycastTarget = false;
            var rect = rule.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, UIStyle.DividerPx);
            rect.anchoredPosition = Vector2.zero;
        }

        private static RawImage AddSwatch(Transform button)
        {
            var rect = UIFactory.CreateRect("Swatch", button);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(ToolbarMetrics.SwatchSize, ToolbarMetrics.SwatchSize);
            rect.anchoredPosition = new Vector2(-ToolbarMetrics.SwatchInset, ToolbarMetrics.SwatchInset);
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
            _issueBadgeRevision = SceneRevision.Version;

            int errors = 0, warnings = 0;
            foreach (var iss in SceneAnalyzer.Analyze())
            {
                if (iss.Level == IssueLevel.Error) errors++;
                else if (iss.Level == IssueLevel.Warning) warnings++;
            }

            int total = errors + warnings;
            Color tint = errors > 0 ? UIStyle.TextError : warnings > 0 ? UIStyle.TextWarning : UIStyle.Text;

            _issueBadge?.Show(total, errors > 0);
            if (_errorsIcon != null) _errorsIcon.color = tint;
            if (_gotoIssueButton != null) _gotoIssueButton.interactable = total > 0;
            if (_gotoIssueIcon != null && total > 0) _gotoIssueIcon.color = tint;
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

        private static string HandleModeIconName() =>
            IsResizeMode() ? OutlineIconPaths.ResizeHandles : OutlineIconPaths.MoveHandles;

        private static string HandleModeTooltip() =>
            IsResizeMode() ? HandleModeTooltips[0] : HandleModeTooltips[1];

        private void ToggleHandleMode()
        {
            ResizeHandleManager.ToggleMode();
            if (_handleModeIcon != null) _handleModeIcon.sprite = OutlineIcons.Get(HandleModeIconName());
            if (_handleModeButton != null) ReattachTooltip(_handleModeButton.gameObject, HandleModeTooltip());
        }

        private static void ReattachTooltip(GameObject target, string tooltip)
        {
            var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
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

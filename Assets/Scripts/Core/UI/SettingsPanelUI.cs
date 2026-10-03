using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class SettingsPanelUI : MonoBehaviour, IProjectWindow
    {
        private const float PanelW = 600;
        private const float PanelH = 900;
        private const float TitleY = PanelH * 0.5f - 55;
        private const float TabY = PanelH * 0.5f - 94;
        private const float ContentTopY = PanelH * 0.5f - 138;
        private const float CloseH = 40f;
        private const float CloseY = -PanelH * 0.5f + 54;
        private const float PanelSidePad = 40f;

        private const float BodyTopInset = PanelH * 0.5f - ContentTopY - SettingsRowFactory.RowH * 0.5f;
        private const float BodyBottomInset = PanelH * 0.5f + CloseY + CloseH * 0.5f + UIStyle.GapInner;

        private static readonly LocalizedCache<string[]> TabLabelsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("settings.tab.project"), Loc.T("settings.tab.view"), Loc.T("settings.tab.construction"), Loc.T("settings.tab.control"), Loc.T("settings.tab.photo"), Loc.T("settings.tab.light"), "MCP", Loc.T("settings.tab.about") });

        private static string[] TabLabels => TabLabelsCache.Value;

        private const int ControlTabIndex = 3;

        private readonly SettingsRowFactory _rows = new();
        private readonly SettingsTabStrip _tabs = new();

        private GameObject? _root;
        private WindowBody? _body;
        private SettingsProjectTab? _projectTab;
        private SettingsViewTab? _viewTab;
        private SettingsControlTab? _controlTab;
        private SettingsPhotoTab? _photoTab;
        private SettingsMcpTab? _mcpTab;
        private KeybindingCaptureGate? _captureGate;
        private KeybindingGestureGate? _gestureGate;

        public void Build(Transform canvas)
        {
            _viewTab?.Dispose();

            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("SetTitle", panel.transform, Loc.T("settings.title"), UIStyle.FontWindowTitle,
                new Vector2(0, TitleY), new Vector2(PanelW - PanelSidePad, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root.SetActive(false);
                return;
            }

            _tabs.BuildButtons(panel.transform, TabLabels, PanelW - PanelSidePad, TabY);

            _body = WindowBody.Create(panel.rectTransform, BodyTopInset, BodyBottomInset,
                PanelSidePad * 0.5f);
            _tabs.AfterSwitch = () =>
            {
                _body?.Fit();
                if (_tabs.CurrentIndex == ControlTabIndex) return;
                _captureGate?.CancelIfCapturing();
                _gestureGate?.CancelIfCapturing();
            };

            _captureGate = gameObject.AddComponent<KeybindingCaptureGate>();
            _captureGate.Build(canvas);
            _gestureGate = gameObject.AddComponent<KeybindingGestureGate>();
            _gestureGate.Build(canvas);

            _projectTab = new SettingsProjectTab(_rows, RefreshDependentStates);
            _viewTab = new SettingsViewTab(_rows, RefreshDependentStates);
            _controlTab = new SettingsControlTab(_rows);
            _photoTab = new SettingsPhotoTab(_rows);

            _projectTab.Build(AddPage("Tab_Project"), s, ContentTopY);
            _viewTab.Build(AddPage("Tab_View"), ContentTopY);
            new SettingsConstructionTab(_rows).Build(AddPage("Tab_Construction"), s, ContentTopY);
            _controlTab.Build(AddPage("Tab_Control"), s, ContentTopY, _captureGate, _gestureGate,
                () => _body?.Fit());
            _photoTab.Build(AddPage("Tab_Photo"), s, ContentTopY);
            new SettingsLightTab(_rows).Build(AddPage("Tab_Light"), s, ContentTopY);
            _mcpTab = new SettingsMcpTab(_rows);
            _mcpTab.Build(AddPage("Tab_Mcp"), ContentTopY);
            new SettingsAboutTab().Build(AddPage("Tab_About"));

            _tabs.Switch(0);

            _viewTab.FollowEditMode();

            UIFactory.CreateButton("SetClose", panel.transform, Loc.T("common.close"),
                new Vector2(0, CloseY), new Vector2(160, CloseH),
                () => SetVisible(false));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            _body.Fit();
            _root.SetActive(false);
        }

        private Transform AddPage(string name) =>
            _tabs.AddPage(_body!.Content, name, _body!.PanelOriginY).transform;

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            _captureGate?.CancelIfCapturing();
            _gestureGate?.CancelIfCapturing();
            _viewTab?.Dispose();
        }

        internal void SyncFromSettings()
        {
            _rows.ReadBackFromSettings();
            _photoTab?.RefreshPresetLabel();
            _mcpTab?.Refresh();
            _controlTab?.RefreshConflicts();
        }

        public void OpenControlsTab() => OpenTab(ControlTabIndex);

        internal int CurrentTab => _tabs.CurrentIndex;

        internal void OpenTab(int index)
        {
            SetVisible(true);
            _tabs.Switch(index);
            _body?.Fit();
        }

        private void RefreshDependentStates()
        {
            if (_root == null) return;
            var s = KitchenSettings.Instance;
            if (s == null) return;
            _projectTab?.RefreshDependentStates(s);
            _viewTab?.Refresh();
            _body?.Fit();
        }

        public string WindowId => "settings";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (!visible)
            {
                _captureGate?.CancelIfCapturing();
                _gestureGate?.CancelIfCapturing();
                return;
            }
            SyncFromSettings();
            RefreshDependentStates();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class LevelsWindowUI : MonoBehaviour, IProjectWindow
    {
        public string WindowId => "levels";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;

        private WindowChrome? _chrome;
        private WindowBody? _body;
        private LevelsListView? _list;
        private int _shownVersion = -1;

        internal LevelsListView? List => _list;

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "LevelsWindow", Loc.T("window.levels.title"),
                new Vector2(UIStyle.LevelsW, UIStyle.LevelsW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = () => SetVisible(false),
                    RuledHeader = true,
                });
            var panel = _chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-UIStyle.Space3, -(UIStyle.ToolbarH + UIStyle.Space3));
            ProjectWindows.Register(this);

            _body = _chrome.CreateBody();
            _list = new LevelsListView(_body.Content, _chrome.BodyWidth, DeleteLevel);
            Layout(new List<Level>());

            panel.gameObject.SetActive(false);
            LevelRegistry.Changed += OnLevelRegistryChanged;
        }

        public void Refresh()
        {
            if (_list == null) return;
            var levels = new List<Level>(LevelRegistry.Items);
            levels.Sort((a, b) => b.floorElevationMm.CompareTo(a.floorElevationMm));
            Layout(levels);
        }

        private void Layout(List<Level> levels)
        {
            if (_chrome == null || _body == null || _list == null) return;
            _shownVersion = LevelRegistry.Version;
            float content = _list.Rebuild(levels, LevelRegistry.CurrentId);
            _chrome.FitHeightTo(content);
            _body.Fit();
        }

        internal void DeleteLevel(Level level) => LevelEdits.Delete(level);

        public bool IsVisible => _chrome != null && !IsGone(_chrome.Panel) && _chrome.Panel.gameObject.activeSelf;

        private static bool IsGone(RectTransform panel) => panel == null;

        public void SetVisible(bool visible)
        {
            if (_chrome == null || IsGone(_chrome.Panel)) return;
            _chrome.Panel.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        private void OnLevelRegistryChanged()
        {
            if (IsVisible) Refresh();
        }

        private void OnDestroy()
        {
            LevelRegistry.Changed -= OnLevelRegistryChanged;
            ProjectWindows.Unregister(this);
        }

        private void Update()
        {
            if (!IsVisible) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TryHideFromEscape();
            if (_shownVersion != LevelRegistry.Version) Refresh();
        }

        internal void TryHideFromEscape()
        {
            if (OwnsEscape()) SetVisible(false);
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                ModalOpen = ModalPresence.IsOpen,
                LevelsWindowOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
                ContextMenuOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen,
                GroupMenuOpen = GroupMenuUI.Instance != null && GroupMenuUI.Instance.IsOpen,
                CatalogTileSelected = SidebarUI.CatalogClaimsEscape,
                DayNightOpen = DayNightPanelUI.Instance != null && DayNightPanelUI.Instance.IsVisible,
                MusicOpen = MusicPanelUI.Instance != null && MusicPanelUI.Instance.IsVisible,
            }) == EscapeOwner.LevelsWindow;
    }
}

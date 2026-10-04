using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SettingsPanelUI : MonoBehaviour, IProjectWindow
    {
        public const string PanelName = "SettingsPanel";
        internal const string ResetButtonName = "ResetSection";
        internal const string AppliedNoteNode = "AppliedNote";
        internal const string EmptyNode = "SettingsEmpty";

        internal const string GeneralId = "general";
        internal const string ProjectId = "project";
        internal const string ViewId = "view";
        internal const string ControlId = "control";
        internal const string LightId = "light";
        internal const string PhotoId = "photo";
        internal const string ConstructionId = "construction";
        internal const string McpId = "mcp";
        internal const string AboutId = "about";

        internal static readonly string[] PageOrder =
        {
            GeneralId, ProjectId, ViewId, ControlId, LightId, PhotoId, ConstructionId, McpId, AboutId,
        };

        private readonly List<SettingsPage> _pages = new();

        private SettingsForm _form = new();
        private SettingsNav? _nav;
        private GameObject? _root;
        private WindowBody? _body;
        private WindowFooter? _footer;
        private Button? _resetButton;
        private GameObject? _empty;
        private SettingsProjectTab? _projectTab;
        private SettingsViewTab? _viewTab;
        private SettingsControlTab? _controlTab;
        private SettingsPhotoTab? _photoTab;
        private SettingsMcpTab? _mcpTab;
        private KeybindingCaptureGate? _captureGate;
        private KeybindingGestureGate? _gestureGate;
        private int _current;
        private bool _noMatches;

        private static int ControlIndex => System.Array.IndexOf(PageOrder, ControlId);

        public void Build(Transform canvas)
        {
            _viewTab?.Dispose();
            _pages.Clear();
            _form = new SettingsForm();
            _current = 0;
            _noMatches = false;

            var chrome = WindowChrome.Create(canvas, PanelName, Loc.T("settings.title"), UIStyle.SettingsSize,
                new WindowChromeOptions
                {
                    Kind = WindowKind.Dialog,
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });
            var panel = chrome.Panel;
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            var s = KitchenSettings.Instance;
            _body = chrome.CreateBody();
            InsetBodyForTheNav(_body, chrome);

            _captureGate = gameObject.AddComponent<KeybindingCaptureGate>();
            _captureGate.Build(canvas);
            _gestureGate = gameObject.AddComponent<KeybindingGestureGate>();
            _gestureGate.Build(canvas);

            BuildPages(s);
            BuildNav(panel);
            BuildFooter(chrome);
            BuildEmptyState();

            _viewTab!.FollowEditMode();
            ShowPage(0);
            _root.SetActive(false);
        }

        private void BuildPages(KitchenSettings s)
        {
            var content = _body!.Content;
            SettingsPage Add(string id, string title, string subtitle) =>
                AddPage(new SettingsPage(_form, content, id, title, subtitle));

            new SettingsGeneralTab(Add(GeneralId, Loc.T("settings.tab.general"), Loc.T("settings.page.general")))
                .Build();

            _projectTab = new SettingsProjectTab(
                Add(ProjectId, Loc.T("settings.tab.project"), Loc.T("settings.page.project")),
                RefreshDependentStates);
            _projectTab.Build(s);

            _viewTab = new SettingsViewTab(
                Add(ViewId, Loc.T("settings.tab.view"), Loc.T("settings.page.view")), RefreshDependentStates);
            _viewTab.Build();

            _controlTab = new SettingsControlTab(
                Add(ControlId, Loc.T("settings.tab.control"), Loc.T("settings.page.control")));
            _controlTab.Build(s, _captureGate!, _gestureGate!, () => _body?.Fit());

            new SettingsLightTab(Add(LightId, Loc.T("settings.tab.light"), Loc.T("settings.page.light")))
                .Build(s);

            _photoTab = new SettingsPhotoTab(
                Add(PhotoId, Loc.T("settings.tab.photo"), Loc.T("settings.page.photo")));
            _photoTab.Build(s);

            new SettingsConstructionTab(
                Add(ConstructionId, Loc.T("settings.tab.construction"), Loc.T("settings.page.construction")))
                .Build(s);

            _mcpTab = new SettingsMcpTab(Add(McpId, "MCP", Loc.T("settings.page.mcp")));
            _mcpTab.Build();

            new SettingsAboutTab(Add(AboutId, Loc.T("settings.tab.about"), Loc.T("settings.page.about")))
                .Build();

            foreach (var page in _pages) page.Relayout();
        }

        private SettingsPage AddPage(SettingsPage page)
        {
            _pages.Add(page);
            return page;
        }

        private void BuildNav(RectTransform panel)
        {
            string picture = Loc.T("settings.nav.group.picture");
            string house = Loc.T("settings.nav.group.house");
            string other = Loc.T("settings.nav.group.other");
            string? GroupOf(string id) => id switch
            {
                LightId => picture,
                PhotoId => picture,
                ConstructionId => house,
                McpId => other,
                AboutId => other,
                _ => null,
            };

            var entries = new List<SettingsNavEntry>();
            foreach (var page in _pages) entries.Add(new SettingsNavEntry(page.Id, page.Title, GroupOf(page.Id)));
            _nav = SettingsNav.Create(panel, entries, ShowPage, OnQuery);
        }

        private void BuildFooter(WindowChrome chrome)
        {
            _footer = chrome.Footer!;
            _resetButton = _footer.AddLeft(ResetButtonName, Loc.T("settings.reset.section"), OnResetClicked,
                ButtonRole.Link);

            string text = Loc.T("settings.footer.applied");
            var note = UIFactory.CreateLabel(AppliedNoteNode, _footer.Root, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(0f, UIStyle.ControlH), TextAnchor.MiddleRight);
            note.color = UIStyle.TextSecondary;
            note.raycastTarget = false;
            note.enableWordWrapping = false;
            note.overflowMode = TextOverflowModes.Ellipsis;
            bool rtl = LayoutDirection.IsRtl;
            var rt = note.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(rtl ? 0f : 1f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Ceil(note.GetPreferredValues(text).x) + 1f, UIStyle.ControlH);
            rt.anchoredPosition = new Vector2(rtl ? UIStyle.Space4 : -UIStyle.Space4, 0f);
        }

        private void BuildEmptyState()
        {
            var empty = EmptyState.Create(_body!.Content, EmptyNode, Loc.T("settings.empty.title"),
                Loc.T("settings.empty.hint"));
            var rt = empty.Root;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(SettingsPage.ContentW * 0.5f, -_body.VisibleHeight * 0.5f);
            _empty = rt.gameObject;
            _empty.SetActive(false);
        }

        private static void InsetBodyForTheNav(WindowBody body, WindowChrome chrome)
        {
            var viewport = body.Viewport;
            float inset = UIStyle.NavW + chrome.BodyPad;
            if (LayoutDirection.IsRtl)
            {
                viewport.offsetMin = new Vector2(Mathf.Max(0f, chrome.BodyPad - WindowBody.BarW), viewport.offsetMin.y);
                viewport.offsetMax = new Vector2(-inset, viewport.offsetMax.y);
            }
            else
            {
                viewport.offsetMin = new Vector2(inset, viewport.offsetMin.y);
            }
        }

        private void ShowPage(int index)
        {
            if (_pages.Count == 0) return;
            _current = Mathf.Clamp(index, 0, _pages.Count - 1);
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].Root.gameObject.SetActive(i == _current && !_noMatches);
            if (_empty != null) _empty.SetActive(_noMatches);
            _nav?.SetCurrent(_current);

            var content = _body!.Content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            if (_current != ControlIndex)
            {
                _captureGate?.CancelIfCapturing();
                _gestureGate?.CancelIfCapturing();
            }
            SyncFooter();
            _body.Fit();
        }

        private void OnQuery(string query)
        {
            var shown = new bool[_pages.Count];
            int first = -1;
            bool filtering = query.Trim().Length > 0;
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].SetQuery(query);
                shown[i] = !filtering || _pages[i].MatchCount > 0;
                if (shown[i] && first < 0) first = i;
            }

            _nav!.Layout(shown);
            _noMatches = first < 0;
            int target = _noMatches || shown[_current] ? _current : first;
            ShowPage(target);
        }

        private void OnResetClicked() => _pages[_current].OnReset?.Invoke();

        internal void SyncFooter()
        {
            if (_resetButton == null || _pages.Count == 0) return;
            var page = _pages[_current];
            bool has = page.OnReset != null && !_noMatches;
            _resetButton.gameObject.SetActive(has);
            if (!has) return;

            bool enabled = page.CanReset == null || page.CanReset();
            _resetButton.interactable = enabled;
            _resetButton.GetComponentInChildren<TMP_Text>().color =
                enabled ? UIStyle.AccentText : UIStyle.TextDisabled;
        }

        private void Update()
        {
            if (_root != null && _root.activeSelf) SyncFooter();
        }

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            _captureGate?.CancelIfCapturing();
            _gestureGate?.CancelIfCapturing();
            _viewTab?.Dispose();
        }

        internal void SyncFromSettings()
        {
            _form.ReadBackFromSettings();
            _photoTab?.RefreshPreset();
            _mcpTab?.Refresh();
            _controlTab?.RefreshConflicts();
            SyncFooter();
        }

        public void OpenControlsTab() => OpenTab(ControlIndex);

        internal int CurrentTab => _current;

        internal int PageCount => _pages.Count;

        internal SettingsNav? Nav => _nav;

        internal void OpenTab(int index)
        {
            SetVisible(true);
            _nav?.ClearSearch();
            ShowPage(index);
        }

        private void RefreshDependentStates()
        {
            if (_root == null) return;
            var s = KitchenSettings.Instance;
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

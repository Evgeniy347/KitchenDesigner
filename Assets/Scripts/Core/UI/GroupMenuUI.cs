using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class GroupMenuUI : MonoBehaviour
    {
        public const string SettingsPanelName = "GroupMenu";
        public const string LinkPanelName = "GroupLinkPrompt";
        public const string UnlinkNode = "GmUnlink";
        public const string DeleteAllNode = "GmDeleteAll";
        public const string LinkNode = "GmLink";

        internal const float SettingsW = 400f;

        public static GroupMenuUI? Instance { get; private set; }

        public bool IsOpen => IsShown(_settings) || IsShown(_link);

        private WindowChrome? _settings;
        private WindowChrome? _link;
        private TMP_InputField? _nameField;
        private Toggle? _lockMove;
        private LinkGroup? _group;

        internal RectTransform? SettingsPanel => _settings?.Panel;

        internal RectTransform? LinkPanel => _link?.Panel;

        private void Awake() => Instance = this;

        private static bool IsShown(WindowChrome? chrome) => chrome != null && chrome.Panel.gameObject.activeSelf;

        public void Build(Transform canvas)
        {
            BuildSettings(canvas);
            BuildLinkPrompt(canvas);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += CloseWhenSelectionLeavesTheGroup;
        }

        private void BuildSettings(Transform canvas)
        {
            _settings = WindowChrome.Create(canvas, SettingsPanelName, Loc.T("group.title"),
                new Vector2(SettingsW, UIStyle.InspectorW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = Close,
                    HasFooter = true,
                    RuledHeader = true,
                });
            Center(_settings.Panel);

            var body = _settings.CreateBody();
            var rows = new FormRows(body.Content, RowDensity.Compact);
            (_, _nameField) = rows.Text("GmName", Loc.T("group.name"));
            _nameField.onEndEdit.AddListener(t => { if (_group != null) GroupManager.Rename(_group, t); });
            (_, _lockMove) = rows.Switch("GmLockMove", Loc.T("group.lock"), false,
                v => { if (_group != null) GroupManager.SetMovable(_group, !v); });
            rows.Gap(UIStyle.Space2);
            rows.Custom(EditModuleButton(body.Content, rows.Metrics.Width), UIStyle.ControlH);
            _settings.FitHeightTo(rows.Relayout());
            body.Fit();

            var footer = _settings.Footer!;
            footer.AddLeft(UnlinkNode, Loc.T("group.unlink"), DoUnlink);
            var delete = footer.AddRight(DeleteAllNode, Loc.T("group.deleteAll"), null!, ButtonRole.DangerOutline);
            ConfirmDeleteButton.Attach(delete, DoDeleteWithContents);
            WidenToFitFooter(_settings);
            _settings.Panel.gameObject.SetActive(false);
        }

        private static void WidenToFitFooter(WindowChrome chrome)
        {
            float needed = 2f * WindowFooter.ButtonPadX + UIStyle.Space2;
            foreach (var button in chrome.Footer!.LeftGroup) needed += button.sizeDelta.x;
            foreach (var button in chrome.Footer.RightGroup) needed += button.sizeDelta.x;
            var size = chrome.Panel.sizeDelta;
            chrome.Panel.sizeDelta = new Vector2(Mathf.Max(size.x, needed), size.y);
        }

        private RectTransform EditModuleButton(RectTransform parent, float width)
        {
            var button = UIFactory.CreateButton("GmEdit", parent, Loc.T("group.editModule"), Vector2.zero,
                new Vector2(width, UIStyle.ControlH), DoEditModule);
            return (RectTransform)button.transform;
        }

        private void BuildLinkPrompt(Transform canvas)
        {
            _link = WindowChrome.Create(canvas, LinkPanelName, Loc.T("group.linkPrompt"),
                new Vector2(UIStyle.InspectorW, UIStyle.InspectorW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = Close,
                    HasFooter = true,
                    RuledHeader = true,
                });
            Center(_link.Panel);
            _link.Footer!.AddPrimary(LinkNode, Loc.T("group.link"), DoLink);
            _link.FitHeightTo(0f);
            _link.Panel.gameObject.SetActive(false);
        }

        private static void Center(RectTransform panel)
        {
            UIFactory.AnchorCenter(panel);
            panel.anchoredPosition = new Vector2(0f, UIStyle.Space6 + UIStyle.Space2);
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;
            _group = GroupManager.GroupOf(element);

            if (_group != null)
            {
                ShowGroupSettings(_group);
                return;
            }

            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 2) { Close(); return; }
            ShowCompactLinkPrompt();
        }

        private void ShowGroupSettings(LinkGroup group)
        {
            _link!.Panel.gameObject.SetActive(false);
            _nameField!.SetTextWithoutNotify(group.name);
            _lockMove!.SetIsOnWithoutNotify(!group.movable);
            _settings!.Panel.gameObject.SetActive(true);
        }

        private void ShowCompactLinkPrompt()
        {
            _settings!.Panel.gameObject.SetActive(false);
            _link!.Panel.gameObject.SetActive(true);
        }

        public void Close()
        {
            _group = null;
            ConfirmDeleteButton.DisarmAll();
            if (_settings != null) _settings.Panel.gameObject.SetActive(false);
            if (_link != null) _link.Panel.gameObject.SetActive(false);
        }

        private void DoLink()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;
            var members = new List<KitchenElement>(sel.SelectedElements);
            var g = GroupManager.Link(members);
            if (g != null)
            {
                sel.SelectOnly(GroupManager.MembersOf(g));
                Open(members[0]);
            }
        }

        private void DoUnlink()
        {
            if (_group == null) return;
            var group = _group;
            Close();
            GroupDeletion.Dissolve(group);
        }

        private void DoDeleteWithContents()
        {
            if (_group == null) return;
            var group = _group;
            string expected = GroupDeletion.DeleteDescription(group);
            string name = group.name;
            Close();
            GroupDeletion.DeleteWithContents(group);
            ToastNotification.ShowIfAvailable(Loc.F("toast.deleted", name), 5f, Loc.T("common.undo"), () =>
            {
                if (CommandStack.CanUndo && CommandStack.PeekUndoDescription() == expected) CommandStack.Undo();
            });
        }

        private void DoEditModule()
        {
            if (_group == null) return;
            var g = _group;
            Close();
            ModuleEditMode.Enter(g);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.DeselectAll();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                TryCloseFromEscape();
        }

        internal void TryCloseFromEscape()
        {
            if (OwnsEscape()) Close();
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                ModalOpen = ModalPresence.IsOpen,
                GroupMenuOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
                ContextMenuOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen,
            }) == EscapeOwner.GroupMenu;

        private void CloseWhenSelectionLeavesTheGroup(KitchenElement? element)
        {
            if (!IsShown(_settings) || _group == null) return;
            if (element == null || GroupManager.GroupOf(element) != _group)
                Close();
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= CloseWhenSelectionLeavesTheGroup;
        }
    }
}

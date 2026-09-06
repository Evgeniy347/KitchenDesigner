using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Lighting;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuLightLinkSection : ContextMenuListSection<string>
    {
        private const float RowDropdownX = -18f, RowDropdownW = 296f;
        private const float AddDropdownX = -66f, AddDropdownW = 200f;
        private const float AddButtonX = 85f, AddButtonW = 90f;
        private const float TrailingButtonX = 150f, TrailingButtonW = 28f;
        private const float LinkRowH = 28f;

        public const string HeaderNode = "CtxLightLinks";
        public const string AddDropdownNode = "CtxLightLinkAdd";
        public const string AddButtonNode = "CtxLightLinkAddBtn";
        public const string PickButtonNode = "CtxLightLinkPick";

        public const string HeaderCaption = "Светильники";
        public const string AddCaption = "Добавить";
        public const string NoLightsInScene = "нет светильников";

        private TMP_Text? _countLabel;
        private TMP_Dropdown? _addDropdown;
        private Button? _pickButton;
        private readonly TMP_Dropdown?[] _rowLight =
            new TMP_Dropdown?[SwitchLightLinks.MaxLightsPerSwitch];
        private readonly List<string> _options = new List<string>();
        private readonly List<string> _links = new List<string>();

        public ContextMenuLightLinkSection(IContextMenuHost host) : base(host)
        {
        }

        private ILightSwitch? Target
            => Host.Target != null ? Host.Target as ILightSwitch : null;

        public override bool Eligible() => Target != null;

        protected override IReadOnlyList<string>? CurrentItems() => Target?.LightNames;

        public override int Count() => _links.Count;

        public void Build(Transform parent)
        {
            var headerBtn = UIFactory.CreateButton(HeaderNode, parent, HeaderCaption,
                new Vector2(0, 0), new Vector2(RowWidth, BtnH), Toggle);
            _countLabel = headerBtn.GetComponentInChildren<TMP_Text>();
            Host.Layout.AddWhen(Eligible, BtnH, RowGap, headerBtn.GetComponent<RectTransform>());

            for (int i = 0; i < SwitchLightLinks.MaxLightsPerSwitch; i++)
            {
                int index = i;
                var lightDd = UIFactory.CreateDropdown($"CtxLightLink{i}", parent,
                    new List<string>(), new Vector2(RowDropdownX, 0),
                    new Vector2(RowDropdownW, LinkRowH), _ => Replace(index));
                var delBtn = UIFactory.CreateConfirmDeleteButton($"CtxLightLinkDel{i}", parent,
                    UIStyle.GlyphClose, new Vector2(TrailingButtonX, 0),
                    new Vector2(TrailingButtonW, LinkRowH), () => Remove(index));

                _rowLight[i] = lightDd;
                Host.Layout.AddWhen(
                    () => Eligible() && Expanded && Count() > index, LinkRowH, 4f,
                    lightDd.GetComponent<RectTransform>(),
                    delBtn.GetComponent<RectTransform>());
            }

            _addDropdown = UIFactory.CreateDropdown(AddDropdownNode, parent, new List<string>(),
                new Vector2(AddDropdownX, 0), new Vector2(AddDropdownW, LinkRowH), _ => { });
            var addBtn = UIFactory.CreateButton(AddButtonNode, parent, AddCaption,
                new Vector2(AddButtonX, 0), new Vector2(AddButtonW, LinkRowH), AddFromUI);
            _pickButton = UIFactory.CreateIconButton(PickButtonNode, parent, IconFactory.Crosshair,
                new Vector2(TrailingButtonX, 0), new Vector2(TrailingButtonW, LinkRowH),
                TogglePicking);

            Host.Layout.AddWhen(() => Eligible() && Expanded, LinkRowH, ActionGap,
                _addDropdown.GetComponent<RectTransform>(),
                addBtn.GetComponent<RectTransform>(),
                _pickButton.GetComponent<RectTransform>());
        }

        public override void Collapse()
        {
            base.Collapse();
            LightPickMode.SetSource(null);
        }

        protected override void OnToggled()
        {
            if (!Expanded) LightPickMode.SetSource(null);
        }

        private void TogglePicking()
        {
            if (Target == null) return;
            LightPickMode.Toggle(Target);
            Refresh();
        }

        private void AddFromUI()
        {
            if (Target == null || _addDropdown == null) return;
            if (_options.Count == 0) return;

            int index = _addDropdown.value;
            if (index < 0 || index >= _options.Count) return;

            if (Count() >= SwitchLightLinks.MaxLightsPerSwitch)
            {
                ToastNotification.ShowIfAvailable(
                    $"Не больше {SwitchLightLinks.MaxLightsPerSwitch} светильников на выключатель");
                return;
            }

            LinkLights.Add(Target, _options[index]);
            Expanded = true;
            AfterChange();
        }

        private void Remove(int index)
        {
            if (Target == null) return;
            LinkLights.RemoveAt(Target, index);
            AfterChange();
        }

        private void Replace(int index)
        {
            if (Target == null) return;
            var dropdown = _rowLight[index];
            if (dropdown == null) return;
            if (dropdown.value < 0 || dropdown.value >= _options.Count) return;

            LinkLights.ReplaceAt(Target, index, _options[dropdown.value]);
            AfterChange();
        }

        protected override void RefreshRows()
        {
            RebuildOptions();

            _links.Clear();
            if (Target != null) _links.AddRange(LinkLights.Live(Target));

            if (_countLabel != null)
                _countLabel.text = $"{HeaderCaption} ({_links.Count})  "
                    + (Expanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed);

            for (int i = 0; i < _rowLight.Length; i++)
            {
                if (i >= _links.Count) continue;
                _rowLight[i]?.SetValueWithoutNotify(_options.IndexOf(_links[i]));
                _rowLight[i]?.RefreshShownValue();
            }

            if (_pickButton != null)
                _pickButton.interactable = Target != null && _options.Count > 0;
        }

        private void RebuildOptions()
        {
            _options.Clear();
            _options.AddRange(LightSwitchNetwork.LiveLightNames());

            var shown = _options.Count > 0
                ? new List<string>(_options)
                : new List<string> { NoLightsInScene };

            Fill(_addDropdown, shown);
            foreach (var dropdown in _rowLight) Fill(dropdown, shown);
        }

        private static void Fill(TMP_Dropdown? dropdown, List<string> options)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            UIFactory.FitDropdownItems(dropdown);
        }
    }
}

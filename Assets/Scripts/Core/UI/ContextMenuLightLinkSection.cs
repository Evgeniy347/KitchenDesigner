using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Lighting;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuLightLinkSection : ContextMenuListSection<string>
    {
        private const float AddButtonW = 88f;

        public const string SectionId = "LightLinks";
        public const string AddDropdownNode = "CtxLightLinkAdd";
        public const string AddButtonNode = "CtxLightLinkAddBtn";
        public const string PickButtonNode = "CtxLightLinkPick";

        public static string HeaderCaption => Loc.T("element.lightSwitch.lights");
        public static string AddCaption => Loc.T("common.add");
        public static string NoLightsInScene => Loc.T("element.lightSwitch.noLights");

        private TMP_Dropdown? _addDropdown;
        private Button? _pickButton;
        private Button? _addButton;
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

        public void Build()
        {
            var rows = Host.Rows;
            BeginSection(SectionId, HeaderCaption, true);
            Section.Header.VisibleWhen = Eligible;

            float cell = UIStyle.ControlHCompact;
            float gap = UIStyle.Space1;
            float width = rows.Metrics.Width;
            for (int i = 0; i < SwitchLightLinks.MaxLightsPerSwitch; i++)
            {
                int index = i;
                var row = rows.NewRowRect("CtxLightLinkRow" + i);
                float dropdownW = width - cell - gap;
                var lightDd = UIFactory.CreateDropdown($"CtxLightLink{i}", row,
                    new List<string>(), Vector2.zero,
                    new Vector2(dropdownW, rows.Metrics.ControlH), _ => Replace(index));
                var delBtn = QuietDeleteButton.Create($"CtxLightLinkDel{i}", row, cell,
                    Loc.T("common.delete"), () => Remove(index));
                rows.PlaceCell((RectTransform)lightDd.transform, 0f, dropdownW);
                rows.PlaceCell((RectTransform)delBtn.transform, width - cell, cell);

                _rowLight[i] = lightDd;
                rows.Custom(row, rows.Metrics.ControlH,
                    RowVisibility.When(() => Eligible() && Count() > index));
            }

            var addRow = rows.NewRowRect("CtxLightLinkAddRow");
            float addW = AddButtonW;
            float addDropdownW = width - addW - cell - 2f * gap;
            _addDropdown = UIFactory.CreateDropdown(AddDropdownNode, addRow, new List<string>(),
                Vector2.zero, new Vector2(addDropdownW, rows.Metrics.ControlH), _ => { });
            _addButton = UIFactory.CreateButton(AddButtonNode, addRow, AddCaption,
                Vector2.zero, new Vector2(addW, rows.Metrics.ControlH), AddFromUI);
            _pickButton = UIFactory.CreateIconButton(PickButtonNode, addRow, IconFactory.Crosshair,
                Vector2.zero, new Vector2(cell, cell), TogglePicking);
            rows.PlaceCell((RectTransform)_addDropdown.transform, 0f, addDropdownW);
            rows.PlaceCell((RectTransform)_addButton.transform, addDropdownW + gap, addW);
            rows.PlaceCell((RectTransform)_pickButton.transform, width - cell, cell);
            rows.Custom(addRow, rows.Metrics.ControlH, RowVisibility.When(Eligible));
        }

        public void ForgetPicking() => LightPickMode.SetSource(null);

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
                    Loc.F("toast.lightLinkLimit", SwitchLightLinks.MaxLightsPerSwitch));
                return;
            }

            LinkLights.Add(Target, _options[index]);
            EnsureExpanded();
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

            Section.SetCount(NumberFormat.Integer(_links.Count));

            for (int i = 0; i < _rowLight.Length; i++)
            {
                if (i >= _links.Count) continue;
                _rowLight[i]?.SetValueWithoutNotify(_options.IndexOf(_links[i]));
                _rowLight[i]?.RefreshShownValue();
            }

            if (_addButton != null) _addButton.interactable = Target != null && _options.Count > 0;
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

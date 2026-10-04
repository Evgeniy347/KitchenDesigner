using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Ventilation;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DuctFieldsEditor : NumberFieldsEditor
    {
        public const string ProfileNode = "CtxDuctProfile";
        public const string DiameterNode = "CtxDuctDiameter";
        public const string WidthNode = "CtxDuctWidth";
        public const string HeightNode = "CtxDuctHeight";
        public const string AirflowNode = "CtxDuctAirflow";

        private TMP_Dropdown? _profile;

        public DuctFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is DuctElement;

        public override bool DepthEditable => false;

        public override void Build()
        {
            var isDuct = RowVisibility.When(() => Host.Target is DuctElement);
            var isRound = RowVisibility.When(() =>
                Host.Target is DuctElement d && d.ProfileKind == DuctProfileKind.Round);
            var isRect = RowVisibility.When(() =>
                Host.Target is DuctElement d && d.ProfileKind == DuctProfileKind.Rect);

            _profile = Rows.Dropdown(Loc.T("element.duct.profile"), new List<string> { Loc.T("element.duct.profileRound"), Loc.T("element.duct.profileRect") },
                OnProfileSelected, isDuct, ProfileNode, hint: "element.duct.profile");

            var diameterRow = Rows.NumberField(Loc.T("element.duct.diameter"), isRound, Loc.T("unit.mm"), DiameterNode,
                hint: "element.duct.diameter");
            Bind<DuctElement>(diameterRow, d => d.DiameterMm, (d, v) => d.DiameterMm = v,
                DuctDefaults.DefaultRoundDiameterMm.ToString());

            var widthRow = Rows.NumberField(Loc.T("element.duct.width"), isRect, Loc.T("unit.mm"), WidthNode,
                hint: "element.duct.width");
            Bind<DuctElement>(widthRow, d => d.RectWidthMm, (d, v) => d.RectWidthMm = v,
                DuctDefaults.DefaultRectWidthMm.ToString());

            var heightRow = Rows.NumberField(Loc.T("element.duct.height"), isRect, Loc.T("unit.mm"), HeightNode,
                hint: "element.duct.height");
            Bind<DuctElement>(heightRow, d => d.RectHeightMm, (d, v) => d.RectHeightMm = v,
                DuctDefaults.DefaultRectHeightMm.ToString());

            var airflowRow = Rows.NumberField(Loc.T("element.duct.airflow"), isDuct, Loc.T("unit.m3PerHour"), AirflowNode,
                hint: "element.duct.airflow");
            Bind<DuctElement>(airflowRow, d => d.AirflowM3PerHour, (d, v) => d.AirflowM3PerHour = v,
                DuctDefaults.DefaultAirflowM3PerHour.ToString());
        }

        public override void Show(KitchenElement element)
        {
            base.Show(element);
            WriteWidgets(element);
        }

        public override void Refresh(KitchenElement element)
        {
            base.Refresh(element);
            WriteWidgets(element);
        }

        public override void AfterApply(KitchenElement element)
        {
            base.AfterApply(element);
            WriteWidgets(element);
        }

        private void WriteWidgets(KitchenElement element)
        {
            if (!(element is DuctElement duct)) return;
            _profile?.SetValueWithoutNotify((int)duct.ProfileKind);
            _profile?.RefreshShownValue();
        }

        private void OnProfileSelected(int index)
        {
            if (Host.Target is DuctElement duct)
                ChoiceRowUndo.Commit(duct, () => duct.ProfileKind = (DuctProfileKind)index);
        }
    }
}

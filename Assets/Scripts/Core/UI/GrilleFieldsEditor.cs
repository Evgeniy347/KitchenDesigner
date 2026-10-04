using KitchenDesigner.Core.Ventilation;

namespace KitchenDesigner.Core.UI
{
    internal sealed class GrilleFieldsEditor : NumberFieldsEditor
    {
        public const string AirflowNode = "CtxGrilleAirflow";

        public GrilleFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is GrilleElement;

        public override bool DepthEditable => false;

        public override void Build()
        {
            var isGrille = RowVisibility.When(() => Host.Target is GrilleElement);

            var airflowRow = Rows.NumberField(Loc.T("element.grille.airflow"), isGrille, Loc.T("unit.m3PerHour"), AirflowNode,
                hint: "element.grille.airflow");
            Bind<GrilleElement>(airflowRow, g => g.AirflowM3PerHour, (g, v) => g.AirflowM3PerHour = v,
                GrilleDefaults.DefaultAirflowM3PerHour.ToString());
        }
    }
}

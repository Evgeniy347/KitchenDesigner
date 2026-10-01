namespace KitchenDesigner.Core.UI
{
    internal sealed class BathMixerFieldsEditor : NumberFieldsEditor
    {
        public const string CentresNode = "МежосевоеСмесителя";
        public const string BodyLengthNode = "ДлинаКорпусаСмесителя";
        public const string BodyDiameterNode = "ДиаметрКорпусаСмесителя";
        public const string EscutcheonReachNode = "ВылетОтражателяСмесителя";
        public const string SpoutLengthNode = "ДлинаИзливаСмесителя";
        public const string OutletDiameterNode = "ДиаметрШтуцераСмесителя";

        public static string CentresLabel => Loc.T("element.bathMixer.centres");
        public static string BodyLengthLabel => Loc.T("element.bathMixer.bodyLength");
        public static string BodyDiameterLabel => Loc.T("element.bathMixer.bodyDiameter");
        public static string EscutcheonReachLabel => Loc.T("element.bathMixer.escutcheonReach");
        public static string SpoutLengthLabel => Loc.T("element.bathMixer.spout");
        public static string OutletDiameterLabel => Loc.T("element.bathMixer.outletDiameter");

        public BathMixerFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is BathMixerElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is BathMixerElement);

            var centresRow = Rows.NumberField(CentresLabel, visibility, Loc.T("unit.mm"), CentresNode,
                hint: "element.bathMixer.centres");
            var bodyLengthRow = Rows.NumberField(BodyLengthLabel, visibility, Loc.T("unit.mm"),
                BodyLengthNode, hint: "element.bathMixer.bodyLength");
            var bodyDiameterRow = Rows.NumberField(BodyDiameterLabel, visibility, Loc.T("unit.mm"),
                BodyDiameterNode, hint: "element.bathMixer.bodyDiameter");
            var reachRow = Rows.NumberField(EscutcheonReachLabel, visibility, Loc.T("unit.mm"),
                EscutcheonReachNode, hint: "element.bathMixer.escutcheonReach");
            var spoutRow = Rows.NumberField(SpoutLengthLabel, visibility, Loc.T("unit.mm"), SpoutLengthNode,
                hint: "element.bathMixer.spout");
            var outletRow = Rows.NumberField(OutletDiameterLabel, visibility, Loc.T("unit.mm"),
                OutletDiameterNode, hint: "element.bathMixer.outletDiameter");

            Bind<BathMixerElement>(bodyDiameterRow, mixer => mixer.BodyDiameterMM,
                (mixer, value) => mixer.BodyDiameterMM = value,
                BathMixerSpec.DefaultBodyDiameterMM.ToString());
            Bind<BathMixerElement>(bodyLengthRow, mixer => mixer.BodyLengthMM,
                (mixer, value) => mixer.BodyLengthMM = value,
                BathMixerSpec.DefaultBodyLengthMM.ToString());
            Bind<BathMixerElement>(centresRow, mixer => mixer.CentresMM,
                (mixer, value) => mixer.CentresMM = value,
                BathMixerSpec.DefaultCentresMM.ToString());
            Bind<BathMixerElement>(reachRow, mixer => mixer.EscutcheonReachMM,
                (mixer, value) => mixer.EscutcheonReachMM = value,
                BathMixerSpec.DefaultEscutcheonReachMM.ToString());
            Bind<BathMixerElement>(spoutRow, mixer => mixer.SpoutLengthMM,
                (mixer, value) => mixer.SpoutLengthMM = value,
                BathMixerSpec.DefaultSpoutLengthMM.ToString());
            Bind<BathMixerElement>(outletRow, mixer => mixer.OutletDiameterMM,
                (mixer, value) => mixer.OutletDiameterMM = value,
                BathMixerSpec.DefaultOutletDiameterMM.ToString());
        }
    }
}

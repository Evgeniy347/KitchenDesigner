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

        public const string CentresLabel = "Межосевое";
        public const string BodyLengthLabel = "Длина корпуса";
        public const string BodyDiameterLabel = "Ø корпуса";
        public const string EscutcheonReachLabel = "Вылет отражателя";
        public const string SpoutLengthLabel = "Длина излива";
        public const string OutletDiameterLabel = "Ø штуцера";

        public BathMixerFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is BathMixerElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is BathMixerElement);

            var centresRow = Rows.NumberField(CentresLabel, visibility, "мм", CentresNode,
                hint: "element.bathMixer.centres");
            var bodyLengthRow = Rows.NumberField(BodyLengthLabel, visibility, "мм",
                BodyLengthNode);
            var bodyDiameterRow = Rows.NumberField(BodyDiameterLabel, visibility, "мм",
                BodyDiameterNode);
            var reachRow = Rows.NumberField(EscutcheonReachLabel, visibility, "мм",
                EscutcheonReachNode);
            var spoutRow = Rows.NumberField(SpoutLengthLabel, visibility, "мм", SpoutLengthNode,
                hint: "element.bathMixer.spout");
            var outletRow = Rows.NumberField(OutletDiameterLabel, visibility, "мм",
                OutletDiameterNode);

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

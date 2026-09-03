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

            var centresRow = Rows.NumberField(CentresLabel, visibility, "мм", CentresNode);
            var bodyLengthRow = Rows.NumberField(BodyLengthLabel, visibility, "мм",
                BodyLengthNode);
            var bodyDiameterRow = Rows.NumberField(BodyDiameterLabel, visibility, "мм",
                BodyDiameterNode);
            var reachRow = Rows.NumberField(EscutcheonReachLabel, visibility, "мм",
                EscutcheonReachNode);
            var spoutRow = Rows.NumberField(SpoutLengthLabel, visibility, "мм", SpoutLengthNode);
            var outletRow = Rows.NumberField(OutletDiameterLabel, visibility, "мм",
                OutletDiameterNode);

            Bind(bodyDiameterRow, BodyDiameterOf, SetBodyDiameter,
                BathMixerSpec.DefaultBodyDiameterMM.ToString());
            Bind(bodyLengthRow, BodyLengthOf, SetBodyLength,
                BathMixerSpec.DefaultBodyLengthMM.ToString());
            Bind(centresRow, CentresOf, SetCentres, BathMixerSpec.DefaultCentresMM.ToString());
            Bind(reachRow, ReachOf, SetReach,
                BathMixerSpec.DefaultEscutcheonReachMM.ToString());
            Bind(spoutRow, SpoutOf, SetSpout, BathMixerSpec.DefaultSpoutLengthMM.ToString());
            Bind(outletRow, OutletOf, SetOutlet,
                BathMixerSpec.DefaultOutletDiameterMM.ToString());
        }

        private static int CentresOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.CentresMM
                : BathMixerSpec.DefaultCentresMM;

        private static void SetCentres(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.CentresMM = value;
        }

        private static int BodyLengthOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.BodyLengthMM
                : BathMixerSpec.DefaultBodyLengthMM;

        private static void SetBodyLength(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.BodyLengthMM = value;
        }

        private static int BodyDiameterOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.BodyDiameterMM
                : BathMixerSpec.DefaultBodyDiameterMM;

        private static void SetBodyDiameter(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.BodyDiameterMM = value;
        }

        private static int ReachOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.EscutcheonReachMM
                : BathMixerSpec.DefaultEscutcheonReachMM;

        private static void SetReach(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.EscutcheonReachMM = value;
        }

        private static int SpoutOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.SpoutLengthMM
                : BathMixerSpec.DefaultSpoutLengthMM;

        private static void SetSpout(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.SpoutLengthMM = value;
        }

        private static int OutletOf(KitchenElement element) =>
            element is BathMixerElement mixer
                ? mixer.OutletDiameterMM
                : BathMixerSpec.DefaultOutletDiameterMM;

        private static void SetOutlet(KitchenElement element, int value)
        {
            if (element is BathMixerElement mixer) mixer.OutletDiameterMM = value;
        }
    }
}

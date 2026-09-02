using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct BathMixerSpec
    {
        public const int DefaultCentresMM = 150;
        public const int DefaultBodyLengthMM = 270;
        public const int DefaultBodyDiameterMM = 70;
        public const int DefaultEscutcheonReachMM = 34;
        public const int DefaultSpoutLengthMM = 110;
        public const int DefaultOutletDiameterMM = 13;

        public const int MinCentresMM = 60;
        public const int MinBodyLengthMM = 150;
        public const int MaxBodyLengthMM = 600;
        public const int MinBodyDiameterMM = 30;
        public const int MaxBodyDiameterMM = 160;
        public const int MinEscutcheonReachMM = 10;
        public const int MaxEscutcheonReachMM = 200;
        public const int MinSpoutLengthMM = 40;
        public const int MaxSpoutLengthMM = 400;
        public const int MinOutletDiameterMM = 8;
        public const int MaxOutletDiameterMM = 40;

        public readonly int CentresMM;
        public readonly int BodyLengthMM;
        public readonly int BodyDiameterMM;
        public readonly int EscutcheonReachMM;
        public readonly int SpoutLengthMM;
        public readonly int OutletDiameterMM;

        private BathMixerSpec(int centresMM, int bodyLengthMM, int bodyDiameterMM,
            int escutcheonReachMM, int spoutLengthMM, int outletDiameterMM)
        {
            CentresMM = centresMM;
            BodyLengthMM = bodyLengthMM;
            BodyDiameterMM = bodyDiameterMM;
            EscutcheonReachMM = escutcheonReachMM;
            SpoutLengthMM = spoutLengthMM;
            OutletDiameterMM = outletDiameterMM;
        }

        public static BathMixerSpec Default => Clamped(DefaultCentresMM, DefaultBodyLengthMM,
            DefaultBodyDiameterMM, DefaultEscutcheonReachMM, DefaultSpoutLengthMM,
            DefaultOutletDiameterMM);

        public static BathMixerSpec Clamped(int centresMM, int bodyLengthMM, int bodyDiameterMM,
            int escutcheonReachMM, int spoutLengthMM, int outletDiameterMM)
        {
            int diameter = ClampBodyDiameterMM(bodyDiameterMM);
            int length = ClampBodyLengthMM(bodyLengthMM, diameter);

            return new BathMixerSpec(
                ClampCentresMM(centresMM, length, diameter),
                length,
                diameter,
                ClampEscutcheonReachMM(escutcheonReachMM),
                ClampSpoutLengthMM(spoutLengthMM),
                ClampOutletDiameterMM(outletDiameterMM));
        }

        public static int ClampBodyDiameterMM(int value) =>
            Mathf.Clamp(value, MinBodyDiameterMM, MaxBodyDiameterMM);

        public static int MinBodyLengthForMM(int bodyDiameterMM) =>
            Mathf.Max(MinBodyLengthMM, ClampBodyDiameterMM(bodyDiameterMM) + MinCentresMM);

        public static int ClampBodyLengthMM(int value, int bodyDiameterMM) =>
            Mathf.Clamp(value, MinBodyLengthForMM(bodyDiameterMM), MaxBodyLengthMM);

        public static int MaxCentresForMM(int bodyLengthMM, int bodyDiameterMM) =>
            ClampBodyLengthMM(bodyLengthMM, bodyDiameterMM) - ClampBodyDiameterMM(bodyDiameterMM);

        public static int ClampCentresMM(int value, int bodyLengthMM, int bodyDiameterMM) =>
            Mathf.Clamp(value, MinCentresMM, MaxCentresForMM(bodyLengthMM, bodyDiameterMM));

        public static int ClampEscutcheonReachMM(int value) =>
            Mathf.Clamp(value, MinEscutcheonReachMM, MaxEscutcheonReachMM);

        public static int ClampSpoutLengthMM(int value) =>
            Mathf.Clamp(value, MinSpoutLengthMM, MaxSpoutLengthMM);

        public static int ClampOutletDiameterMM(int value) =>
            Mathf.Clamp(value, MinOutletDiameterMM, MaxOutletDiameterMM);
    }
}

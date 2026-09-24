using System;

namespace KitchenDesigner.Core.Ventilation
{
    public enum DuctProfileKind
    {
        Round,
        Rect,
    }

    public readonly struct DuctProfile
    {
        public const int MinRoundDiameterMm = 100;
        public const int MaxRoundDiameterMm = 315;

        public readonly DuctProfileKind Kind;
        public readonly int DiameterMm;
        public readonly int WidthMm;
        public readonly int HeightMm;

        private DuctProfile(DuctProfileKind kind, int diameterMm, int widthMm, int heightMm)
        {
            Kind = kind;
            DiameterMm = diameterMm;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public static DuctProfile Round(int diameterMm)
        {
            if (diameterMm < MinRoundDiameterMm || diameterMm > MaxRoundDiameterMm)
                throw new ArgumentOutOfRangeException(nameof(diameterMm), diameterMm,
                    "Round duct diameter is 100..315 mm (docs/todo_evolution.md 3.5)");
            return new DuctProfile(DuctProfileKind.Round, diameterMm, 0, 0);
        }

        public static DuctProfile Rect(int widthMm, int heightMm)
        {
            if (widthMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(widthMm), widthMm,
                    "Duct width must be positive");
            if (heightMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(heightMm), heightMm,
                    "Duct height must be positive");
            return new DuctProfile(DuctProfileKind.Rect, 0, widthMm, heightMm);
        }

        public string ProfileId => Kind == DuctProfileKind.Round
            ? "round" + DiameterMm
            : "rect" + WidthMm + "x" + HeightMm;
    }
}

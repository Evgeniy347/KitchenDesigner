using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class UiScale
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float Floor = 0.8125f;
        public const int AutoPercent = 100;
        public const int MinPercent = 90;
        public const int MaxPercent = 150;

        public static readonly IReadOnlyList<int> ChoicePercents = new[] { AutoPercent, 90, 110, 125, MaxPercent };

        public static float Fit(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return 1f;
            return (float)Math.Sqrt(screenWidth / ReferenceWidth * (screenHeight / ReferenceHeight));
        }

        public static float Automatic(float screenWidth, float screenHeight) =>
            Math.Max(Floor, Fit(screenWidth, screenHeight));

        public static int ClampPercent(int percent) =>
            percent < MinPercent ? MinPercent : percent > MaxPercent ? MaxPercent : percent;

        public static float Factor(float screenWidth, float screenHeight, int userPercent) =>
            Automatic(screenWidth, screenHeight) * ClampPercent(userPercent) / 100f;

        public static float CanvasHeight(float screenHeight, float factor) =>
            factor > 0f ? screenHeight / factor : screenHeight;

        public static float ScaleFactorFor(float realScreenHeight, float emulatedWidth, float emulatedHeight, int userPercent)
        {
            float factor = Factor(emulatedWidth, emulatedHeight, userPercent);
            if (realScreenHeight <= 0f || emulatedHeight <= 0f) return factor;
            return factor * realScreenHeight / emulatedHeight;
        }

        public static int ChoiceIndex(int percent)
        {
            for (int i = 0; i < ChoicePercents.Count; i++)
                if (ChoicePercents[i] == percent) return i;
            return 0;
        }
    }
}

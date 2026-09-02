using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class LightSwitchLayout
    {
        public const float KeyGapRatio = 0.15f;
        public const float MinKeyGapMM = 0.4f;
        public const float KeyCornerRatio = 0.12f;

        public const string BackName = "SwitchBack";
        public const string KeyName = "SwitchKey";

        public static float KeyGapMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinKeyGapMM,
                WallDeviceLayout.RimWidthMM(plateWidthMM, plateHeightMM) * KeyGapRatio);

        public static float KeyWidthMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinKeyGapMM, WallDeviceLayout.InnerWidthMM(plateWidthMM, plateHeightMM)
                - 2f * KeyGapMM(plateWidthMM, plateHeightMM));

        public static float KeyHeightMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinKeyGapMM, WallDeviceLayout.InnerHeightMM(plateWidthMM, plateHeightMM)
                - 2f * KeyGapMM(plateWidthMM, plateHeightMM));

        public static float KeyDepthMM(int protrusionMM)
            => WallDeviceLayout.FrameDepthMM(protrusionMM)
                + WallDeviceLayout.RecessDepthMM(protrusionMM);

        public static float KeyCentreZMM(int protrusionMM)
            => WallDeviceLayout.FrontZMM(protrusionMM) - KeyDepthMM(protrusionMM) * 0.5f;

        public static List<FurniturePartBox> BodyParts(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
        {
            var parts = new List<FurniturePartBox>();
            int posts = WallDeviceLayout.ClampPostCount(postCount);
            float innerW = WallDeviceLayout.InnerWidthMM(plateWidthMM, plateHeightMM);
            float innerH = WallDeviceLayout.InnerHeightMM(plateWidthMM, plateHeightMM);
            float backDepth = WallDeviceLayout.BodyDepthMM(protrusionMM);
            float backZ = WallDeviceLayout.BodyCentreZMM(protrusionMM);

            for (int post = 0; post < posts; post++)
            {
                float x = WallDeviceLayout.PostCentreXMM(post, plateWidthMM, posts);
                WallDeviceLayout.AddRim(parts, post, x, plateWidthMM, plateHeightMM, protrusionMM);
                parts.Add(WallDeviceLayout.Bar(BackName + post, x, 0f, backZ,
                    innerW, innerH, backDepth, 0f));
            }

            return parts;
        }

        public static List<FurniturePartBox> KeyParts(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
        {
            var parts = new List<FurniturePartBox>();
            int posts = WallDeviceLayout.ClampPostCount(postCount);
            float keyW = KeyWidthMM(plateWidthMM, plateHeightMM);
            float keyH = KeyHeightMM(plateWidthMM, plateHeightMM);
            float keyDepth = KeyDepthMM(protrusionMM);
            float keyZ = KeyCentreZMM(protrusionMM);
            float radius = Mathf.Min(keyW, keyH) * KeyCornerRatio;

            for (int post = 0; post < posts; post++)
                parts.Add(WallDeviceLayout.Bar(KeyName + post,
                    WallDeviceLayout.PostCentreXMM(post, plateWidthMM, posts), 0f, keyZ,
                    keyW, keyH, keyDepth, radius));

            return parts;
        }
    }
}

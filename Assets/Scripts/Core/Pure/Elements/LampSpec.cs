namespace KitchenDesigner.Core
{
    public enum LampShape
    {
        Plafond = 0,
        Sphere = 1,
    }

    public enum LampShadow
    {
        None = 0,
        Hard = 1,
        Soft = 2,
    }

    public static class LampSpec
    {
        public const int DEFAULT_SIZE_MM = 150;

        public const int DEFAULT_TEMPERATURE_K = 3000;
        public const int DEFAULT_POWER_W = 9;
        public const int MIN_TEMPERATURE_K = 1500;
        public const int MAX_TEMPERATURE_K = 10000;

        public const int DEFAULT_DIFFUSION_PCT = 50;

        public const int DEFAULT_RANGE_MIN_MM = 2000;
        public const int DEFAULT_RANGE_MAX_MM = 16000;
        public const int MIN_RANGE_MM = 100;
        public const int MAX_RANGE_MM = 60000;

        public const int DEFAULT_DROP_MM = 120;
        public const int MAX_DROP_MM = 1000;

        public const int DEFAULT_UP_RANGE_PCT = 50;
        public const int DEFAULT_UP_CONE_PCT = 66;
        public const int MAX_FRACTION_PCT = 200;

        public const int DEFAULT_UP_PCT = 8;
        public const int MAX_UP_PCT = 50;

        public const int DEFAULT_BEAM_DEG = 150;
        public const int MIN_BEAM_DEG = 20;
        public const int MAX_BEAM_DEG = 175;

        public const int DEFAULT_SOFTNESS_PCT = 30;

        public const int DEFAULT_EFFICACY_LM_PER_W = 110;
        public const int MAX_EFFICACY_LM_PER_W = 400;
        public const int DEFAULT_LUMENS_PER_UNIT = 700;
        public const int MIN_LUMENS_PER_UNIT = 10;
        public const int MAX_LUMENS_PER_UNIT = 10000;

        public const int DEFAULT_GLOW_PCT = 100;
        public const int MAX_GLOW_PCT = 400;

        public const int DEFAULT_SHADOW_STRENGTH_PCT = 70;

        public const LampShape DEFAULT_SHAPE = LampShape.Plafond;
        public const LampShadow DEFAULT_SHADOW = LampShadow.None;
    }
}

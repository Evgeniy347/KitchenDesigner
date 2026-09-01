namespace KitchenDesigner.Core
{
    public static class ScrewLegSpec
    {
        public const string ThreadM6 = "M6";
        public const string ThreadM8 = "M8";
        public const string ThreadM10 = "M10";

        public const string DEFAULT_THREAD = ThreadM6;

        public static readonly string[] Threads = { ThreadM6, ThreadM8, ThreadM10 };

        public const int DEFAULT_THREAD_LENGTH_MM = 50;
        public const int MIN_THREAD_LENGTH_MM = 5;
        public const int MAX_THREAD_LENGTH_MM = 1000;

        public const int DEFAULT_INSERTION_MM = 25;
        public const int MIN_INSERTION_MM = 1;

        public const int DEFAULT_BASE_DIAMETER_MM = 25;
        public const int MIN_BASE_DIAMETER_MM = 5;
        public const int MAX_BASE_DIAMETER_MM = 200;

        public const int DEFAULT_BASE_HEIGHT_MM = 8;
        public const int MIN_BASE_HEIGHT_MM = 1;
        public const int MAX_BASE_HEIGHT_MM = 200;

        public const int CENTRING_REQUIRED_SPAN_MM = 25;

        public const float CENTRE_TOLERANCE_MM = 0.5f;

        public static int ThreadDiameterMM(string? thread) => thread switch
        {
            ThreadM8 => 8,
            ThreadM10 => 10,
            _ => 6,
        };

        public static string NormalizeThread(string? thread)
        {
            foreach (var known in Threads)
                if (string.Equals(known, thread, System.StringComparison.OrdinalIgnoreCase))
                    return known;
            return DEFAULT_THREAD;
        }

        public static int ClampThreadLengthMM(int lengthMM) =>
            lengthMM < MIN_THREAD_LENGTH_MM ? MIN_THREAD_LENGTH_MM
            : lengthMM > MAX_THREAD_LENGTH_MM ? MAX_THREAD_LENGTH_MM
            : lengthMM;

        public static int ClampInsertionMM(int insertionMM, int threadLengthMM)
        {
            int max = ClampThreadLengthMM(threadLengthMM);
            return insertionMM < MIN_INSERTION_MM ? MIN_INSERTION_MM
                : insertionMM > max ? max
                : insertionMM;
        }

        public static int ClampBaseDiameterMM(int diameterMM) =>
            diameterMM < MIN_BASE_DIAMETER_MM ? MIN_BASE_DIAMETER_MM
            : diameterMM > MAX_BASE_DIAMETER_MM ? MAX_BASE_DIAMETER_MM
            : diameterMM;

        public static int ClampBaseHeightMM(int heightMM) =>
            heightMM < MIN_BASE_HEIGHT_MM ? MIN_BASE_HEIGHT_MM
            : heightMM > MAX_BASE_HEIGHT_MM ? MAX_BASE_HEIGHT_MM
            : heightMM;

        public static int BodyHeightMM(int threadLengthMM, int baseHeightMM) =>
            threadLengthMM + baseHeightMM;

        public static int HeightAboveFloorMM(int threadLengthMM, int insertionMM, int baseHeightMM) =>
            threadLengthMM - insertionMM + baseHeightMM;

        public static int ThreadLengthForHeightMM(int heightAboveFloorMM, int insertionMM,
            int baseHeightMM) =>
            ClampThreadLengthMM(heightAboveFloorMM + insertionMM - baseHeightMM);

        public static int ProtrusionMM(int insertionMM, int hostThicknessMM) =>
            insertionMM > hostThicknessMM ? insertionMM - hostThicknessMM : 0;

        public static bool NeedsCentring(float faceSpanMM) =>
            faceSpanMM < CENTRING_REQUIRED_SPAN_MM;

        public static bool IsCentred(float offsetMM) =>
            (offsetMM < 0f ? -offsetMM : offsetMM) <= CENTRE_TOLERANCE_MM;
    }
}

namespace KitchenDesigner.Core.UI
{
    public static class HintKey
    {
        public const int MinSegments = 2;
        public const int MaxSegments = 4;
        public const int MaxLength = 60;

        public static bool IsValid(string? key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key!.Length > MaxLength) return false;

            var segments = key.Split('.');
            if (segments.Length < MinSegments || segments.Length > MaxSegments) return false;

            foreach (var segment in segments)
                if (!IsSegment(segment)) return false;

            return true;
        }

        private static bool IsSegment(string segment)
        {
            if (segment.Length == 0) return false;
            if (segment[0] < 'a' || segment[0] > 'z') return false;

            for (int i = 1; i < segment.Length; i++)
            {
                char c = segment[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok) return false;
            }

            return true;
        }
    }
}

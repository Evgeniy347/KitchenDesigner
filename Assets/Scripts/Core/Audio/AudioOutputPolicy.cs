using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public static class AudioOutputPolicy
    {
        private static bool _announcedTestRun;

        public static bool Silent => _announcedTestRun || Application.isBatchMode;

        public static void SilenceForTestRun() => _announcedTestRun = true;
    }
}

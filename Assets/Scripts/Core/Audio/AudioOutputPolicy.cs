using System;
using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public static partial class AudioOutputPolicy
    {
        private static bool _announcedTestRun;

        public static bool Silent =>
            Decide(_announcedTestRun, Application.isBatchMode, Environment.GetCommandLineArgs());

        public static void SilenceForTestRun() => _announcedTestRun = true;
    }
}

using System;
using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public static class AudioOutputPolicy
    {
        private static bool _announcedTestRun;
        private static bool? _mutedByArgument;

        public static bool Silent => _announcedTestRun || Application.isBatchMode || MutedByArgument;

        private static bool MutedByArgument
        {
            get
            {
                _mutedByArgument ??= MuteAudioArgument.Parse(Environment.GetCommandLineArgs());
                return _mutedByArgument.Value;
            }
        }

        public static void SilenceForTestRun() => _announcedTestRun = true;
    }
}

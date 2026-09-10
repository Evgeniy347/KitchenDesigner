namespace KitchenDesigner.Core.Audio
{
    public static partial class AudioOutputPolicy
    {
        public static bool Decide(bool testRun, bool batchMode, string[]? args) =>
            testRun || batchMode || MuteAudioArgument.Parse(args);
    }
}

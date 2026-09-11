using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class FirstRunMarker
    {
        internal const string Key = "KitchenFirstRunDone";

        internal static bool Recorded => PlayerPrefs.GetInt(Key, 0) != 0;

        internal static void Record(string[]? commandLineArgs)
        {
            if (EphemeralSessionArgument.Parse(commandLineArgs)) return;
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }
    }
}

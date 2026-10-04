using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class SettingsSectionReset
    {
        public static void Run(string title, Action<KitchenSettings, KitchenSettings> toDefaults,
            Action afterApply)
        {
            var settings = KitchenSettings.Instance;
            var before = settings.ToData();

            var factory = new KitchenSettings();
            factory.ResetToDefaults();

            var scratch = new KitchenSettings();
            scratch.ApplyFrom(before);
            toDefaults(scratch, factory);
            var after = scratch.ToData();

            if (JsonUtility.ToJson(before) == JsonUtility.ToJson(after)) return;
            SetSettingCommand.Push(title, settings.ApplyFrom, before, after, afterApply);
        }
    }
}

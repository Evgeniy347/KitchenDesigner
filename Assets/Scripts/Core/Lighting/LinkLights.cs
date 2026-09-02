using System.Collections.Generic;

namespace KitchenDesigner.Core.Lighting
{
    public static class LinkLights
    {
        public static void Add(ILightSwitch? source, string? lightName)
        {
            if (source == null || string.IsNullOrEmpty(lightName)) return;
            Apply(source, SwitchLightLinks.WithAdded(source.LightNames, lightName));
        }

        public static void RemoveAt(ILightSwitch? source, int index)
        {
            if (source == null) return;
            Apply(source, SwitchLightLinks.WithRemovedAt(source.LightNames, index));
        }

        public static void ReplaceAt(ILightSwitch? source, int index, string? lightName)
        {
            if (source == null) return;
            Apply(source, SwitchLightLinks.WithReplacedAt(source.LightNames, index, lightName));
        }

        public static List<string> Live(ILightSwitch? source)
            => source == null
                ? new List<string>()
                : SwitchLightLinks.Sanitized(source.LightNames, LightSwitchNetwork.LiveLightNames());

        public static void Prune(ILightSwitch? source)
        {
            if (source == null) return;
            var live = Live(source);
            if (live.Count == source.LightNames.Count) return;
            source.SetLightNames(live);
        }

        private static void Apply(ILightSwitch source, List<string> after)
        {
            var before = new List<string>(source.LightNames);
            if (SameNames(before, after)) return;
            CommandStack.Execute(new SetSwitchLightsCommand(source, before, after));
        }

        private static bool SameNames(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}

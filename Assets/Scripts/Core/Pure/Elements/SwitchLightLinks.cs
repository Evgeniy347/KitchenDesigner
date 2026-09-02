using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class SwitchLightLinks
    {
        public const int MaxLightsPerSwitch = 12;

        public static bool IsControlled(string? lightName,
            IReadOnlyList<SwitchLinkState>? switches)
        {
            if (string.IsNullOrEmpty(lightName) || switches == null) return false;
            for (int i = 0; i < switches.Count; i++)
                if (switches[i].Controls(lightName)) return true;
            return false;
        }

        public static bool AnyControllingSwitchIsOn(string? lightName,
            IReadOnlyList<SwitchLinkState>? switches)
        {
            if (string.IsNullOrEmpty(lightName) || switches == null) return false;
            for (int i = 0; i < switches.Count; i++)
                if (switches[i].IsOn && switches[i].Controls(lightName)) return true;
            return false;
        }

        public static bool IsLit(string? lightName, IReadOnlyList<SwitchLinkState>? switches,
            bool globalOn)
        {
            if (!globalOn) return false;
            if (!IsControlled(lightName, switches)) return true;
            return AnyControllingSwitchIsOn(lightName, switches);
        }

        public static List<string> ControllingSwitchNames(string? lightName,
            IReadOnlyList<SwitchLinkState>? switches)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(lightName) || switches == null) return names;
            for (int i = 0; i < switches.Count; i++)
            {
                if (!switches[i].Controls(lightName)) continue;
                if (names.Contains(switches[i].Name)) continue;
                names.Add(switches[i].Name);
            }
            return names;
        }

        public static List<string> Sanitized(IEnumerable<string>? names,
            ICollection<string>? liveLightNames)
        {
            var kept = new List<string>();
            if (names == null) return kept;
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name) || kept.Contains(name)) continue;
                if (liveLightNames != null && !liveLightNames.Contains(name)) continue;
                if (kept.Count >= MaxLightsPerSwitch) break;
                kept.Add(name);
            }
            return kept;
        }

        public static List<string> Renamed(IEnumerable<string>? names, string? oldName,
            string? newName)
        {
            var renamed = new List<string>();
            if (names == null) return renamed;
            foreach (var name in names)
            {
                string mapped = !string.IsNullOrEmpty(oldName) && name == oldName
                    && !string.IsNullOrEmpty(newName)
                    ? newName!
                    : name;
                if (string.IsNullOrEmpty(mapped) || renamed.Contains(mapped)) continue;
                renamed.Add(mapped);
            }
            return renamed;
        }

        public static List<string> WithAdded(IEnumerable<string>? names, string? added)
        {
            var list = Sanitized(names, null);
            if (string.IsNullOrEmpty(added) || list.Contains(added!)) return list;
            if (list.Count >= MaxLightsPerSwitch) return list;
            list.Add(added!);
            return list;
        }

        public static List<string> WithRemovedAt(IEnumerable<string>? names, int index)
        {
            var list = Sanitized(names, null);
            if (index < 0 || index >= list.Count) return list;
            list.RemoveAt(index);
            return list;
        }

        public static List<string> WithReplacedAt(IEnumerable<string>? names, int index,
            string? replacement)
        {
            var list = Sanitized(names, null);
            if (index < 0 || index >= list.Count) return list;
            if (string.IsNullOrEmpty(replacement)) return list;
            int duplicate = list.IndexOf(replacement!);
            if (duplicate >= 0 && duplicate != index) return list;
            list[index] = replacement!;
            return list;
        }
    }
}

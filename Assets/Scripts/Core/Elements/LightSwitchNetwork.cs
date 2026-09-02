using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class LightSwitchNetwork
    {
        private static readonly List<SwitchLinkState> _states = new List<SwitchLinkState>();
        private static bool _stale = true;

        public static IReadOnlyList<SwitchLinkState> States
        {
            get
            {
                EnsureCollected();
                return _states;
            }
        }

        public static void Invalidate() => _stale = true;

        public static void Reset()
        {
            _states.Clear();
            _stale = true;
        }

        public static void Refresh()
        {
            _stale = true;
            EnsureCollected();
            foreach (var lamp in AllLights()) lamp.SyncLightState();
        }

        public static bool IsLit(string? lightName)
        {
            EnsureCollected();
            return SwitchLightLinks.IsLit(lightName, _states, LightSourceElement.GlobalOn);
        }

        public static List<LightSourceElement> AllLights()
        {
            var lamps = new List<LightSourceElement>();
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var element = all[i];
                if (element == null) continue;
                if (element is LightSourceElement lamp) lamps.Add(lamp);
            }
            return lamps;
        }

        public static List<LightSwitchElement> AllSwitches()
        {
            var switches = new List<LightSwitchElement>();
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var element = all[i];
                if (element == null) continue;
                if (element is LightSwitchElement source) switches.Add(source);
            }
            return switches;
        }

        public static LightSourceElement? LightUnder(Collider? collider)
            => collider == null ? null : collider.GetComponentInParent<LightSourceElement>();

        public static List<string> LiveLightNames()
        {
            var names = new List<string>();
            foreach (var lamp in AllLights())
            {
                if (string.IsNullOrEmpty(lamp.PartName) || names.Contains(lamp.PartName)) continue;
                names.Add(lamp.PartName);
            }
            return names;
        }

        public static LightSourceElement? LightNamed(string? lightName)
        {
            if (string.IsNullOrEmpty(lightName)) return null;
            foreach (var lamp in AllLights())
                if (lamp.PartName == lightName) return lamp;
            return null;
        }

        public static List<Vector3> LinkAnchorsOf(ILightSwitch? source)
        {
            var anchors = new List<Vector3>();
            if (source == null) return anchors;
            foreach (var name in source.LightNames)
            {
                var lamp = LightNamed(name);
                if (lamp != null) anchors.Add(lamp.transform.position);
            }
            return anchors;
        }

        public static void RenameLight(string? oldName, string? newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;
            if (oldName == newName) return;

            foreach (var source in AllSwitches())
            {
                if (!NamesOf(source).Contains(oldName!)) continue;
                source.SetLightNames(SwitchLightLinks.Renamed(source.LightNames, oldName, newName));
            }
            Refresh();
        }

        public static void ForgetLight(string? lightName)
        {
            if (string.IsNullOrEmpty(lightName)) return;

            foreach (var source in AllSwitches())
            {
                var names = NamesOf(source);
                if (!names.Remove(lightName!)) continue;
                source.SetLightNames(names);
            }
            Refresh();
        }

        private static List<string> NamesOf(ILightSwitch source)
            => new List<string>(source.LightNames);

        private static void EnsureCollected()
        {
            if (!_stale) return;
            _stale = false;
            _states.Clear();
            foreach (var source in AllSwitches())
                _states.Add(new SwitchLinkState(source.PartName, source.IsOn, source.LightNames));
        }
    }
}

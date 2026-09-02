using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public readonly struct SwitchLinkState
    {
        private static readonly string[] Nothing = new string[0];

        public readonly string Name;
        public readonly bool IsOn;
        public readonly IReadOnlyList<string> LightNames;

        public SwitchLinkState(string? name, bool isOn, IReadOnlyList<string>? lightNames)
        {
            Name = name ?? "";
            IsOn = isOn;
            LightNames = lightNames ?? Nothing;
        }

        public bool Controls(string? lightName)
        {
            if (string.IsNullOrEmpty(lightName)) return false;
            for (int i = 0; i < LightNames.Count; i++)
                if (LightNames[i] == lightName) return true;
            return false;
        }
    }
}

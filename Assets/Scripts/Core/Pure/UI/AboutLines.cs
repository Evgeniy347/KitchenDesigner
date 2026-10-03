using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class AboutLines
    {
        public const string ProductName = "Kitchen Designer";
        public const string Copyright = "Copyright (c) 2025 Evgeniy347";

        public static IReadOnlyList<string> For(AboutEnvironment environment) => new[]
        {
            $"{ProductName} {environment.Version}",
            Loc.F("settings.about.build", environment.BuildDate),
            Loc.F("settings.about.platform", environment.Platform),
            Loc.F("settings.about.unity", environment.UnityVersion),
            Loc.F("settings.about.graphics", environment.GraphicsApi),
            Loc.F("settings.about.gpu", environment.Gpu),
            Copyright,
        };
    }
}

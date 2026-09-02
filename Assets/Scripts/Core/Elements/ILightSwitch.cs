using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ILightSwitch
    {
        string PartName { get; }

        bool IsOn { get; set; }

        IReadOnlyList<string> LightNames { get; }

        Vector3 LinkAnchor { get; }

        void SetLightNames(IEnumerable<string>? names);
    }
}

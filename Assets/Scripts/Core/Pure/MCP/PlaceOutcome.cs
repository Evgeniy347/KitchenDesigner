using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceOutcome
    {
        public readonly List<string> Problems = new List<string>();

        public Vector3 MinMm;

        public bool Ok => Problems.Count == 0;
    }
}

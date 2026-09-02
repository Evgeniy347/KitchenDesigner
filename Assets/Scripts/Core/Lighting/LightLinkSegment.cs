using UnityEngine;

namespace KitchenDesigner.Core.Lighting
{
    public readonly struct LightLinkSegment
    {
        public readonly Vector3 From;
        public readonly Vector3 To;
        public readonly bool BelongsToTheSwitchBeingEdited;

        public LightLinkSegment(Vector3 from, Vector3 to, bool belongsToTheSwitchBeingEdited)
        {
            From = from;
            To = to;
            BelongsToTheSwitchBeingEdited = belongsToTheSwitchBeingEdited;
        }
    }
}

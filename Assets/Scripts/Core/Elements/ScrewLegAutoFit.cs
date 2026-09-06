using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScrewLegAutoFit
    {
        public const float OverlapMarginUnits = 0.002f;

        public const float WorldFloorY = 0f;

        private const float SameLevelEpsilonUnits = 1e-4f;

        public static void Seat(ScrewLegElement leg, IReadOnlyList<KitchenElement> scene)
        {
            if (leg == null || scene == null) return;

            var host = HostAbove(leg, scene);
            if (host != null)
            {
                float mountY = ElementAabb.Of(host).minY;
                float floorY = FloorUnder(leg, host, mountY, scene);

                leg.SetHeightAboveFloorMM(HeightMM(mountY - floorY));
                StandOn(leg, floorY);
            }

            ScrewLegHostLink.Apply(leg, scene);
        }

        public static int HeightMM(float heightUnits) =>
            Mathf.RoundToInt(heightUnits / AppConstants.MM_TO_UNITS);

        public static KitchenElement? HostAbove(ScrewLegElement leg,
            IReadOnlyList<KitchenElement> scene)
        {
            var centre = leg.transform.position;
            float legBottom = ElementAabb.Of(leg).minY;
            float reach = legBottom + ScrewLegSpec.MAX_THREAD_LENGTH_MM * AppConstants.MM_TO_UNITS;

            KitchenElement? best = null;
            float bestY = float.MaxValue;
            foreach (var el in scene)
            {
                if (el == null || ReferenceEquals(el, leg) || !CanHost(el)) continue;
                var aabb = ElementAabb.Of(el);
                if (aabb.minY < legBottom + SameLevelEpsilonUnits || aabb.minY > reach) continue;
                if (!aabb.CoversInXZ(centre, OverlapMarginUnits)) continue;
                if (aabb.minY >= bestY) continue;
                best = el;
                bestY = aabb.minY;
            }
            return best;
        }

        public static float FloorUnder(ScrewLegElement leg, KitchenElement host, float mountY,
            IReadOnlyList<KitchenElement> scene)
        {
            var centre = leg.transform.position;
            float bestY = WorldFloorY;
            foreach (var el in scene)
            {
                if (el == null || ReferenceEquals(el, leg) || ReferenceEquals(el, host)) continue;
                var aabb = ElementAabb.Of(el);
                if (aabb.maxY > mountY - SameLevelEpsilonUnits || aabb.maxY <= bestY) continue;
                if (!aabb.CoversInXZ(centre, OverlapMarginUnits)) continue;
                bestY = aabb.maxY;
            }
            return bestY;
        }

        private static bool CanHost(KitchenElement el) =>
            !(el is ScrewLegElement) && el.GetComponent<BasePlate>() == null
            && el.GetComponent<Wall>() == null && !(el is FloorElement);

        private static void StandOn(ScrewLegElement leg, float floorY)
        {
            var p = leg.transform.position;
            leg.transform.position = new Vector3(p.x,
                floorY + AppConstants.HalfHeightUnits(leg.BodyHeightMM), p.z);
        }
    }
}

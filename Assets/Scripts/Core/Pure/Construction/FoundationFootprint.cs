using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct FoundationFootprint
    {
        public readonly Vector3 CentreWorld;
        public readonly Vector3Int SizeMm;

        public FoundationFootprint(Vector3 centreWorld, Vector3Int sizeMm)
        {
            CentreWorld = centreWorld;
            SizeMm = sizeMm;
        }

        public static FoundationFootprint Of(IReadOnlyList<FoundationPolyline> polylines,
            int widthMm, int depthMm)
        {
            bool any = false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

            if (polylines != null)
            {
                foreach (var polyline in polylines)
                {
                    var points = polyline.Points;
                    if (points == null) continue;

                    foreach (var p in points)
                    {
                        if (!any)
                        {
                            minX = maxX = p.x;
                            minZ = maxZ = p.z;
                            any = true;
                            continue;
                        }

                        if (p.x < minX) minX = p.x;
                        if (p.x > maxX) maxX = p.x;
                        if (p.z < minZ) minZ = p.z;
                        if (p.z > maxZ) maxZ = p.z;
                    }
                }
            }

            if (!any) return new FoundationFootprint(Vector3.zero, Vector3Int.zero);

            float toU = AppConstants.MM_TO_UNITS;
            float halfWidthU = Mathf.Max(0f, widthMm) * toU * 0.5f;
            minX -= halfWidthU;
            maxX += halfWidthU;
            minZ -= halfWidthU;
            maxZ += halfWidthU;

            float depthU = Mathf.Max(0f, depthMm) * toU;
            var centre = new Vector3((minX + maxX) * 0.5f, -depthU * 0.5f, (minZ + maxZ) * 0.5f);

            int sizeXmm = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / toU));
            int sizeZmm = Mathf.Max(1, Mathf.RoundToInt((maxZ - minZ) / toU));
            var size = new Vector3Int(sizeXmm, Mathf.Max(1, depthMm), sizeZmm);

            return new FoundationFootprint(centre, size);
        }
    }
}

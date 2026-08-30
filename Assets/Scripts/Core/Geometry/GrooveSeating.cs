using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GrooveSeating
    {
        public const float PanelEngageMarginMm = 6f;

        public const float MinSeatCoverageRatio = 0.5f;

        public static bool IsSeatedInGroove(in ValidationElement panel, in ValidationElement board,
            out Face seatFace)
        {
            seatFace = default;
            if (!panel.IsPanel) return false;

            var seats = board.Geometry.GrooveSeatFaces;
            if (seats == null || seats.Length == 0) return false;

            var verts = panel.Vertices;
            foreach (var seat in seats)
            {
                float minAlong = float.MaxValue;
                foreach (var v in verts)
                    minAlong = Mathf.Min(minAlong, Vector3.Dot(v - seat.center, seat.normal));

                if (minAlong >= -Tolerance.SnapEpsilon) { seatFace = seat; return true; }
            }
            return false;
        }

        public static bool PanelEngagesSeat(Vector3[] panelVertices, in Face seat,
            float depthUnits, float engageMargin, float contactDist, out float minAlong)
        {
            minAlong = float.MaxValue;
            float uMin = float.MaxValue, uMax = float.MinValue;
            float vMin = float.MaxValue, vMax = float.MinValue;

            foreach (var v in panelVertices)
            {
                Vector3 d = v - seat.center;
                float along = Vector3.Dot(d, seat.normal);
                if (along < minAlong) minAlong = along;

                float u = Vector3.Dot(d, seat.rightAxis);
                float w = Vector3.Dot(d, seat.upAxis);
                if (u < uMin) uMin = u; if (u > uMax) uMax = u;
                if (w < vMin) vMin = w; if (w > vMax) vMax = w;
            }

            if (minAlong < -contactDist || minAlong > depthUnits + engageMargin) return false;

            float hu = seat.size.x * 0.5f, hv = seat.size.y * 0.5f;
            float interU = Mathf.Min(uMax, hu) - Mathf.Max(uMin, -hu);
            float interV = Mathf.Min(vMax, hv) - Mathf.Max(vMin, -hv);
            if (interU <= 0f || interV <= 0f) return false;

            float seatArea = seat.size.x * seat.size.y;
            if (seatArea <= 0f) return false;
            return (interU * interV) / seatArea >= MinSeatCoverageRatio;
        }

        public static CoreContact SeatContact(in ValidationElement a, in ValidationElement b,
            int aIdx, int bIdx, in Face seat, bool panelIsA)
        {
            int panelFace = FaceContacts.FaceIndexByNormal(panelIsA ? a.Faces : b.Faces, -seat.normal);
            int boardFace = FaceContacts.FaceIndexByNormal(panelIsA ? b.Faces : a.Faces, seat.normal);
            float area = Mathf.Abs(seat.size.x * seat.size.y);

            return panelIsA
                ? new CoreContact(aIdx, bIdx, panelFace, boardFace, area, true)
                : new CoreContact(aIdx, bIdx, boardFace, panelFace, area, true);
        }
    }
}

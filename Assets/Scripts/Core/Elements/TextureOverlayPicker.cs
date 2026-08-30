using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class TextureOverlayPicker
    {
        public static bool TryPick(KitchenElement? element, Vector3 worldPoint, Vector3 worldNormal,
            out int overlayIndex, out string materialId)
        {
            overlayIndex = -1;
            materialId = MaterialCatalog.DefaultId;
            if (element == null) return false;

            var overlays = element.TextureOverlays;
            if (overlays.Count == 0) return false;

            int faceIndex = NearestFaceIndex(element, worldNormal);
            if (faceIndex < 0) return false;
            if (!TryFacePointMM(element, faceIndex, worldPoint, out Vector2Int uv)) return false;

            var faceMM = TextureOverlayGeometry.FaceSizeMM(element.DimensionsMM, faceIndex);
            for (int i = overlays.Count - 1; i >= 0; i--)
            {
                var spec = overlays[i];
                if (!CoversFace(spec.side, faceIndex)) continue;
                if (!spec.Resolve(faceMM).Contains(uv)) continue;

                overlayIndex = i;
                materialId = spec.MaterialId;
                return true;
            }
            return false;
        }

        public static int NearestFaceIndex(KitchenElement element, Vector3 worldNormal)
        {
            if (element == null || worldNormal.sqrMagnitude < Mathf.Epsilon) return -1;

            var faces = element.GetFaces();
            int best = -1;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < faces.Length; i++)
            {
                float dot = Vector3.Dot(faces[i].normal.normalized, worldNormal.normalized);
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        public static bool TryFacePointMM(KitchenElement element, int faceIndex,
            Vector3 worldPoint, out Vector2Int uv)
        {
            uv = Vector2Int.zero;
            var faces = element.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return false;

            var face = faces[faceIndex];
            var faceMM = TextureOverlayGeometry.FaceSizeMM(element.DimensionsMM, faceIndex);
            var delta = worldPoint - face.center;

            float u = Vector3.Dot(delta, face.rightAxis) / AppConstants.MM_TO_UNITS + faceMM.x * 0.5f;
            float v = Vector3.Dot(delta, face.upAxis) / AppConstants.MM_TO_UNITS + faceMM.y * 0.5f;

            uv = new Vector2Int(Mathf.RoundToInt(u), Mathf.RoundToInt(v));
            return uv.x >= 0 && uv.y >= 0 && uv.x <= faceMM.x && uv.y <= faceMM.y;
        }

        private static bool CoversFace(OverlaySide side, int faceIndex)
        {
            if (side == OverlaySide.All) return true;
            return (int)side == faceIndex;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleLayout
    {
        public static void Place(Face[] faces, IReadOnlyList<ResizeHandle> handles,
            Camera? cam, in HandleMetrics metrics)
        {
            if (faces == null || handles == null) return;

            var box = HandlePlacement.BoxOf(faces);
            int thinAxis = cam != null ? HandlePlacement.ThinAxis(box) : -1;
            PinholeView view = cam != null ? HandleView.Of(cam) : default;

            for (int i = 0; i < handles.Count; i++)
            {
                var h = handles[i];
                if (h == null || h.faceIndex < 0 || h.faceIndex >= faces.Length) continue;
                var f = faces[h.faceIndex];
                Vector3 n = f.normal.sqrMagnitude > Tolerance.EpsilonSqr
                    ? f.normal.normalized
                    : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > Tolerance.UpDotThreshold
                    ? Vector3.forward
                    : Vector3.up;
                Vector3 pos = f.center;

                float scale = cam != null
                    ? HandleScale.ForScreen(view, pos, metrics)
                    : HandleScale.WorldSized;

                bool alreadyOutsideThePlate = h.faceIndex / 2 == thinAxis;
                if (thinAxis >= 0 && !alreadyOutsideThePlate)
                    pos += HandlePlacement.CameraOffset(
                        box, thinAxis, cam!.transform.position, metrics.Gap * scale);

                h.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(n, up));
                h.transform.localScale = Vector3.one * scale;
                h.grabPoint = pos + n * (metrics.GrabCenterZ * scale);
            }
        }
    }
}

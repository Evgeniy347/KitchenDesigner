using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GroundSnapGeometry
    {
        public const string Name = "Ground";

        public const int Id = int.MinValue;

        private const float ExtentUnits = 100f;

        private const float ThicknessUnits = AppConstants.BOARD_THICKNESS_DEFAULT * AppConstants.MM_TO_UNITS;

        public static ElementGeometry Of(ImpliedGround ground)
        {
            var slab = ElementGeometry.Box(Name,
                new Vector3(0f, ground.Y - ThicknessUnits * 0.5f, 0f),
                new Vector3(ExtentUnits, ThicknessUnits, ExtentUnits));
            return new ElementGeometry(Id, Name, slab.Faces, slab.GrooveSeatFaces,
                slab.GrooveWallFaces, slab.Min, slab.Max, false);
        }

        public static bool IsServedUnder(ImpliedGround ground, IReadOnlyList<ElementGeometry> scene,
            int exceptId, Vector3 under)
        {
            if (!ground.Present || scene == null) return false;

            for (int i = 0; i < scene.Count; i++)
            {
                var geometry = scene[i];
                if (geometry.IsEmpty || geometry.Id == exceptId) continue;

                foreach (var face in geometry.Faces)
                    if (face.normal.y >= Tolerance.ParallelDot
                        && Mathf.Abs(face.center.y - ground.Y) <= Tolerance.ContactUnits
                        && Contains(face, under))
                        return true;
            }

            return false;
        }

        public static bool TryOffer(ImpliedGround ground, IReadOnlyList<ElementGeometry> scene,
            int exceptId, Vector3 under, out ElementGeometry geometry)
        {
            if (!ground.Present || IsServedUnder(ground, scene, exceptId, under))
            {
                geometry = default;
                return false;
            }

            geometry = Of(ground);
            return true;
        }

        public static bool AppendTo(List<ElementGeometry> scene, ImpliedGround ground, int exceptId,
            Vector3 under)
        {
            if (!TryOffer(ground, scene, exceptId, under, out var geometry)) return false;

            scene.Add(geometry);
            return true;
        }

        private static bool Contains(in Face face, Vector3 point)
        {
            var offset = point - face.center;
            return Mathf.Abs(Vector3.Dot(offset, face.rightAxis)) <= face.size.x * 0.5f + Tolerance.ContactUnits
                && Mathf.Abs(Vector3.Dot(offset, face.upAxis)) <= face.size.y * 0.5f + Tolerance.ContactUnits;
        }
    }
}

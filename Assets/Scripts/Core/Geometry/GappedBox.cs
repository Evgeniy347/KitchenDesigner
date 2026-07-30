using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Габарит детали С УЧЁТОМ зазоров — «проёмный» бокс, по которому
    /// работают прилипание, валидация и ручки. Физический меш меньше на зазоры.
    ///
    /// Так устроен фасад (зазор от проёма) и так же — ДВП/ХДФ (технологический
    /// зазор в пазу): прилипает номинал, а зазор остаётся внутри детали. Зазоры
    /// есть у любой детали (см. KitchenElement.SupportsGaps), у обычной они
    /// нулевые и бокс совпадает с физическим габаритом. Чистые функции: ядро
    /// геометрии исполняется без Unity.</summary>
    public static class GappedBox
    {
        /// <summary>Границы проёмного бокса в локальных единицах. Зазоры
        /// асимметричны, поэтому бокс может быть НЕ центрирован вокруг transform.</summary>
        public static void CornerUnits(Vector3 physical, BoxGaps gaps,
            out float minX, out float maxX, out float minY, out float maxY,
            out float minZ, out float maxZ)
        {
            float gl = gaps.Left * AppConstants.MM_TO_UNITS;
            float gr = gaps.Right * AppConstants.MM_TO_UNITS;
            float gt = gaps.Top * AppConstants.MM_TO_UNITS;
            float gb = gaps.Bottom * AppConstants.MM_TO_UNITS;
            float gf = gaps.Front * AppConstants.MM_TO_UNITS;
            float gk = gaps.Back * AppConstants.MM_TO_UNITS;
            minX = -physical.x * 0.5f - gl;
            maxX = physical.x * 0.5f + gr;
            minY = -physical.y * 0.5f - gb;
            maxY = physical.y * 0.5f + gt;
            minZ = -physical.z * 0.5f - gk;
            maxZ = physical.z * 0.5f + gf;
        }

        /// <summary>Размер проёмного бокса: каждая ось растёт на сумму зазоров
        /// своих двух сторон, толщина в том числе (Front + Back).</summary>
        public static Vector3 EffectiveScale(Vector3 physical, BoxGaps gaps)
        {
            float gapX = (gaps.Left + gaps.Right) * AppConstants.MM_TO_UNITS;
            float gapY = (gaps.Top + gaps.Bottom) * AppConstants.MM_TO_UNITS;
            float gapZ = (gaps.Front + gaps.Back) * AppConstants.MM_TO_UNITS;
            return physical + new Vector3(gapX, gapY, gapZ);
        }

        public static Vector3[] Vertices(Vector3 physical, BoxGaps gaps, Vector3 pos, Quaternion rot)
        {
            CornerUnits(physical, gaps, out var minX, out var maxX,
                out var minY, out var maxY, out var minZ, out var maxZ);
            var local = new[]
            {
                new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ),
                new Vector3(maxX, minY, maxZ), new Vector3(minX, minY, maxZ),
                new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ),
                new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ),
            };
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * local[i];
            return result;
        }

        public static Face[] Faces(Vector3 physical, BoxGaps gaps,
            Vector3 pos, Quaternion rot)
        {
            CornerUnits(physical, gaps, out var minX, out var maxX,
                out var minY, out var maxY, out var minZ, out var maxZ);

            var axes = new[] { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
            float w = maxX - minX, h = maxY - minY, d = maxZ - minZ;

            // Из-за асимметричных зазоров центр бокса смещён относительно transform.
            var localCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            var centerShift = rot * localCenter;

            var faceDims = new[] { new Vector2(h, d), new Vector2(w, d), new Vector2(w, h) };
            var offsets = new[]
            {
                axes[0] * w * 0.5f, -axes[0] * w * 0.5f,
                axes[1] * h * 0.5f, -axes[1] * h * 0.5f,
                axes[2] * d * 0.5f, -axes[2] * d * 0.5f,
            };
            var normals = new[] { axes[0], -axes[0], axes[1], -axes[1], axes[2], -axes[2] };
            var rightAxis = new[] { axes[1], axes[1], axes[0], axes[0], axes[0], axes[0] };
            var upAxis = new[] { axes[2], axes[2], axes[2], axes[2], axes[1], axes[1] };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
                faces[i] = new Face(
                    pos + centerShift + offsets[i], normals[i], faceDims[i / 2],
                    rightAxis[i], upAxis[i]);
            return faces;
        }
    }
}

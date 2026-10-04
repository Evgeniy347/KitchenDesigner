using UnityEngine;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сенсор растяжки текстуры: у каждого плоского треугольника сравнивает
    /// МИЛЛИМЕТРЫ детали с миллиметрами ТЕКСТУРЫ (UV, умноженное на число плиток и
    /// на физический размер плитки). Если развёртка честная, на любой плоской грани
    /// длина в миллиметрах вдоль любого направления одинакова в детали и в картинке:
    /// оба сингулярных числа якобиана равны единице. Полосы вдоль одной оси — это
    /// σ_min около нуля, картинка в два раза крупнее — σ около 0,5.
    ///
    /// Смотрятся только треугольники, у которых ВСЕ три нормали лежат вдоль осей:
    /// на скруглениях любая проекция неизбежно врёт, и это не то, что видно глазом.</summary>
    public static class UvStretch
    {
        public const float AxisAlignedDot = 0.999f;

        public readonly struct Report
        {
            public readonly int Triangles;
            public readonly float MinSigma;
            public readonly float MaxSigma;

            public Report(int triangles, float minSigma, float maxSigma)
            {
                Triangles = triangles;
                MinSigma = minSigma;
                MaxSigma = maxSigma;
            }
        }

        public static Report Measure(Vector3[] positionsMM, Vector2[] textureMM, Vector3[] normals,
            int[] triangles, float axisAlignedDot = AxisAlignedDot)
        {
            int count = 0;
            float minSigma = float.MaxValue;
            float maxSigma = 0f;

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                if (!AxisAligned(normals[i0], axisAlignedDot) || !AxisAligned(normals[i1], axisAlignedDot)
                    || !AxisAligned(normals[i2], axisAlignedDot)) continue;

                var e1 = positionsMM[i1] - positionsMM[i0];
                var e2 = positionsMM[i2] - positionsMM[i0];
                float length1 = e1.magnitude;
                if (length1 < 1e-3f) continue;

                var b1 = e1 / length1;
                var rest = e2 - Vector3.Dot(e2, b1) * b1;
                float length2 = rest.magnitude;
                if (length2 < 1e-3f) continue;
                var b2 = rest / length2;

                float a11 = length1, a12 = Vector3.Dot(e2, b1), a22 = length2;
                var d1 = textureMM[i1] - textureMM[i0];
                var d2 = textureMM[i2] - textureMM[i0];

                float inverse11 = 1f / a11;
                float inverse12 = -a12 / (a11 * a22);
                float inverse22 = 1f / a22;
                float j11 = d1.x * inverse11;
                float j12 = d1.x * inverse12 + d2.x * inverse22;
                float j21 = d1.y * inverse11;
                float j22 = d1.y * inverse12 + d2.y * inverse22;

                float sum = j11 * j11 + j12 * j12 + j21 * j21 + j22 * j22;
                float det = j11 * j22 - j12 * j21;
                float root = Mathf.Sqrt(Mathf.Max(0f, sum * sum - 4f * det * det));
                float big = Mathf.Sqrt(Mathf.Max(0f, (sum + root) * 0.5f));
                float small = Mathf.Sqrt(Mathf.Max(0f, (sum - root) * 0.5f));

                count++;
                minSigma = Mathf.Min(minSigma, small);
                maxSigma = Mathf.Max(maxSigma, big);
            }

            return new Report(count, count == 0 ? 0f : minSigma, maxSigma);
        }

        private static bool AxisAligned(Vector3 normal, float threshold)
            => Mathf.Max(Mathf.Abs(normal.x), Mathf.Max(Mathf.Abs(normal.y), Mathf.Abs(normal.z)))
               >= threshold;
    }
}

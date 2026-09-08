using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Пары граней в полосе (deadBand, maxDist] — слепой зоне
/// <c>ConstraintValidator</c> (см. <c>SaveValidationTests.Geometry_..._NoSubToleranceJoints</c>
/// для истории появления). Вынесено из <c>SaveValidationTests</c>, чтобы тот же скан
/// использовал и сенсор на живом файле пользователя (<c>SaveValidationSensorTests</c>)
/// без копипаста ~100 строк геометрии.</summary>
public readonly struct SubToleranceJoint
{
    public readonly KitchenElement A;
    public readonly KitchenElement B;

    /// <summary>Знак: + щель, − врезание.</summary>
    public readonly float GapUnits;

    public readonly int FaceIndexOnA;

    public SubToleranceJoint(KitchenElement a, KitchenElement b, float gapUnits, int faceIndexOnA)
    {
        A = a;
        B = b;
        GapUnits = gapUnits;
        FaceIndexOnA = faceIndexOnA;
    }
}

public static class SubToleranceJointScanner
{
    private static readonly string[] FaceLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

    public static string FaceLabel(int index) =>
        index >= 0 && index < FaceLabels.Length ? FaceLabels[index] : "?";

    /// <summary>Все пары встречных граней в полосе (deadBand, maxDist], у которых
    /// перекрытие ≥ <see cref="Tolerance.MinSupportOverlap"/>. Обе величины — в
    /// мировых юнитах Unity, не в мм. <c>LightSourceElement</c> исключён — у него
    /// нет физических граней в смысле стыков.</summary>
    public static List<SubToleranceJoint> Find(IReadOnlyList<KitchenElement> elements,
        float deadBand, float maxDist)
    {
        var parts = elements.Where(e => e != null && e is not LightSourceElement).ToList();
        var faces = new Dictionary<KitchenElement, Face[]>();
        var boxes = new Dictionary<KitchenElement, (Vector3 min, Vector3 max)>();
        foreach (var e in parts)
        {
            faces[e] = e.GetFaces();
            boxes[e] = WorldBoundsUnits(e);
        }

        var joints = new List<SubToleranceJoint>();
        for (int i = 0; i < parts.Count; i++)
        {
            for (int j = i + 1; j < parts.Count; j++)
            {
                var a = parts[i];
                var b = parts[j];
                if (!BoxesWithin(boxes[a], boxes[b], maxDist)) continue;

                if (!WorstJoint(faces[a], faces[b], deadBand, maxDist, out float gapUnits, out int faceIndex))
                    continue;

                joints.Add(new SubToleranceJoint(a, b, gapUnits, faceIndex));
            }
        }
        return joints;
    }

    public static string FormatLine(SubToleranceJoint j, float toMm)
    {
        float gapMm = j.GapUnits * toMm;
        string kind = gapMm > 0 ? "щель  " : "врезание";
        return $"{kind} {gapMm,9:F3} мм   {j.A.PartName,-28} ↔ {j.B.PartName,-28} "
            + $"(грань {FaceLabel(j.FaceIndexOnA)} у {j.A.PartName})";
    }

    private static (Vector3 min, Vector3 max) WorldBoundsUnits(KitchenElement e)
    {
        var verts = e.GetVertices();
        Vector3 min = verts[0], max = verts[0];
        foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        return (min, max);
    }

    private static bool BoxesWithin((Vector3 min, Vector3 max) a, (Vector3 min, Vector3 max) b,
        float margin) =>
        a.min.x <= b.max.x + margin && a.max.x >= b.min.x - margin &&
        a.min.y <= b.max.y + margin && a.max.y >= b.min.y - margin &&
        a.min.z <= b.max.z + margin && a.max.z >= b.min.z - margin;

    /// <summary>Худший (по модулю) стык встречных граней в полосе (deadBand, maxDist].
    /// Знак: + щель, − врезание. Гейты те же, что у ConstraintValidator.MinParallelGap
    /// (параллельность, перекрытие ≥ MinSupportOverlap), но нормали обязаны быть
    /// ВСТРЕЧНЫМИ — только тогда расстояние между плоскостями имеет знак.</summary>
    private static bool WorstJoint(Face[] fa, Face[] fb,
        float deadBand, float maxDist, out float gapUnits, out int faceIndex)
    {
        gapUnits = 0f;
        faceIndex = -1;
        for (int a = 0; a < 6; a++)
        {
            for (int b = 0; b < 6; b++)
            {
                if (Vector3.Dot(fa[a].normal, fb[b].normal) > -Tolerance.ParallelDot) continue;

                float signed = Vector3.Dot(fb[b].center - fa[a].center, fa[a].normal);
                float abs = Mathf.Abs(signed);
                if (abs <= deadBand || abs > maxDist) continue;

                if (!FacesOverlap(fa[a], fb[b], out float ratio)) continue;
                if (ratio < Tolerance.MinSupportOverlap) continue;

                if (abs > Mathf.Abs(gapUnits)) { gapUnits = signed; faceIndex = a; }
            }
        }
        return faceIndex >= 0;
    }

    /// <summary>Перекрытие граней в плоскости — копия ConstraintValidator.FacesOverlap
    /// (там private). Полуосевое отношение, а не отношение площадей: узкие
    /// перпендикулярные грани иначе отсекались бы.</summary>
    private static bool FacesOverlap(Face a, Face b, out float overlapRatio)
    {
        Vector3 u = a.rightAxis;
        Vector3 v = a.upAxis;
        Rect aRect = FaceRect(a, u, v);
        Rect bRect = FaceRect(b, u, v);

        float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
        float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
        float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
        float interTop = Mathf.Min(aRect.yMax, bRect.yMax);
        if (interLeft >= interRight || interBottom >= interTop)
        {
            overlapRatio = 0f;
            return false;
        }

        float ratioU = Mathf.Min(aRect.width, bRect.width) > 0
            ? (interRight - interLeft) / Mathf.Min(aRect.width, bRect.width) : 0f;
        float ratioV = Mathf.Min(aRect.height, bRect.height) > 0
            ? (interTop - interBottom) / Mathf.Min(aRect.height, bRect.height) : 0f;
        overlapRatio = ratioU * ratioV;
        return true;
    }

    private static Rect FaceRect(Face face, Vector3 u, Vector3 v)
    {
        var center = new Vector2(Vector3.Dot(face.center, u), Vector3.Dot(face.center, v));
        float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                    + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
        float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                    + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;
        return new Rect(center.x - halfU, center.y - halfV, halfU * 2f, halfV * 2f);
    }
}

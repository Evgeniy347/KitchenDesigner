using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>ПРОТОТИП быстрого пути контакта граней, живущий в тестовой сборке.
///
/// Общий путь (<c>FaceContactScan</c>) перебирает 6×6 граней и на каждой паре
/// считает скалярные произведения и прямоугольник перекрытия. Для деталей,
/// повёрнутых кратно 90°, ответ достаётся интервальной арифметикой за O(1):
/// параллельны только грани, смотрящие вдоль ОДНОЙ мировой оси, значит из 36 пар
/// содержательны 12 — по четыре на ось, — а перекрытие у всех четырёх одно и то же,
/// это сечение двух AABB по двум оставшимся осям.
///
/// Соответствие общему пути построчное, и это важнее краткости:
/// <c>planeGap</c> = расстояние между плоскостями граней, порог тот же
/// <c>contactDist</c>; площадь = произведение перекрытий; признак
/// <c>IsFaceToFace</c> = произведение долей от МЕНЬШЕЙ стороны, сравнённое с
/// <c>Tolerance.MinSupportOverlap</c>. Индексы граней берутся настоящие, из самого
/// массива <c>Faces</c>, а не выводятся из порядка построения — иначе сравнение
/// множеств проверяло бы соглашение о нумерации, а не геометрию.
///
/// Признак осевого поворота — тот же, что у <c>MmGrid.IsAxisAligned</c>: все три
/// локальные оси лежат на мировых с точностью 0,9999. Правило повторено здесь, а не
/// вызвано, потому что <c>MmGrid</c> живёт в Unity-сборке, которую быстрый путь не
/// компилирует; порог вынесен в константу с этой ссылкой, чтобы расхождение было
/// видно глазом.</summary>
internal static class AxisAlignedBoxContacts
{
    public const float AxisDotThreshold = 0.9999f;

    public static bool IsAxisAligned(Quaternion rotation) =>
        IsWorldAxis(rotation * Vector3.right)
        && IsWorldAxis(rotation * Vector3.up)
        && IsWorldAxis(rotation * Vector3.forward);

    public static bool IsWorldAxis(Vector3 axis) =>
        Mathf.Max(Mathf.Abs(axis.x), Mathf.Max(Mathf.Abs(axis.y), Mathf.Abs(axis.z)))
        >= AxisDotThreshold;

    public static void AppendContacts(int aIdx, int bIdx,
        in AxisAlignedBox a, in AxisAlignedBox b, float contactDist, List<CoreContact> into)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            int u = axis == 2 ? 0 : axis + 1;
            int v = axis == 0 ? 2 : axis - 1;

            float overlapU = Mathf.Min(a.Max[u], b.Max[u]) - Mathf.Max(a.Min[u], b.Min[u]);
            float overlapV = Mathf.Min(a.Max[v], b.Max[v]) - Mathf.Max(a.Min[v], b.Min[v]);
            if (overlapU <= 0f || overlapV <= 0f) continue;

            float ratio =
                FaceRects.RatioOfSmallerSide(overlapU, Mathf.Min(a.Side(u), b.Side(u))) *
                FaceRects.RatioOfSmallerSide(overlapV, Mathf.Min(a.Side(v), b.Side(v)));
            bool faceToFace = ratio >= Tolerance.MinSupportOverlap;
            float area = overlapU * overlapV;

            for (int sideA = 0; sideA < 2; sideA++)
                for (int sideB = 0; sideB < 2; sideB++)
                {
                    float gap = Mathf.Abs(b.Plane(axis, sideB) - a.Plane(axis, sideA));
                    if (gap > contactDist) continue;
                    into.Add(new CoreContact(aIdx, bIdx,
                        a.FaceIndex(axis, sideA), b.FaceIndex(axis, sideB), area, faceToFace));
                }
        }
    }
}

/// <summary>Деталь, повёрнутая кратно 90°, в виде, который нужен интервальному
/// пути: коробка и таблица «мировая ось + знак → номер грани и координата её
/// плоскости». Таблица строится ОДИН раз на деталь (O(n) на сцену), а не на пару —
/// иначе замер пары мерил бы подготовку.</summary>
internal readonly struct AxisAlignedBox
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    private readonly int[] _faceIndex;
    private readonly float[] _plane;

    private AxisAlignedBox(Vector3 min, Vector3 max, int[] faceIndex, float[] plane)
    {
        Min = min;
        Max = max;
        _faceIndex = faceIndex;
        _plane = plane;
    }

    public int FaceIndex(int axis, int side) => _faceIndex[axis * 2 + side];

    public float Plane(int axis, int side) => _plane[axis * 2 + side];

    public float Side(int axis) => Max[axis] - Min[axis];

    public int AxisOfFace(int faceIndex)
    {
        for (int slot = 0; slot < _faceIndex.Length; slot++)
            if (_faceIndex[slot] == faceIndex) return slot / 2;
        return -1;
    }

    /// <summary>Меньшее из двух перекрытий сечения — тот размер, по которому пара
    /// стоит на границе «перекрытие есть / перекрытия нет». Им расхождение двух
    /// путей отделяется от настоящего: площадь для этого не годится, потому что
    /// перекрытие в полмикрона, умноженное на высоту стены 2,5 м, даёт «приличные»
    /// 1,3 мм².</summary>
    public static float MinOverlap(in AxisAlignedBox a, in AxisAlignedBox b, int axis)
    {
        int u = axis == 2 ? 0 : axis + 1;
        int v = axis == 0 ? 2 : axis - 1;
        return Mathf.Min(
            Mathf.Min(a.Max[u], b.Max[u]) - Mathf.Max(a.Min[u], b.Min[u]),
            Mathf.Min(a.Max[v], b.Max[v]) - Mathf.Max(a.Min[v], b.Min[v]));
    }

    public static bool TryOf(in ValidationElement element, out AxisAlignedBox box)
    {
        box = default;
        var faces = element.Faces;
        if (faces == null || faces.Length != Face.BoxFaceCount) return false;

        var faceIndex = new int[Face.BoxFaceCount];
        var plane = new float[Face.BoxFaceCount];
        var filled = new bool[Face.BoxFaceCount];

        for (int i = 0; i < faces.Length; i++)
        {
            var normal = faces[i].normal;
            if (!AxisAlignedBoxContacts.IsWorldAxis(normal)) return false;

            int axis = DominantAxis(normal);
            int side = normal[axis] > 0f ? 0 : 1;
            int slot = axis * 2 + side;
            if (filled[slot]) return false;
            filled[slot] = true;
            faceIndex[slot] = i;
            plane[slot] = faces[i].center[axis];
        }

        for (int slot = 0; slot < filled.Length; slot++)
            if (!filled[slot]) return false;

        box = new AxisAlignedBox(element.Geometry.Min, element.Geometry.Max, faceIndex, plane);
        return true;
    }

    private static int DominantAxis(Vector3 normal)
    {
        float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }
}

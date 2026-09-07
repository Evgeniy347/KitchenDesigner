using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Каждый треугольник трубы обязан быть обойдён в ту же сторону,
/// куда смотрит нормаль его вершин. У <c>TubeMesh</c> это было не так —
/// ВЕЗДЕ: и боковая обшивка, и оба торцевых диска обходились ровно наоборот.
/// Нормали при этом считались правильно, наружу, поэтому освещение выглядело
/// осмысленным, а отсечение задних граней выбрасывало ближнюю к камере
/// половину и рисовало ДАЛЬНЮЮ, изнутри. Смеситель и стойка — единственные
/// два типа, которые строятся через TubeMesh, — выглядели вывернутыми
/// наизнанку, и никакой правкой раскладки это было не вылечить.
///
/// Соглашение о знаке в проекте одно и проверяемое: у переднего треугольника
/// <c>Cross(b − a, c − a)</c> совпадает по направлению с нормалью. Оно взято
/// не из головы — так устроен эталонный quad самого Unity (вершины
/// (0,0,0), (1,0,0), (0,1,0) с треугольником 0,2,1 смотрят в −Z, и
/// Cross даёт ровно −Z), и по нему же живёт
/// <c>CushionSurfaceTests.EveryTriangle_FacesOutwards</c>.
///
/// Тестов два, и второй здесь не дубль. Первый сверяет обход с ХРАНИМОЙ
/// нормалью, и его одного мало: перевернуть заодно и нормали — значит
/// оставить его зелёным на такой же вывернутой трубе. Второй поэтому не
/// смотрит на нормали вовсе, а выводит «наружу» из ГЕОМЕТРИИ: у боковой
/// грани цилиндра нормаль обхода обязана иметь положительную проекцию на
/// радиус, у ближнего торца — смотреть против оси, у дальнего — по оси.
/// Развернуть их обоих одновременно нельзя, не сломав саму трубу.</summary>
public class TubeMeshWindingTests
{
    private const float Tol = 1e-4f;

    private static Mesh Straight()
    {
        var target = new MeshAccumulator();
        TubeMesh.AppendSegment(target, new Vector3(-0.075f, 0f, 0f),
            new Vector3(0.075f, 0f, 0f), 0.035f, 0.035f, 20);
        return target.Build();
    }

    private static Mesh Tapered()
    {
        var target = new MeshAccumulator();
        TubeMesh.AppendSegment(target, new Vector3(0f, 0f, 0f),
            new Vector3(0f, -0.07f, 0.11f), 0.018f, 0.014f, 16);
        return target.Build();
    }

    private static Mesh Bent()
    {
        var target = new MeshAccumulator();
        TubeMesh.Append(target,
            new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0.4f, 0f),
                new Vector3(0f, 0.55f, 0.12f), new Vector3(0f, 0.58f, 0.3f),
            },
            new[] { 0.016f, 0.016f, 0.016f, 0.016f }, 16);
        return target.Build();
    }

    /// <summary>Нормаль обхода треугольника, или ноль, если треугольник
    /// вырожден и о направлении ничего не говорит.</summary>
    private static Vector3 WindingNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = Vector3.Cross((b - a).normalized, (c - a).normalized);
        return cross.sqrMagnitude < Tolerance.EpsilonSqr ? Vector3.zero : cross.normalized;
    }

    private static void AssertWindingAgreesWithNormals(Mesh mesh, string what)
    {
        var positions = mesh.vertices;
        var normals = mesh.normals;
        var triangles = mesh.triangles;
        int judged = 0;

        Assert.AreEqual(0, triangles.Length % 3, what + ": треугольники идут тройками");

        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = positions[triangles[i]];
            var winding = WindingNormal(a, positions[triangles[i + 1]],
                positions[triangles[i + 2]]);
            if (winding == Vector3.zero) continue;

            judged++;
            Assert.Greater(Vector3.Dot(winding, normals[triangles[i]]), 0f,
                what + ": обход треугольника " + i / 3 + " смотрит против нормали своей "
                + "вершины — при отсечении задних граней исчезнет ближняя к камере "
                + "стенка трубы, и деталь будет выглядеть вывернутой наизнанку");
        }

        Assert.Greater(judged, triangles.Length / 6,
            what + ": вырожденных треугольников больше половины — тест позеленел бы, "
            + "ничего не проверив");
    }

    [Test]
    public void TubeMesh_EveryTriangle_WindsTheSameWayItsVertexNormalPoints()
    {
        var straight = Straight();
        var tapered = Tapered();
        var bent = Bent();

        AssertWindingAgreesWithNormals(straight, "прямая труба");
        AssertWindingAgreesWithNormals(tapered, "конус");
        AssertWindingAgreesWithNormals(bent, "гнутая труба");

        Object.DestroyImmediate(straight);
        Object.DestroyImmediate(tapered);
        Object.DestroyImmediate(bent);
    }

    /// <summary>То же требование, выведенное из геометрии, а не из хранимых
    /// нормалей: перевернуть меш вместе с нормалями этот тест не пропустит.
    /// Цилиндр здесь ровный и вдоль X, поэтому «наружу» для боковой грани —
    /// это радиус в плоскости YZ, а для торцов — сама ось.</summary>
    [Test]
    public void TubeMesh_TheSideFacesTurnOutwards_AndTheCapsFaceAlongTheAxis()
    {
        var mesh = Straight();
        var positions = mesh.vertices;
        var triangles = mesh.triangles;
        int sides = 0, nearCap = 0, farCap = 0;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = positions[triangles[i]];
            var b = positions[triangles[i + 1]];
            var c = positions[triangles[i + 2]];
            var winding = WindingNormal(a, b, c);
            if (winding == Vector3.zero) continue;

            var centroid = (a + b + c) / 3f;
            if (Mathf.Abs(a.x - b.x) > Tol || Mathf.Abs(a.x - c.x) > Tol)
            {
                sides++;
                var radial = new Vector3(0f, centroid.y, centroid.z).normalized;
                Assert.Greater(Vector3.Dot(winding, radial), 0f,
                    "боковая грань цилиндра обязана быть обойдена НАРУЖУ от оси, "
                    + "треугольник " + i / 3);
                continue;
            }

            if (centroid.x < 0f)
            {
                nearCap++;
                Assert.Less(winding.x, 0f,
                    "ближний торец смотрит против оси, треугольник " + i / 3);
            }
            else
            {
                farCap++;
                Assert.Greater(winding.x, 0f,
                    "дальний торец смотрит по оси, треугольник " + i / 3);
            }
        }

        Assert.AreEqual(40, sides, "обшивка прямого отрезка из 20 сегментов — это 40 "
            + "треугольников: разойдись число, тест мерил бы не ту трубу");
        Assert.AreEqual(20, nearCap, "и ровно 20 на ближнем торце");
        Assert.AreEqual(20, farCap, "и 20 на дальнем");

        Object.DestroyImmediate(mesh);
    }
}

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Соответствие «сторона с зазором → индекс грани».
///
/// Таблица маленькая, но перепутанные в ней Front/Back или Left/Right стоят
/// дорого: подсветка в окне свойств покажет не ту сторону, а сам зазор сдвинет
/// габарит в противоположную. Порядок граней — контракт (index/2 = ось,
/// чётный = положительное направление), поэтому проверяем НЕ таблицу против
/// самой себя, а против настоящих граней детали.</summary>
public class GapSideTests
{
    private GameObject? _go;

    [TearDown]
    public void Teardown()
    {
        // Накладки статические и переживают тест: оставленная подсветка
        // утащила бы за собой уничтоженную деталь в следующий класс.
        SideHighlighter.Hide();
        if (_go != null) Object.DestroyImmediate(_go);
        PartRegistry.Clear();
    }

    private KitchenElement MakeBoard()
    {
        _go = new GameObject("B");
        var el = _go.AddComponent<KitchenElement>();
        el.PartName = "B";
        el.DimensionsMM = new Vector3Int(800, 400, 18);
        el.transform.position = Vector3.zero;
        el.transform.rotation = Quaternion.identity;
        el.ApplyDimensions();
        return el;
    }

    [Test]
    public void FaceIndex_MatchesTheRealFaceNormal()
    {
        var el = MakeBoard();
        var faces = el.GetFaces();

        var expected = new (GapSide side, Vector3 normal)[]
        {
            (GapSide.Right, Vector3.right),
            (GapSide.Left, Vector3.left),
            (GapSide.Top, Vector3.up),
            (GapSide.Bottom, Vector3.down),
            (GapSide.Front, Vector3.forward),
            (GapSide.Back, Vector3.back),
        };

        foreach (var (side, normal) in expected)
        {
            var face = faces[GapSides.FaceIndex(side)];
            Assert.AreEqual(1f, Vector3.Dot(face.normal.normalized, normal), 1e-4f,
                $"сторона {side} смотрит не туда");
        }
    }

    [Test]
    public void All_CoversEverySideOnce()
    {
        Assert.AreEqual(6, GapSides.All.Length);
        var indices = new System.Collections.Generic.HashSet<int>();
        foreach (var side in GapSides.All) indices.Add(GapSides.FaceIndex(side));
        Assert.AreEqual(6, indices.Count, "две стороны показывают на одну грань");
    }

    /// <summary>BoxGaps.Of обязан отдавать зазор ИМЕННО своей стороны: на этом
    /// строится и UI, и копирование зазоров при дублировании.</summary>
    [Test]
    public void BoxGaps_Of_ReturnsItsOwnSide()
    {
        var gaps = new BoxGaps(left: 1, right: 2, top: 3, bottom: 4, front: 5, back: 6);

        Assert.AreEqual(1, gaps.Of(GapSide.Left));
        Assert.AreEqual(2, gaps.Of(GapSide.Right));
        Assert.AreEqual(3, gaps.Of(GapSide.Top));
        Assert.AreEqual(4, gaps.Of(GapSide.Bottom));
        Assert.AreEqual(5, gaps.Of(GapSide.Front));
        Assert.AreEqual(6, gaps.Of(GapSide.Back));
        Assert.AreEqual(6, gaps.NonZeroCount);
    }

    [Test]
    public void BoxGaps_NonZeroCount_IgnoresZeroSides()
    {
        Assert.AreEqual(0, BoxGaps.None.NonZeroCount);
        Assert.AreEqual(2, new BoxGaps(2, 0, 0, 2, 0, 0).NonZeroCount);
    }

    // ── Подсветка стороны с зазором ───────────────────────────────────
    // Тот же класс, что подсвечивает кромку: торец целиком плюс каёмка на
    // каждой из четырёх соседних граней.

    [Test]
    public void ShowGapSide_LightsUpFiveQuads_OnTheRightFace()
    {
        var el = MakeBoard();

        SideHighlighter.ShowGapSide(el, GapSide.Front);

        Assert.AreEqual(5, SideHighlighter.QuadCount, "грань + 4 каёмки");
        Assert.IsTrue(SideHighlighter.IsGapSideShown(el, GapSide.Front));
        Assert.IsFalse(SideHighlighter.IsGapSideShown(el, GapSide.Back),
            "противоположная сторона подсвеченной не считается");

        SideHighlighter.Hide();
        Assert.AreEqual(0, SideHighlighter.QuadCount);
    }

    /// <summary>Накладка ложится на СВОЮ грань: сторона Back смотрит в −Z, и
    /// перепутанные местами front/back тихо подсветили бы противоположный торец.</summary>
    [Test]
    public void ShowGapSide_PutsTheFaceQuadOnThatFace()
    {
        var el = MakeBoard();
        var expected = el.GetFaces()[GapSides.FaceIndex(GapSide.Back)];

        SideHighlighter.ShowGapSide(el, GapSide.Back);

        var faceQuad = SideHighlighter.QuadObjects[0].transform.position;
        Assert.AreEqual(expected.center.z, faceQuad.z, 0.001f,
            "накладка не на той грани");
        SideHighlighter.Hide();
    }
}

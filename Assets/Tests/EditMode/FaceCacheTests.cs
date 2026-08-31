using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class FaceCacheTests : SnapTestBase
{
    [TearDown]
    public void ClearCacheAfterEachTest() => FaceCache.Clear();

    [Test]
    public void FaceCache_IsOffUntilATestTurnsItOn()
    {
        Assert.IsFalse(FaceCache.Enabled,
            "в рантайме кэш выключен: включённый по умолчанию, он раздавал бы "
            + "устаревшие грани после каждого переноса детали");
    }

    [Test]
    public void GetFaces_WithoutCache_FollowsTheElementAfterItMoves()
    {
        var board = MakeStd("Деталь", Vector3.zero);
        float before = FaceCache.GetFaces(board)[0].center.x;

        board.transform.position = new Vector3(1f, 0f, 0f);
        float after = FaceCache.GetFaces(board)[0].center.x;

        Assert.AreEqual(before + 1f, after, Tol,
            "без кэша грани считаются заново — иначе снэп ловил бы деталь там, "
            + "где её уже нет");
    }

    [Test]
    public void GetFaces_WithCache_ReturnsTheFrozenFaces_AndClearBringsBackTheLiveOnes()
    {
        var board = MakeStd("Деталь", Vector3.zero);
        float frozen = board.GetFaces()[0].center.x;
        FaceCache.Set(new Dictionary<KitchenElement, Face[]> { [board] = board.GetFaces() });
        Assert.IsTrue(FaceCache.Enabled);

        board.transform.position = new Vector3(1f, 0f, 0f);
        Assert.AreEqual(frozen, FaceCache.GetFaces(board)[0].center.x, Tol,
            "ради этого кэш и существует: свип по 1 мм не пересчитывает грани "
            + "неподвижных соседей тысячи раз");

        FaceCache.Clear();
        Assert.IsFalse(FaceCache.Enabled);
        Assert.AreEqual(frozen + 1f, FaceCache.GetFaces(board)[0].center.x, Tol,
            "Clear возвращает живые грани, а не последний снимок");
    }

    [Test]
    public void GetFaces_WithCache_FallsBackToTheElementForStrangers()
    {
        var cached = MakeStd("Своя", Vector3.zero);
        var stranger = MakeStd("Чужая", new Vector3(2f, 0f, 0f));
        FaceCache.Set(new Dictionary<KitchenElement, Face[]> { [cached] = cached.GetFaces() });

        Assert.AreEqual(stranger.GetFaces()[0].center.x,
            FaceCache.GetFaces(stranger)[0].center.x, Tol,
            "кэш прозрачный: деталь, которой в нём нет, считает грани сама");
    }
}

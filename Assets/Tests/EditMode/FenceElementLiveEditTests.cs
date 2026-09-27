using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>FenceElement.PostSectionMm/PostStepMm/PitDepthMm are [Undoable] but their setters
/// used to only clamp and store, unlike KitchenElement's Gap* properties, which call
/// ApplyDimensions(). RebuildPosts() (called from ApplyDimensions) reads the backing fields
/// directly, so editing these properties after the element already exists left the post
/// GameObjects stale — the panel showed the new number, the scene kept the old posts.</summary>
public class FenceElementLiveEditTests
{
    private const float U = AppConstants.MM_TO_UNITS;
    private readonly List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private FenceElement Spawn(int lengthMm, int heightMm)
    {
        var go = ElementFactory.CreateFence(lengthMm, heightMm, "F", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<FenceElement>();
    }

    private static int PostCount(FenceElement fence) => fence.transform.childCount;

    private static Transform Post(FenceElement fence, int index) => fence.transform.GetChild(index);

    [Test]
    public void PostStepMm_Changed_UpdatesPostCount()
    {
        var fence = Spawn(6000, 2000);
        int expectedBefore = FenceQuantities.PostsPerRun(6000, FenceDefaults.PostStepMm);
        Assert.AreEqual(expectedBefore, PostCount(fence),
            "посылка: при шаге по умолчанию столбов должно быть именно столько");

        fence.PostStepMm = 3000;
        int expectedAfter = FenceQuantities.PostsPerRun(6000, 3000);

        Assert.AreNotEqual(expectedBefore, expectedAfter,
            "выбранный шаг обязан менять число столбов, иначе тест ничего не проверяет");
        Assert.AreEqual(expectedAfter, PostCount(fence),
            "смена PostStepMm обязана перестроить столбы через ApplyDimensions()");
    }

    [Test]
    public void PostSectionMm_Changed_UpdatesPostScale()
    {
        var fence = Spawn(6000, 2000);
        float scaleBefore = Post(fence, 0).localScale.x;
        Assert.AreEqual(FenceDefaults.PostSectionMm * U, scaleBefore, 1e-5f);

        fence.PostSectionMm = 100;

        float scaleAfter = Post(fence, 0).localScale.x;
        Assert.AreNotEqual(scaleBefore, scaleAfter,
            "новое сечение обязано изменить масштаб столба, иначе тест ничего не проверяет");
        Assert.AreEqual(100f * U, scaleAfter, 1e-5f,
            "смена PostSectionMm обязана перестроить столбы через ApplyDimensions()");
        Assert.AreEqual(100f * U, Post(fence, 0).localScale.z, 1e-5f);
    }

    [Test]
    public void PitDepthMm_Changed_UpdatesPostHeightAndPosition()
    {
        var fence = Spawn(6000, 2000);
        var post = Post(fence, 0);
        float heightU = 2000 * U;

        float pitBeforeU = FenceDefaults.PitDepthMm * U;
        float expectedYBefore = (heightU - pitBeforeU) * 0.5f;
        Assert.AreEqual(heightU + pitBeforeU, post.localScale.y, 1e-4f);
        Assert.AreEqual(expectedYBefore, post.localPosition.y, 1e-4f);

        fence.PitDepthMm = 2000;

        float pitAfterU = 2000 * U;
        float expectedYAfter = (heightU - pitAfterU) * 0.5f;
        Assert.AreNotEqual(expectedYBefore, expectedYAfter,
            "выбранная глубина ямы обязана сдвинуть столб, иначе тест ничего не проверяет");
        Assert.AreEqual(heightU + pitAfterU, post.localScale.y, 1e-4f,
            "смена PitDepthMm обязана перестроить столбы через ApplyDimensions() (высота)");
        Assert.AreEqual(expectedYAfter, post.localPosition.y, 1e-4f,
            "смена PitDepthMm обязана перестроить столбы через ApplyDimensions() (положение)");
    }

    /// <summary>SheetMark не влияет ни на RebuildPosts, ни на профиль листа в ApplyDimensions —
    /// он читается только в GetSpecItems (ведомость материалов). Перестройка геометрии ему не
    /// нужна, поэтому его сеттер намеренно не тронут этим же исправлением.</summary>
    [Test]
    public void SheetMark_Changed_DoesNotNeedGeometryRebuild()
    {
        var fence = Spawn(6000, 2000);
        int countBefore = PostCount(fence);
        float scaleBefore = Post(fence, 0).localScale.x;

        fence.SheetMark = FenceSheetMark.C20;

        Assert.AreEqual(countBefore, PostCount(fence));
        Assert.AreEqual(scaleBefore, Post(fence, 0).localScale.x, 1e-5f);
    }

    /// <summary>test-results/review-construction.md #11: GetSpecItems всегда заказывал лист
    /// по рабочей ширине С8 (1150 мм), какая бы марка ни стояла — смена марки меняла только
    /// подпись материала в ведомости, а число листов оставалось прежним.</summary>
    [Test]
    public void GetSpecItems_HC35_OrdersByItsOwnNarrowerWidth_NotTheDefaultС8Width()
    {
        var fence = Spawn(10000, 2000);
        fence.SheetMark = FenceSheetMark.HC35;

        var sheetItem = fence.GetSpecItems(new List<KitchenElement>())
            .Single(i => i.name == FenceSpecItems.SheetName);

        Assert.AreEqual("НС35", sheetItem.material);
        Assert.AreEqual(10, sheetItem.qty, 0.01f,
            "ceil(10000/1000) = 10 листов НС35 (рабочая ширина 1000 мм), а не "
            + "ceil(10000/1150) = 9, как было бы при рабочей ширине С8");
    }
}

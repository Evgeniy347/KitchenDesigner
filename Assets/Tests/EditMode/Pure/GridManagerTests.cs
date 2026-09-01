using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class GridManagerTests
{
    private const int StepMm = 16;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = StepMm;
        KitchenSettings.Instance.GridEnabled = true;
    }

    [Test]
    public void SnapToGrid_17RoundsTo16()
    {
        Vector3 result = GridManager.SnapToGrid(new Vector3(0.017f, 0, 0));
        Assert.AreEqual(0.016f, result.x, 0.0001f);
    }

    [Test]
    public void SnapToGrid_9RoundsTo16()
    {
        Vector3 result = GridManager.SnapToGrid(new Vector3(0.009f, 0, 0));
        Assert.AreEqual(0.016f, result.x, 0.0001f);
    }

    [Test]
    public void SnapToGrid_0Stays0()
    {
        Vector3 result = GridManager.SnapToGrid(Vector3.zero);
        Assert.AreEqual(Vector3.zero, result);
    }

    [Test]
    public void SnapToGrid_DisabledReturnsInput()
    {
        KitchenSettings.Instance.GridEnabled = false;
        Vector3 input = new Vector3(0.025f, 0.037f, 0.012f);
        Vector3 result = GridManager.SnapToGrid(input);
        Assert.AreEqual(input, result);
    }

    [Test]
    public void SnapToGrid_RoundsHeight_WhichIsWhyHorizontalDraggingNeedsItsOwnCall()
    {
        const float heightNotOnStep = 0.36f;
        Assume.That(Mathf.Round(heightNotOnStep * 1000f) % StepMm, Is.Not.EqualTo(0),
            "высота обязана НЕ быть кратна шагу сетки, иначе округление не сдвинет её "
            + "ни в какую сторону и тест зелен на сломанном коде");

        Vector3 result = GridManager.SnapToGrid(new Vector3(0f, heightNotOnStep, 0f));

        Assert.That(result.y, Is.Not.EqualTo(heightNotOnStep).Within(0.0005f),
            "полный SnapToGrid двигает и Y — именно поэтому у горизонтального "
            + "перетаскивания отдельный метод");
    }

    [Test]
    public void SnapToGridXZ_LeavesHeightAlone_SoFloorContactSurvivesEveryFrameOfADrag()
    {
        const float heightNotOnStep = 0.36f;
        Assume.That(Mathf.Round(heightNotOnStep * 1000f) % StepMm, Is.Not.EqualTo(0),
            "высота обязана НЕ быть кратна шагу сетки, иначе тест не отличит "
            + "«не трогаем Y» от «округлили, и ничего не изменилось»");

        Vector3 result = GridManager.SnapToGridXZ(new Vector3(0.017f, heightNotOnStep, 0.009f));

        Assert.AreEqual(heightNotOnStep, result.y, 0.0001f,
            "высоту детали держат снэп и стартовая позиция; повторное округление Y "
            + "каждый кадр рвало контакт с полом — снэпу приходилось чинить вертикаль "
            + "вместо прилипания к соседу, и деталь краснела");
    }

    [Test]
    public void SnapToGridXZ_StillRoundsBothHorizontalAxes()
    {
        Vector3 result = GridManager.SnapToGridXZ(new Vector3(0.017f, 0.36f, 0.009f));
        Assert.AreEqual(0.016f, result.x, 0.0001f);
        Assert.AreEqual(0.016f, result.z, 0.0001f);
    }

    [Test]
    public void SnapToGridXZ_ClampsHeightToWorldBounds_EvenThoughItDoesNotRoundIt()
    {
        Vector3 result = GridManager.SnapToGridXZ(new Vector3(0f, WorldBounds.LimitMeters * 10f, 0f));
        Assert.AreEqual(WorldBounds.LimitMeters, result.y, 0.0001f,
            "не округлять Y — не значит выпускать деталь за пределы мира");
    }
}

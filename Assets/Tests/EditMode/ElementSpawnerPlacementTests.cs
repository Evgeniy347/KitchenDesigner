using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Две ПРАВИЛА постановки нового элемента, которые в UIManager жили как две
/// почти одинаковые копии одного и того же кода и отличались только порядком
/// двух строк. Порядок — не мелочь: SnapToGrid округляет ВСЕ три координаты,
/// поэтому «сначала высота, потом сетка» кладёт центр детали на шаг сетки, а
/// «сначала сетка, потом высота» оставляет высоту точной.
///
/// Тесты фиксируют обе ветки поимённо, чтобы схлопывание семейства Spawn* в
/// параметризованный <see cref="ElementSpawner"/> не свело их к одной.
/// </summary>
public class ElementSpawnerPlacementTests
{
    private const float GroundX = 0.137f;
    private const float GroundZ = -0.421f;
    private const int GridStepMm = 50;

    private bool _gridEnabledBefore;
    private int _gridStepBefore;
    private ElementSpawner _spawner = null!;

    [SetUp]
    public void SetUp()
    {
        _gridEnabledBefore = KitchenSettings.Instance.GridEnabled;
        _gridStepBefore = KitchenSettings.Instance.GridStep;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.GridStep = GridStepMm;

        _spawner = new ElementSpawner(() => new Vector3(GroundX, 0f, GroundZ), () => null);
    }

    [TearDown]
    public void TearDown()
    {
        if (KitchenSettings.Instance == null) return;
        KitchenSettings.Instance.GridEnabled = _gridEnabledBefore;
        KitchenSettings.Instance.GridStep = _gridStepBefore;
    }

    [Test]
    public void CenteredOnGroundPoint_RoundsTheHeightToTheGridStep()
    {
        Vector3 pos = _spawner.CenteredOnGroundPoint(595);

        Assert.AreEqual(0.30f, pos.y, Tolerance.EpsilonUnits,
            "деталь ставится центром на половину высоты и вместе с ней округляется к сетке: "
            + "595/2 = 297,5 мм при шаге 50 мм даёт 300 мм. Если тут появилось 0,2975 — "
            + "порядок «высота, затем SnapToGrid» потерян");
    }

    [Test]
    public void GroundPointAtHeightUnaffectedByGrid_KeepsTheHeightExact()
    {
        float ovenCenterY = OvenElement.ModelDimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS;
        Assume.That(ovenCenterY % (GridStepMm * 0.001f), Is.Not.EqualTo(0f).Within(Tolerance.EpsilonUnits),
            "проверять «сетка не трогает высоту» на высоте, кратной шагу сетки, бессмысленно: "
            + "округление такую высоту не сдвинет и сломанный код останется зелёным");

        Vector3 pos = _spawner.GroundPointAtHeightUnaffectedByGrid(ovenCenterY);

        Assert.AreEqual(ovenCenterY, pos.y, Tolerance.EpsilonUnits,
            "духовка (595 мм) встаёт центром ровно на 297,5 мм: она не прилипает ни к чему "
            + "и обязана стоять на полу. Округление к шагу сетки подняло бы её на 300 мм");
    }

    [Test]
    public void GroundPointAtHeightUnaffectedByGrid_KeepsTheWorktopHeightExact()
    {
        Vector3 pos = _spawner.GroundPointAtHeightUnaffectedByGrid(ElementSpawner.WorktopHeightMeters);

        Assert.AreEqual(ElementSpawner.WorktopHeightMeters, pos.y, Tolerance.EpsilonUnits,
            "мойка и варочная встают ровно на высоту столешницы (900 мм), чтобы дальше "
            + "прилипнуть к детали");
    }

    [Test]
    public void BothPlacementRules_SnapTheHorizontalPositionToTheGrid()
    {
        Vector3 centered = _spawner.CenteredOnGroundPoint(720);
        Vector3 exactHeight = _spawner.GroundPointAtHeightUnaffectedByGrid(1f);

        Assert.AreEqual(0.15f, centered.x, Tolerance.EpsilonUnits, "X округляется к сетке в обеих ветках");
        Assert.AreEqual(0.15f, exactHeight.x, Tolerance.EpsilonUnits, "X округляется к сетке в обеих ветках");
        Assert.AreEqual(-0.40f, centered.z, Tolerance.EpsilonUnits, "Z округляется к сетке в обеих ветках");
        Assert.AreEqual(-0.40f, exactHeight.z, Tolerance.EpsilonUnits, "Z округляется к сетке в обеих ветках");
    }

    [Test]
    public void CenteredOnGroundPoint_WithGridOff_KeepsTheExactHalfHeight()
    {
        KitchenSettings.Instance.GridEnabled = false;

        Vector3 pos = _spawner.CenteredOnGroundPoint(595);

        Assert.AreEqual(0.2975f, pos.y, Tolerance.EpsilonUnits,
            "без сетки округлять нечем — центр стоит ровно на половине высоты");
    }
}

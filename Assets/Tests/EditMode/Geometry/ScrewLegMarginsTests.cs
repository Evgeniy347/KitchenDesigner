using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Четыре расстояния от центра опоры до граней детали-хозяина —
/// «слева / справа» и «сверху / снизу» в окне свойств.
///
/// Пара осей берётся НЕ из мира и не из имён X/Z, а у той грани хозяина, в
/// которую входит резьба: «слева/справа» — вдоль <c>rightAxis</c> этой грани,
/// «сверху/снизу» — вдоль её <c>upAxis</c>. Отсюда главное свойство, которое
/// тут и проверяется: оси поворачиваются ВМЕСТЕ с хозяином, поэтому опора,
/// стоящая в 60 мм от торца царги, показывает 60 и после поворота царги — она
/// не начинает мерить от другого края.
///
/// Числа избыточны: сумма пары равна размеру хозяина по этой оси. Это
/// проверяется отдельно, потому что именно на нём держится правило «правка
/// одного числа пересчитывает противоположное».</summary>
public class ScrewLegMarginsTests : SnapCoreTestBase
{
    private const int PlinthWidthMM = 482;
    private const int PlinthHeightMM = 80;
    private const int PlinthThicknessMM = 16;

    private static readonly Vector3 PlinthCentre =
        new Vector3(0f, 60f * MM, 0f);

    private static ElementGeometry Plinth(Quaternion? rotation = null) =>
        At(Make("Plinth", new Vector3Int(PlinthWidthMM, PlinthHeightMM, PlinthThicknessMM),
            rotation), PlinthCentre);

    private static ScrewLegMargins Of(Vector3 legCentreMM, Quaternion? rotation = null) =>
        ScrewLegHosting.Margins(legCentreMM * MM, Vector3.up, Plinth(rotation));

    [Test]
    public void WithNoHost_NothingIsMeasured()
    {
        var margins = ScrewLegHosting.Margins(Vector3.zero, Vector3.up, default);

        Assert.IsFalse(margins.HasHost,
            "опора в воздухе не имеет чего мерить: панель обязана показать прочерк, "
            + "а не ноль — ноль означал бы «стоит ровно на кромке»");
    }

    [Test]
    public void UnderTheCentreOfThePlinth_BothPairsAreHalvesOfIt()
    {
        var m = Of(new Vector3(0f, 0f, 0f));

        Assert.IsTrue(m.HasHost, "хозяин задан — мерить есть от чего");
        Assert.AreEqual(241f, m.LeftMM, 0.01f, "половина 482 мм влево");
        Assert.AreEqual(241f, m.RightMM, 0.01f, "и столько же вправо");
        Assert.AreEqual(8f, m.BottomMM, 0.01f,
            "вторая ось грани — толщина цоколя 16 мм, половина её");
        Assert.AreEqual(8f, m.TopMM, 0.01f, "и вторая половина");
    }

    [Test]
    public void EachPair_AddsUpToTheHostSpan_WhereverTheLegStands()
    {
        var m = Of(new Vector3(-137f, 0f, 5f));

        Assert.AreEqual(PlinthWidthMM, m.SpanAcrossMM, 0.01f,
            "сумма «слева» и «справа» — это габарит хозяина по той же оси; на этом "
            + "равенстве стоит пересчёт противоположного числа при вводе");
        Assert.AreEqual(PlinthThicknessMM, m.SpanAlongMM, 0.01f,
            "то же для «сверху» и «снизу»");
    }

    [Test]
    public void LeftGrows_WhenTheLegMovesAlongTheFacesRightAxis()
    {
        var m = Of(new Vector3(60f, 0f, 0f));

        Assert.AreEqual(301f, m.LeftMM, 0.01f,
            "сдвиг на 60 мм по правой оси грани увеличивает «слева» ровно на 60");
        Assert.AreEqual(181f, m.RightMM, 0.01f, "и на столько же уменьшает «справа»");
        Assert.AreEqual(Vector3.right, m.RightAxis,
            "у неповёрнутого цоколя правая ось нижней грани — мировой +X");
    }

    /// <summary>Тот же физический угол детали, деталь повёрнута на 90°.
    /// Если бы «слева» мерилось по мировому X, число тут стало бы 8 (половина
    /// толщины), а не 60.</summary>
    [Test]
    public void RotatingTheHost_KeepsTheLegSixtyMillimetresFromTheSameEnd()
    {
        var straight = Of(new Vector3(-181f, 0f, 0f));
        var turned = Of(new Vector3(0f, 0f, 181f), RotY(90f));

        Assert.AreEqual(60f, straight.LeftMM, 0.01f,
            "опора в 60 мм от левого торца цоколя");
        Assert.AreEqual(60f, turned.LeftMM, 0.01f,
            "цоколь повернули на 90°, опору перенесли на тот же его торец — число то же. "
            + "Оси берутся у грани хозяина и поворачиваются вместе с ним");
    }

    /// <summary>Хозяин, положенный плашмя: резьба входит уже в другую грань, и
    /// вторая ось меняется с толщины 16 мм на высоту 80 мм. Это не сбой, а
    /// смысл правила — опора вкручена в ДРУГУЮ пласть.</summary>
    [Test]
    public void WithTheHostLaidFlat_TheSecondAxisBecomesItsHeight()
    {
        var m = ScrewLegHosting.Margins(PlinthCentre, Vector3.up,
            At(Make("Plinth", new Vector3Int(PlinthWidthMM, PlinthHeightMM, PlinthThicknessMM),
                RotX(90f)), PlinthCentre));

        Assert.AreEqual(PlinthWidthMM, m.SpanAcrossMM, 0.01f,
            "длина царги осталась той же осью");
        Assert.AreEqual(PlinthHeightMM, m.SpanAlongMM, 0.01f,
            "а поперёк теперь 80 мм — та пласть, в которую вошла резьба");
    }
}

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Выделение подкрашивает СОБСТВЕННЫЙ цвет элемента, а не подменяет его.
///
/// Прежняя тонировка клала в _BaseColor жёлтую константу и плоскую эмиссию. На доске
/// с белым цветом и текстурой это даёт жёлтый множитель к текстуре — то, что нужно.
/// На варочной (тёмное стекло 0,08), духовке, стиральной машине и диване цвет задан в
/// самом _BaseColor: константа его стирала, и элемент становился ровно жёлтым — «текстура
/// подменена». Дефект не логируется и ни один прежний тест не роняет: «цвет изменился»
/// верно и для подмены.
///
/// Здесь вопрос задан напрямую по цвету, без Unity: подкраска не уходит дальше
/// середины пути к жёлтому по яркости, сохраняет порядок яркостей собственных цветов, а
/// белый (доска, по которой правило и было верным) остаётся ровно прежним жёлтым.</summary>
public class SelectionTintMathTests
{
    private const float EqualWithin = 0.0001f;
    private const float ContrastKept = 0.7f;
    private const float VisibleStep = 0.05f;

    private static readonly Color Black = new Color(0f, 0f, 0f, 1f);
    private static readonly Color CooktopGlass = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color OvenBody = new Color(0.45f, 0.45f, 0.46f, 1f);
    private static readonly Color LaundryBody = new Color(0.93f, 0.93f, 0.94f, 1f);
    private static readonly Color Fabric = new Color(0.35f, 0.40f, 0.55f, 1f);

    private static readonly Color[] Own = { Black, CooktopGlass, OvenBody, LaundryBody, Fabric, Color.white };

    private static bool Visibly(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > VisibleStep || Mathf.Abs(a.g - b.g) > VisibleStep
        || Mathf.Abs(a.b - b.b) > VisibleStep;

    private static float Dist(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    [TestCase(false)]
    [TestCase(true)]
    public void AWhiteOwnColour_GetsExactlyTheTintColour_SoTexturedBoardsLookAsTheyDid(bool multi)
    {
        var tinted = SelectionTintMath.Base(Color.white, multi);

        Assert.AreEqual(0f, Dist(tinted, SelectionTintMath.TintOf(multi)), EqualWithin,
            "белый × жёлтый = жёлтый: доска с текстурой должна желтеть как раньше");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ADarkOwnColour_IsNotReplacedByTheTint_ItStaysDarkerThanTheHalfWay(bool multi)
    {
        foreach (var own in new[] { Black, CooktopGlass, OvenBody, Fabric })
        {
            var tinted = SelectionTintMath.Base(own, multi);

            Assert.IsFalse(SelectionTintContract.Replaces(own, tinted, multi),
                "цвет " + own + " стал " + tinted + " — подкраска ушла за середину пути к "
                + "жёлтому: это подмена цвета, а не тонировка");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EveryOwnColour_ChangesVisibly_SoSelectionIsStillSeen(bool multi)
    {
        foreach (var own in Own)
        {
            var tinted = SelectionTintMath.Base(own, multi);

            Assert.IsTrue(Visibly(own, tinted),
                "цвет " + own + " не изменился заметно (" + tinted + ") — выделения не видно");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ADarkerOwnColour_NeverGivesABrighterTint(bool multi)
    {
        for (float step = 0f; step < 1f; step += 0.05f)
        {
            var darker = new Color(step, step, step, 1f);
            var lighter = new Color(step + 0.05f, step + 0.05f, step + 0.05f, 1f);

            Assert.LessOrEqual(
                SelectionTintMath.Luma(SelectionTintMath.Base(darker, multi)),
                SelectionTintMath.Luma(SelectionTintMath.Base(lighter, multi)) + SelectionTintContract.LumaSlack,
                "порядок яркостей собственных цветов обязан пережить подкраску, иначе "
                + "рядом стоящие тёмная и светлая детали выровняются в один жёлтый");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ASaturatedWarmOwnColour_StillShiftsVisibly_NotOnlyTheDarkAndTheGreyOnes(bool multi)
    {
        var warm = new[]
        {
            new Color(1f, 0.46f, 0.15f, 1f),
            new Color(0.72f, 0.26f, 0.18f, 1f),
            new Color(1f, 0.3f, 0f, 1f),
            new Color(1f, 0.8f, 0.2f, 1f),
            SelectionTintMath.TintOf(multi),
            new Color(0.9f, 0.1f, 0.1f, 1f),
        };

        foreach (var own in warm)
        {
            var tinted = SelectionTintMath.Base(own, multi);

            Assert.Greater(SelectionTintMath.MaxShift(own, tinted),
                VisibleStep,
                "цвет " + own + " почти не изменился (" + tinted + "): умножение на жёлтый "
                + "оставляет тёплое тёплым, и выделения не видно");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TheMinimumShift_DoesNotMoveColoursThatAlreadyShiftEnough(bool multi)
    {
        var tint = SelectionTintMath.TintOf(multi);

        foreach (var own in new[] { Color.white, LaundryBody, OvenBody })
        {
            var tinted = SelectionTintMath.Base(own, multi);
            var product = new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a);

            Assert.Less(SelectionTintMath.MaxShift(tinted, product),
                SelectionTintMath.DarkLift + EqualWithin,
                "цвет " + own + ": добавка видимости не должна трогать тех, кто и так заметен");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ADarkPartKeepsItsContrastWithALightOne_SoBlackGlassDoesNotMergeWithTheBody(bool multi)
    {
        var darks = new[] { new Color(0.05f, 0.05f, 0.05f, 1f), CooktopGlass, new Color(0.1f, 0.1f, 0.1f, 1f) };
        var lights = new[] { OvenBody, LaundryBody };

        foreach (var dark in darks)
            foreach (var light in lights)
            {
                float own = 1f - SelectionTintMath.Luma(dark) / SelectionTintMath.Luma(light);
                float base_ = 1f - SelectionTintMath.Luma(SelectionTintMath.Base(dark, multi))
                    / SelectionTintMath.Luma(SelectionTintMath.Base(light, multi));
                float glow = 1f - SelectionTintMath.Luma(SelectionTintMath.Emission(dark, multi))
                    / SelectionTintMath.Luma(SelectionTintMath.Emission(light, multi));

                Assert.GreaterOrEqual(base_, ContrastKept * own,
                    "тёмный " + dark + " и светлый " + light + ": контраст цвета " + own.ToString("0.00")
                    + " → " + base_.ToString("0.00") + " — жёлтый выровнял чёрное с корпусом");
                Assert.GreaterOrEqual(glow, ContrastKept * own,
                    "тёмный " + dark + " и светлый " + light + ": контраст свечения "
                    + own.ToString("0.00") + " → " + glow.ToString("0.00"));
            }
    }

    [Test]
    public void TheAlphaOfTheOwnColour_SurvivesTheTint()
    {
        var own = new Color(0.4f, 0.4f, 0.4f, 0.3f);

        Assert.AreEqual(own.a, SelectionTintMath.Base(own, false).a, EqualWithin,
            "прозрачность собственного цвета — часть его вида, подкраска её не трогает");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TheGlow_OfAWhiteColour_IsTheOldFlatOne_AndOfADarkOneIsWeaker(bool multi)
    {
        var white = SelectionTintMath.Emission(Color.white, multi);
        var old = SelectionTintMath.GlowTint * SelectionTintMath.GlowOf(multi);
        var dark = SelectionTintMath.Emission(CooktopGlass, multi);

        Assert.AreEqual(0f, Dist(white, old), EqualWithin,
            "у белой доски свечение прежнее — у неё правило уже было верным");
        Assert.Less(SelectionTintMath.Luma(dark), SelectionTintMath.Luma(white),
            "плоское свечение на тёмной детали съедает её цвет целиком — на тёмной оно слабее");
    }

    [Test]
    public void AMultiSelection_IsPalerThanASingleOne()
    {
        Assert.Less(SelectionTintMath.Emission(OvenBody, true).maxColorComponent,
            SelectionTintMath.Emission(OvenBody, false).maxColorComponent,
            "группа выделена мягче одиночного — как и было");
    }
}


using NUnit.Framework;
using KitchenDesigner.Core;

public class DrawerMountConstantsTests
{
    [Test]
    public void GtvMountingConstants_KeepTheAxisProBrochureValues()
    {
        Assert.AreEqual(37.5f, DrawerConstants.SLIDE_CLEARANCE_PER_SIDE, 1e-4f,
            "зазор направляющих на сторону, GTV AXIS PRO, брошюра «Преимущества», стр. 6 и 8");
        Assert.AreEqual(14, DrawerConstants.SIDE_WALL_THICKNESS,
            "толщина металлической боковины ящика по той же брошюре");
        Assert.AreEqual(16, DrawerConstants.PANEL_THICKNESS,
            "толщина плиты дна и задней стенки по той же брошюре");
        Assert.AreEqual(75, DrawerConstants.BOTTOM_WIDTH_INSET,
            "ширина дна = LW - 75, где LW — проём корпуса «в свету»: ширина ящика "
            + "задаётся ПРОЁМОМ, а не наружным размером короба");
        Assert.AreEqual(87, DrawerConstants.BACK_WIDTH_INSET,
            "ширина задника = LW - 87 по той же брошюре");
        Assert.AreEqual(24, DrawerConstants.BOTTOM_DEPTH_INSET,
            "глубина дна = NL - 24 (сборка версия 1), NL — номинальная длина направляющей");
        Assert.AreEqual(8, DrawerConstants.BACK_REAR_OFFSET,
            "задняя грань задника = NL - 8 по той же брошюре");
    }

    [Test]
    public void GtvBackIsNarrowerThanTheBottom_BecauseItSitsBetweenTheSideWalls()
    {
        Assert.Greater(DrawerConstants.BACK_WIDTH_INSET, DrawerConstants.BOTTOM_WIDTH_INSET,
            "задник встаёт МЕЖДУ металлических боковин, а дно ложится под них: перепутав "
            + "два вычета местами, получаем задник шире проёма между боковинами");
    }

    [Test]
    public void MoventoWidthInset_IsBothSlideClearances()
    {
        Assert.AreEqual(2 * DrawerConstants.MOVENTO_SLIDE_CLEARANCE_PER_SIDE,
            DrawerConstants.MOVENTO_WIDTH_INSET,
            "наружная ширина деревянного короба Movento SKW = LW - 42 — это ровно два "
            + "зазора направляющей по 21 мм (формулы Blum, «Building a MOVENTO drawer»)");
    }

    [Test]
    public void MoventoFrontAndBackInset_IsTheBoxMinusBothSideBoards()
    {
        Assert.AreEqual(
            DrawerConstants.MOVENTO_WIDTH_INSET + 2 * DrawerConstants.MOVENTO_BOARD_THICKNESS,
            DrawerConstants.MOVENTO_FRONT_BACK_INSET,
            "перед и задник встают МЕЖДУ боковин: их ширина = SKW - 2*16 = LW - 74. "
            + "Поменяв толщину плиты и забыв этот вычет, получаем короб, который не "
            + "собирается");
    }

    [Test]
    public void MoventoBottomNiche_IsThinnerThanTheBoard_SoTheSlideHidesUnderTheBottom()
    {
        Assert.Greater(DrawerConstants.MOVENTO_BOTTOM_NICHE, 0,
            "дно приподнято над низом боковин — в этот просвет уходит скрытая направляющая");
        Assert.Less(DrawerConstants.MOVENTO_BOTTOM_NICHE, DrawerConstants.MOVENTO_BOARD_THICKNESS,
            "просвет мельче толщины плиты: высота переда и задника = H - 14 - 16, и "
            + "просвет глубже плиты съел бы всю высоту короба");
    }

    [Test]
    public void DrawerTypeValues_AreHeightsInMillimetres_NotDropdownIndices()
    {
        for (int index = 0; index < DrawerConstants.Types.Length; index++)
        {
            var type = DrawerConstants.Types[index];
            Assert.AreEqual((int)type, DrawerConstants.GetTypeHeight(type),
                "значение enum — это высота ящика в мм");
            Assert.AreNotEqual(index, (int)type,
                "поэтому индекс дропдауна кастовать в DrawerType напрямую нельзя: "
                + "перевод идёт только через Types/TypeIndex/TypeFromIndex");
        }
    }
}

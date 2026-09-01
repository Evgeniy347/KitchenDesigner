using System;
using NUnit.Framework;
using KitchenDesigner.Core;

public class AppConstantsTests
{
    [Test]
    public void GrooveMaxPerPart_IsEverySideTimesEveryKind()
    {
        int sides = Enum.GetValues(typeof(GrooveSide)).Length;
        int kinds = Enum.GetValues(typeof(GrooveKind)).Length;

        Assert.AreEqual(sides * kinds, AppConstants.GROOVE_MAX_PER_PART,
            "потолок пазов на деталь — это ровно «каждая сторона в каждом виде»: "
            + "смещение от кромки фиксировано, поэтому дубль «сторона+вид» бессмыслен. "
            + "Добавили сторону или вид — потолок обязан вырасти вместе с ними, иначе "
            + "часть сочетаний станет недостижима");
    }

    [Test]
    public void AssembledFacadeConstants_KeepTheValuesOfTheSoyuzFacadCatalogue()
    {
        Assert.AreEqual(100, AppConstants.ASSEMBLED_FRAME_MM,
            "ширина рамки A рамочного фасада по каталогу Союз-Фасад, стр. 43");
        Assert.AreEqual(180, AppConstants.ASSEMBLED_GLASS_DEDUCT_MM,
            "вычет под вкладное стекло по тому же каталогу: L-180 и H-180");
        Assert.AreEqual(4, AppConstants.ASSEMBLED_GLASS_THICKNESS_MM,
            "толщина вкладного стекла по тому же каталогу");
        Assert.AreEqual(5, AppConstants.ASSEMBLED_GROOVE_MM,
            "выемка на перекладине 5x5 мм по тому же каталогу");
    }

    [Test]
    public void AssembledDefaultGrooveCount_IsAboveZero_SoRailsComeGroovedOutOfTheBox()
    {
        Assert.Greater(AppConstants.ASSEMBLED_DEFAULT_GROOVES, 0,
            "ноль означает «перекладины без выемок» (AssembledFacadeMesh рисует по две "
            + "выемки на каждой из двух перекладин только при grooveCount > 0). Значение "
            + "по умолчанию — С выемками: обнулив его, рамочный фасад молча станет гладким");
    }

    [Test]
    public void EdgeMaxSideMm_IsAboveTheDefaultBoardThickness()
    {
        Assert.Greater(AppConstants.EDGE_MAX_SIDE_MM, AppConstants.BOARD_THICKNESS_DEFAULT,
            "порог «тонкой стороны» обязан пропускать обычную плиту: деталь под кромку — "
            + "та, у которой РОВНО одна сторона тоньше порога, и эта сторона и есть "
            + "толщина плиты. Опустив порог до толщины плиты, кромку потеряли бы все листы");
    }

    [Test]
    public void EdgeThicknessDefault_LiesInsideTheTapeRange()
    {
        Assert.GreaterOrEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM,
            AppConstants.EDGE_THICKNESS_MIN_MM,
            "толщина кромки по умолчанию не может быть тоньше самой тонкой ленты");
        Assert.LessOrEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM,
            AppConstants.EDGE_THICKNESS_MAX_MM,
            "и не может быть толще самой толстой: значение по умолчанию, которое "
            + "зажимается при первом же применении, — это скрытый дефект");
    }

    [Test]
    public void MmToUnits_TurnsAThousandMillimetresIntoOneUnit()
    {
        Assert.AreEqual(1f, 1000 * AppConstants.MM_TO_UNITS, 1e-6f,
            "юнит сцены — метр, размеры задаются в миллиметрах (CONVENTIONS.md, Units)");
    }
}

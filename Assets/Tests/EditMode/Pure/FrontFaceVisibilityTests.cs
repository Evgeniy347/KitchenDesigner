using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Чем кадр С ЛИЦОМ отличается от кадра с пустой гранью — без глаза.
///
/// Пока изометрия снимала машины со спины, их эталоны были ПРИНЯТЫ и не
/// стерегли ничего: голая коробка совпадает с голой коробкой при любой поломке
/// лица. Отличие меряется здесь: деталь, объявленную лицевой
/// (<c>ElementFront.Parts</c>), обязано быть видно из той стороны, с которой
/// стоит камера, — то есть ни одна другая деталь того же элемента не стоит
/// между ней и объективом.
///
/// Модель в тестах — бак посудомойки: пять коробок корпуса, люк и панель
/// управления на грани +Z. Числа условные, важна только взаимная
/// расстановка.</summary>
public class FrontFaceVisibilityTests
{
    private const string Door = "Door";
    private const string Panel = "ControlPanel";

    private static List<FacePart> Dishwasher() => new List<FacePart>
    {
        Part("BodyBack", new Vector3(0f, 0f, -0.30f), new Vector3(0.60f, 0.80f, 0.02f)),
        Part("BodyLeft", new Vector3(-0.29f, 0f, 0f), new Vector3(0.02f, 0.80f, 0.60f)),
        Part("BodyRight", new Vector3(0.29f, 0f, 0f), new Vector3(0.02f, 0.80f, 0.60f)),
        Part("BodyTop", new Vector3(0f, 0.39f, 0f), new Vector3(0.60f, 0.02f, 0.60f)),
        Part("BodyBottom", new Vector3(0f, -0.39f, 0f), new Vector3(0.60f, 0.02f, 0.60f)),
        Part(Door, new Vector3(0f, -0.05f, 0.29f), new Vector3(0.58f, 0.60f, 0.02f)),
        Part(Panel, new Vector3(0f, 0.32f, 0.30f), new Vector3(0.58f, 0.10f, 0.01f)),
    };

    private static FacePart Part(string name, Vector3 centre, Vector3 size) =>
        new FacePart(name, new Bounds(centre, size));

    private static readonly string[] Declared = { Door, Panel };

    [Test]
    public void FromTheSideTheRigStandsOn_TheDeclaredPartsAreInTheFrame()
    {
        var hidden = FrontFaceVisibility.Hidden(Dishwasher(), Declared, IsoCameraRig.ViewDir);

        Assert.IsEmpty(hidden,
            "камера стоит со стороны лица — люк и панель обязаны быть видны. Если этот "
            + "тест красный, съёмщик снова снимает затылок: " + string.Join(", ", hidden));
    }

    /// <summary>Противоположный вход, без которого проверка выше зелёная на чём
    /// угодно: с той стороны, где камера стояла ДО этой правки, лицо закрыто
    /// собственным корпусом. Это и есть та самая «голая коробка», принятая в
    /// эталоны.</summary>
    [Test]
    public void FromTheOppositeSide_TheSamePartsAreHidden_WhichIsTheDefectItself()
    {
        var hidden = FrontFaceVisibility.Hidden(Dishwasher(), Declared, IsoCameraRig.IsoDir);

        CollectionAssert.AreEquivalent(new[] { Door, Panel }, hidden,
            "со стороны -Z обе лицевые детали закрыты стенками бака — кадр показывает "
            + "коробку. Зелёное здесь означало бы, что сенсор не умеет краснеть вовсе");
    }

    [Test]
    public void APartNobodyBuilt_IsReportedByName_NotSilentlyCountedAsVisible()
    {
        var hidden = FrontFaceVisibility.Hidden(Dishwasher(), new[] { "Griddle" },
            IsoCameraRig.ViewDir);

        Assert.AreEqual(new[] { "Griddle" + FrontFaceVisibility.MissingPartSuffix }, hidden,
            "объявленная, но не построенная деталь — это опечатка в объявлении либо "
            + "переименованный ребёнок; пустой список находок сделал бы объявление "
            + "украшением");
    }

    /// <summary>Имя сопоставляется ПРЕФИКСОМ: у розетки и выключателя лицевые
    /// детали нумеруются по посту (<c>SocketWell0</c>, <c>SocketWell1</c>), и
    /// объявление обязано накрывать все посты разом, а не первый.</summary>
    [Test]
    public void DeclaringAPrefix_CoversEveryNumberedPart_AndEachOneIsChecked()
    {
        var socket = new List<FacePart>
        {
            Part("SocketPlate", new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.08f, 0.01f)),
            Part("SocketWell0", new Vector3(-0.04f, 0f, 0.006f), new Vector3(0.05f, 0.05f, 0.004f)),
            Part("SocketWell1", new Vector3(0.04f, 0f, 0.006f), new Vector3(0.05f, 0.05f, 0.004f)),
        };

        Assert.IsEmpty(FrontFaceVisibility.Hidden(socket, new[] { "SocketWell" },
                IsoCameraRig.ViewDir),
            "оба гнезда видны с лицевой стороны");

        var buried = new List<FacePart>(socket)
        {
            Part("SocketLid", new Vector3(0f, 0f, 0.05f), new Vector3(0.16f, 0.08f, 0.01f)),
        };

        CollectionAssert.AreEquivalent(new[] { "SocketWell0", "SocketWell1" },
            FrontFaceVisibility.Hidden(buried, new[] { "SocketWell" }, IsoCameraRig.ViewDir),
            "накрой их крышкой — и находки придут ПООТДЕЛЬНО, по одной на пост: иначе "
            + "закрытый второй пост прятался бы за открытым первым");
    }

    /// <summary>Накладка, лежащая НА детали, не имеет права считаться её
    /// заслоном. Стекло духовки утоплено в фасад, а панель управления лежит на
    /// нём же; габаритная коробка фасада шире стекла, и наивная проверка
    /// «коробка на пути» объявила бы стекло невидимым. Заслоном считается
    /// только то, чья передняя точка ВПЕРЕДИ центра проверяемой детали.</summary>
    [Test]
    public void TheCarrierBehindAnOverlay_IsNotItsOccluder()
    {
        var oven = new List<FacePart>
        {
            Part("Facade", new Vector3(0f, 0f, 0.28f), new Vector3(0.60f, 0.45f, 0.02f)),
            Part("Glass", new Vector3(0f, -0.03f, 0.292f), new Vector3(0.50f, 0.34f, 0.004f)),
        };

        Assert.IsEmpty(FrontFaceVisibility.Hidden(oven, new[] { "Glass" }, IsoCameraRig.ViewDir),
            "стекло лежит поверх фасада и обязано считаться видимым");
    }

    /// <summary>Обратная сторона той же пары, и она оплачена красным прогоном:
    /// духовка объявляла лицевой деталью ПЛИТУ дверцы (<c>Facade</c>), а её
    /// середину закрывает собственное стекло — плита утоплена под накладку
    /// ровно на её толщину (<c>conventions/SHAPE-AND-SCREENSHOTS.md</c> →
    /// «Накладка, лежащая на детали…»). Сенсор был прав: середины плиты в
    /// кадре нет ни с какой стороны. Объявлять надо то, что лежит СВЕРХУ, —
    /// стекло, панель и ручку.</summary>
    [Test]
    public void ACarrierSunkUnderItsOwnOverlay_IsNotADeclarableFacePart()
    {
        var oven = new List<FacePart>
        {
            Part("Facade", new Vector3(0f, 0f, 0.28f), new Vector3(0.60f, 0.45f, 0.02f)),
            Part("Glass", new Vector3(0f, -0.03f, 0.292f), new Vector3(0.50f, 0.34f, 0.004f)),
        };

        CollectionAssert.AreEquivalent(new[] { "Facade" },
            FrontFaceVisibility.Hidden(oven, new[] { "Facade" }, IsoCameraRig.ViewDir),
            "плиту под накладкой в кадре не видно, и ослаблять сенсор ради неё нельзя: "
            + "тогда он перестанет отличать «деталь закрыта своим же стеклом» от "
            + "«деталь закрыта задней стенкой», ради чего и заведён");
    }

    /// <summary>Вторая находка того же красного прогона: обод люка стиральной
    /// машины (<c>HatchRim</c>) — КОЛЬЦО, и центр его габаритной коробки это
    /// дырка, а не поверхность. В дырке сидит стекло, поэтому обод «не виден»
    /// при любом ракурсе. Это не дефект сенсора и не повод его ослаблять — это
    /// оговорка про AABB у круглых деталей, уже записанная в
    /// <c>CoplanarSurfaceCoverageTests</c>: объявляется то, что дырку
    /// ЗАПОЛНЯЕТ, а кольцо вокруг него видно вместе с ним.</summary>
    [Test]
    public void ARingIsNotADeclarableFacePart_ItsBoxCentreIsTheHole()
    {
        var hatch = new List<FacePart>
        {
            Part("HatchRim", new Vector3(0f, 0f, 0.30f), new Vector3(0.40f, 0.40f, 0.018f)),
            Part("HatchGlass", new Vector3(0f, 0f, 0.305f), new Vector3(0.35f, 0.35f, 0.010f)),
        };

        CollectionAssert.AreEquivalent(new[] { "HatchRim" },
            FrontFaceVisibility.Hidden(hatch, new[] { "HatchRim" }, IsoCameraRig.ViewDir),
            "центр коробки кольца занят стеклом — объявлять лицевым надо стекло");

        Assert.IsEmpty(FrontFaceVisibility.Hidden(hatch, new[] { "HatchGlass" },
                IsoCameraRig.ViewDir),
            "а стекло видно, и вместе с ним в кадре оказывается и обод вокруг него: "
            + "проверка не теряет чувствительности от того, что кольцо не объявлено");
    }
}

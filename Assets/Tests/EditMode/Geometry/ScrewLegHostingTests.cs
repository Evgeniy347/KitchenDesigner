using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Хозяин винтовой опоры выводится из геометрии, а не выбирается в
/// поле: это тот элемент, ВНУТРИ которого находится резьба.
///
/// Функция чистая НАМЕРЕННО и это её главное свойство: вывод связи зовут после
/// загрузки проекта, дублирования, отмены и мутации по MCP, и если бы он тянул
/// за собой вертикальную посадку, первое же открытие проекта опустило бы все
/// опоры и молча испортило сцену. Двигать опору здесь нечем — на входе коробки,
/// на выходе индекс.
///
/// Двойное вхождение (резьба прошла доску насквозь и вошла во вторую) решается
/// НЕ «оба хозяева» и не «последний выигрывает»: хозяин ровно один — тот, в
/// который резьба входит первым по направлению ввинчивания, то есть нижний.
/// Остальные ядро объявит COL-01 само.</summary>
public class ScrewLegHostingTests
{
    private const float MM = 0.001f;

    private static readonly float Margin = Tolerance.ContactMm * MM;

    private static ElementGeometry Box(string name, float centreYMm, float heightMm,
        float centreXMm = 0f, float widthMm = 400f) =>
        ElementGeometry.Box(name, new Vector3(centreXMm, centreYMm, 0f) * MM,
            new Vector3(widthMm, heightMm, 400f) * MM);

    private static ElementGeometry Thread(float bottomMm, float topMm) =>
        ElementGeometry.Box("Опора/thread",
            new Vector3(0f, (bottomMm + topMm) * 0.5f, 0f) * MM,
            new Vector3(6f, topMm - bottomMm, 6f) * MM);

    [Test]
    public void TheBoardTheThreadIsInside_IsTheHost()
    {
        var host = ScrewLegHosting.HostIndex(Thread(8f, 45f),
            new[] { Box("Мимо", 300f, 18f), Box("Дно", 45f, 30f) }, Margin);

        Assert.AreEqual(1, host, "хозяин — тот, в ком резьба, а не тот, кто ближе по списку");
    }

    [Test]
    public void NothingOnTheThread_LeavesTheLinkEmpty()
    {
        var host = ScrewLegHosting.HostIndex(Thread(8f, 45f),
            new[] { Box("Полка", 300f, 18f) }, Margin);

        Assert.AreEqual(ScrewLegHosting.NoHost, host,
            "это и есть случай, который раньше молча оставлял старое имя хозяина");
    }

    [Test]
    public void TouchingTheThreadTip_IsNotEntering()
    {
        var host = ScrewLegHosting.HostIndex(Thread(8f, 45f),
            new[] { Box("Дно", 54f, 18f) }, Margin);

        Assert.AreEqual(ScrewLegHosting.NoHost, host,
            "деталь начинается ровно там, где кончается резьба: опора её подпирает, "
            + "а не вкручена в неё");
    }

    [Test]
    public void ThreadThroughTwoBoards_TakesTheLowerOne()
    {
        var boards = new[] { Box("Верхняя", 50f, 20f), Box("Нижняя", 20f, 20f) };

        Assert.AreEqual(1, ScrewLegHosting.HostIndex(Thread(8f, 60f), boards, Margin),
            "по направлению ввинчивания резьба входит в нижнюю первой");
    }

    [Test]
    public void ThreadThroughTwoBoards_DoesNotDependOnTheOrderOfTheScene()
    {
        var forward = new[] { Box("Верхняя", 50f, 20f), Box("Нижняя", 20f, 20f) };
        var reversed = new[] { Box("Нижняя", 20f, 20f), Box("Верхняя", 50f, 20f) };

        Assert.AreEqual("Нижняя", forward[ScrewLegHosting.HostIndex(Thread(8f, 60f), forward, Margin)].Name,
            "контроль: в прямом порядке хозяин нижний");
        Assert.AreEqual("Нижняя", reversed[ScrewLegHosting.HostIndex(Thread(8f, 60f), reversed, Margin)].Name,
            "порядок элементов в проекте — не геометрия: связь обязана быть той же");
    }

    [Test]
    public void TwoBoardsStartingAtTheSameHeight_AreResolvedByName_NotByOrder()
    {
        var forward = new[] { Box("Б", 20f, 20f, -6f, 12f), Box("А", 20f, 20f, 6f, 12f) };
        var reversed = new[] { Box("А", 20f, 20f, 6f, 12f), Box("Б", 20f, 20f, -6f, 12f) };

        Assert.AreEqual("А", forward[ScrewLegHosting.HostIndex(Thread(8f, 60f), forward, Margin)].Name,
            "контроль: в прямом порядке побеждает не первый по списку, а первый по имени");
        Assert.AreEqual("А", reversed[ScrewLegHosting.HostIndex(Thread(8f, 60f), reversed, Margin)].Name,
            "ничья по высоте разводится именем — иначе одна и та же сцена давала бы "
            + "разную связь после пересортировки");
    }

    [Test]
    public void ABoardBesideTheThread_IsNotAHost()
    {
        var host = ScrewLegHosting.HostIndex(Thread(8f, 45f),
            new[] { Box("Сосед", 20f, 20f, 15.5f, 18f) }, Margin);

        Assert.AreEqual(ScrewLegHosting.NoHost, host,
            "деталь в 3,5 мм от резьбы стоит в габарите пятака Ø25, но резьбы не касается");
    }
}

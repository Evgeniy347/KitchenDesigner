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

    /// <summary>Заход считается ТЕМ ЖЕ телом резьбы и ТЕМ ЖЕ хозяином, которыми
    /// выведена связь: иначе у одной величины появилось бы два описания, и они
    /// разошлись бы — ровно так поле «заход в корпус» показывало 25 мм там, где
    /// резьба сидела на 38.</summary>
    [Test]
    public void TheInsertion_IsMeasuredFromTheHostsLowerFaceUpwards()
    {
        Assert.AreEqual(38f, ScrewLegHosting.InsertionMM(Thread(8f, 58f), Box("Царга", 60f, 80f)),
            0.01f, "царга 20..100, резьба кончается на 58 — внутри 38 мм");
        Assert.AreEqual(16f, ScrewLegHosting.InsertionMM(Thread(8f, 58f), Box("Царга", 50f, 16f)),
            0.01f, "резьба прошла 16-мм царгу насквозь: внутри вся её толщина, "
            + "а торчащий хвост — законный, его считает ProtrusionMM");
    }

    [Test]
    public void AHostBelowTheThreadTip_ContributesNoInsertion()
    {
        Assert.AreEqual(0f, ScrewLegHosting.InsertionMM(Thread(8f, 45f), Box("Дно", 54f, 18f)),
            0.01f, "деталь начинается там, где кончается резьба: заход нулевой, "
            + "и это отличается от «хозяина нет» — там величины нет вовсе");
        Assert.AreEqual(0f, ScrewLegHosting.InsertionMM(Thread(8f, 45f), default),
            0.01f, "пустая коробка не даёт захода");
    }

    [Test]
    public void AHostSwallowingTheWholeThread_CountsOnlyTheThread()
    {
        Assert.AreEqual(37f, ScrewLegHosting.InsertionMM(Thread(8f, 45f), Box("Стойка", 300f, 600f)),
            0.01f, "хозяин начинается ниже резьбы — внутри она вся, 45−8, "
            + "а не расстояние до его дна");
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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using static ValidationTestScene;

/// <summary>Кто в жесте мувер — не объявляется, а ОБНАРУЖИВАЕТСЯ.
///
/// <see cref="GestureValidation"/> замораживает сцену на первом кадре жеста,
/// а дальше каждый кадр сверяет позы со снимком заморозки: кто уехал, тот и
/// мувер. Это сделано не ради красоты. Во время перетаскивания едет не только
/// деталь под курсором: за фитингом идут трубы (<c>PipeRunFollow</c>), винтовая
/// опора пересаживается на нового хозяина, группа едет целиком, ресайз меняет
/// габариты. Список «кто считается мувером», составленный по памяти, разошёлся
/// бы с действительностью ровно так, как разъезжается любой список мест — и
/// разошёлся бы МОЛЧА, отдав пользователю подсветку по устаревшему графу.
///
/// Обнаружение стоит O(n) сравнений поз против O(n²) пар, а растёт множество
/// муверов монотонно, поэтому заморозок за жест — единицы, а не по одной на
/// кадр. Это и проверяется ниже: и совпадение с полным проходом, и что
/// заморозки прекращаются.</summary>
public class GestureValidationTests
{
    private static CoreValidationResult Frame(GestureValidation gesture,
        List<ValidationElement> parts)
    {
        var live = new CoreValidationResult();
        Assert.IsTrue(gesture.TryValidate(parts, live),
            "сцена с якорем — инкрементальный проход обязан взяться за кадр");
        Assert.AreEqual(FullFingerprint(parts), Fingerprint(live, parts),
            "кадр жеста разошёлся с полной валидацией");
        return live;
    }

    [Test]
    public void EveryFrameOfAGesture_MatchesFullValidation()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER", new Vector3(4000, 2000, 2500)));
        int mover = parts.Count - 1;

        var gesture = new GestureValidation();
        Frame(gesture, parts);

        foreach (var pose in GesturePoses())
        {
            parts[mover] = Mover("MOVER", pose);
            Frame(gesture, parts);
        }
    }

    /// <summary>Заморозок за жест — две: одна при захвате, вторая когда мувер
    /// впервые сдвинулся и был опознан. Дальше — ни одной, сколько бы кадров
    /// ни шло. Если бы обнаружение перезамораживало сцену на каждом движении,
    /// кадр стоил бы полную валидацию и вся работа была бы напрасной.</summary>
    [Test]
    public void TheFreezeHappensTwice_AndThenNeverAgainForTheRestOfTheGesture()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER", new Vector3(4000, 2000, 2500)));
        int mover = parts.Count - 1;

        var gesture = new GestureValidation();
        Frame(gesture, parts);
        Assert.AreEqual(1, gesture.Freezes, "захват жеста — первая заморозка");
        Assert.AreEqual(0, gesture.MoverCount, "при захвате ещё никто не двигался");

        parts[mover] = Mover("MOVER", new Vector3(1200, 879, 0));
        Frame(gesture, parts);
        Assert.AreEqual(2, gesture.Freezes, "мувер опознан — сцена заморожена без него");
        Assert.AreEqual(1, gesture.MoverCount, "опознан ровно один мувер");

        for (int i = 0; i < 40; i++)
        {
            parts[mover] = Mover("MOVER", new Vector3(1200 + i, 879 + i * 0.5f, 0));
            Frame(gesture, parts);
        }

        Assert.AreEqual(2, gesture.Freezes,
            $"дальше жест обязан идти по замороженному графу, а заморозок стало {gesture.Freezes}");
        Assert.Less(gesture.PairsInLastFrame, 40,
            $"кадр жеста обязан стоить единицы пар, а стоит {gesture.PairsInLastFrame}");
    }

    /// <summary>Деталь, которую никто не объявлял мувером, уезжает вслед за
    /// перетаскиваемой — так ходят трубы за фитингом. Её обязано опознать, и
    /// результат обязан остаться точным.</summary>
    [Test]
    public void APartThatFollowsTheDrag_IsDiscoveredAsAMoverToo()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER", new Vector3(4000, 2000, 2500)));
        parts.Add(Part("FOLLOWER", new Vector3(5000, 2000, 2500), new Vector3(400, 18, 400)));
        int mover = parts.Count - 2, follower = parts.Count - 1;

        var gesture = new GestureValidation();
        Frame(gesture, parts);

        parts[mover] = Mover("MOVER", new Vector3(1200, 879, 0));
        Frame(gesture, parts);
        Assert.AreEqual(1, gesture.MoverCount, "пока уехала только деталь под курсором");

        parts[mover] = Mover("MOVER", new Vector3(1200, 900, 0));
        parts[follower] = Part("FOLLOWER", new Vector3(1200, 1030, 0), new Vector3(400, 18, 400));
        Frame(gesture, parts);
        Assert.AreEqual(2, gesture.MoverCount,
            "деталь, уехавшая без объявления, обязана быть опознана как мувер");

        for (int i = 0; i < 10; i++)
        {
            parts[mover] = Mover("MOVER", new Vector3(1200 + i * 2, 900, 0));
            parts[follower] = Part("FOLLOWER", new Vector3(1200 + i * 2, 1030, 0),
                new Vector3(400, 18, 400));
            Frame(gesture, parts);
        }
        Assert.AreEqual(3, gesture.Freezes,
            "множество муверов растёт монотонно — заморозки обязаны прекратиться");
    }

    /// <summary>Ресайз — такой же жест: у детали меняются габариты, а не
    /// положение. Обнаружение смотрит на снимок целиком, поэтому ловит и это.</summary>
    [Test]
    public void AResizeIsDiscoveredTheSameWay_AndStaysExact()
    {
        var parts = Kitchen(12);
        parts.Add(Part("STRETCHED", new Vector3(1200, 879, 0), new Vector3(18, 242, 400)));
        int stretched = parts.Count - 1;

        var gesture = new GestureValidation();
        Frame(gesture, parts);

        for (int height = 250; height <= 290; height += 5)
        {
            parts[stretched] = Part("STRETCHED", new Vector3(1200, 758 + height * 0.5f, 0),
                new Vector3(18, height, 400));
            Frame(gesture, parts);
        }
        Assert.AreEqual(2, gesture.Freezes,
            "растягиваемая деталь опознаётся один раз, дальше граф заморожен");
    }

    /// <summary>Сцена сменила состав — замороженный граф ссылается на соседей по
    /// ПОРЯДКОВОМУ НОМЕРУ, и после вставки эти номера означают другие детали.
    /// Заморозка обязана быть сброшена, а не подправлена.</summary>
    [Test]
    public void WhenTheSceneGainsAPart_TheFreezeIsDropped()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER", new Vector3(1200, 879, 0)));

        var gesture = new GestureValidation();
        Frame(gesture, parts);
        Assert.AreEqual(1, gesture.Freezes, "захват жеста — первая заморозка");

        parts.Add(Part("NEWCOMER", new Vector3(3000, 121, 700), new Vector3(400, 18, 400)));
        Frame(gesture, parts);
        Assert.AreEqual(2, gesture.Freezes, "состав сцены изменился — граф пересчитан заново");
        Assert.AreEqual(0, gesture.MoverCount, "после сброса муверы ищутся с нуля");
    }

    /// <summary>Сцена без якоря: заморозка отказывает, и отказ ПОВТОРЯЕТСЯ
    /// каждый кадр — вызывающий честно платит полную валидацию, а не получает
    /// тихо неверную подсветку. Второй раз сцену никто не пытается заморозить:
    /// ответ «якоря нет» за жест не меняется.</summary>
    [Test]
    public void WithoutAnAnchor_TheGestureRefusesEveryFrame()
    {
        var parts = new List<ValidationElement>
        {
            Part("A0", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
            Part("A1", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
            Part("B0", new Vector3(5000, 9, 0), new Vector3(800, 18, 400)),
        };

        var gesture = new GestureValidation();
        var live = new CoreValidationResult();
        for (int i = 0; i < 5; i++)
        {
            parts[0] = Part("A0", new Vector3(i * 10, 9, 0), new Vector3(800, 18, 400));
            Assert.IsFalse(gesture.TryValidate(parts, live),
                "без якоря землёй считается самый нижний уровень ВСЕЙ сцены — " +
                "локальный пересчёт этого не видит, отказ обязан повториться");
        }

        Assert.AreEqual(0, gesture.Freezes, "заморозки не случилось ни разу");
        Assert.IsFalse(gesture.IsFrozen, "и замороженного графа нет");
    }

    /// <summary>После жеста кэш сбрасывается, и следующий жест начинается с
    /// чистого листа — иначе сцена, изменившаяся между жестами командой или
    /// отменой, считалась бы по графу прошлого раза.</summary>
    [Test]
    public void AfterReset_TheNextGestureFreezesAfresh()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER", new Vector3(1200, 879, 0)));
        int mover = parts.Count - 1;

        var gesture = new GestureValidation();
        Frame(gesture, parts);
        parts[mover] = Mover("MOVER", new Vector3(1200, 900, 0));
        Frame(gesture, parts);
        Assert.AreEqual(2, gesture.Freezes, "первый жест: захват и опознание мувера");

        gesture.Reset();
        Assert.IsFalse(gesture.IsFrozen, "сброс убирает замороженный граф");
        Assert.AreEqual(0, gesture.MoverCount, "и забывает муверов прошлого жеста");

        Frame(gesture, parts);
        Assert.AreEqual(3, gesture.Freezes, "новый жест считает сцену заново");
    }
}

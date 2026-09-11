using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож работы, а не времени, для <see cref="BoxOverlap.PenetrationUnits"/> —
/// самой горячей функции открытия фасада. Профиль пользователя (839 кадров,
/// test-results/perf/perf_20260911_163010.csv) назвал её виновником через
/// <c>OpeningCollision.ScanForBlock</c>: 15 вызовов по 258 мс, при том что построение
/// списка препятствий в тех же кадрах стоило 1,6 мс.
///
/// Причина — цена одного вызова: пятнадцать разделяющих осей, на каждой два
/// <c>RadiusAlong</c>, а каждый из них трижды поворачивает вектор кватернионом. Девяносто
/// поворотов на пару коробок, и скан делает 128 шагов × все препятствия. Замерено на ядре
/// под dotnet, 128×400: <b>76,2 мс и 51 200 проверок осей — было, 4,2 мс и 384 — стало</b>,
/// после того как перед SAT встал отказ по описанным сферам.
///
/// Отказ ТОЧНЫЙ, а не приближённый: сфера описана вокруг коробки, поэтому непересекающиеся
/// сферы означают непересекающиеся коробки, и ответ 0 совпадает с тем, что вернул бы полный
/// SAT. Считать здесь надо именно проверки осей: миллисекунды в batch флаки.</summary>
public class BoxOverlapRejectTests
{
    private static OrientedBox Box(Vector3 center, Vector3 half) =>
        new OrientedBox(center, Quaternion.identity, half);

    [SetUp]
    public void SetUp() => BoxOverlap.TakeSeparatingAxisTests();

    /// <summary>Главный сенсор: пара, до которой скан не дотягивается, обязана стоить ноль
    /// разделяющих осей. Ненулевое число здесь — это те самые 51 200 проверок на один скан
    /// открытия дверцы.</summary>
    [Test]
    public void BoxesTooFarApartToTouch_CostNoSeparatingAxisTestAtAll()
    {
        var a = Box(new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.36f, 0.009f));
        var b = Box(new Vector3(5f, 0f, 0f), new Vector3(0.3f, 0.36f, 0.009f));

        Assert.AreEqual(0f, BoxOverlap.PenetrationUnits(a, b),
            "далёкие коробки не пересекаются — ответ прежний");
        Assert.AreEqual(0, BoxOverlap.TakeSeparatingAxisTests(),
            "и получен он обязан быть даром: описанные сферы не дотягиваются друг до друга");
    }

    /// <summary>Положительный контроль: без него «ноль» выше был бы зелёным просто потому,
    /// что счётчик не работает или отказ съел вообще всё.</summary>
    [Test]
    public void OverlappingBoxes_StillReachTheSeparatingAxisTest()
    {
        var a = Box(Vector3.zero, new Vector3(0.3f, 0.36f, 0.009f));
        var b = Box(new Vector3(0.1f, 0f, 0f), new Vector3(0.3f, 0.36f, 0.009f));

        Assert.Greater(BoxOverlap.PenetrationUnits(a, b), 0f,
            "коробки пересекаются — глубина обязана быть положительной");
        Assert.AreEqual(1, BoxOverlap.TakeSeparatingAxisTests(),
            "пересекающаяся пара обязана дойти до полного SAT ровно один раз");
    }

    /// <summary>Граница отказа: сферы описаны, значит любая пара, чьи коробки хотя бы
    /// касаются, обязана пройти отказ. Тонкая длинная коробка — худший случай для сферы,
    /// поэтому проверяется именно она.</summary>
    [Test]
    public void SpheresOfTouchingThinBoxes_AlwaysReach()
    {
        var a = Box(Vector3.zero, new Vector3(1.5f, 0.009f, 0.009f));
        var b = Box(new Vector3(2.9f, 0f, 0f), new Vector3(1.5f, 0.009f, 0.009f));

        Assert.IsTrue(BoxOverlap.SpheresReach(a, b),
            "коробки пересекаются по X — отказ не имеет права их разделить");
        Assert.Greater(BoxOverlap.PenetrationUnits(a, b), 0f,
            "и полный SAT обязан подтвердить пересечение — отказ по сферам его не подменяет");
    }
}

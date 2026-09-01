using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Правило из ТЗ: если сторона, с которой опора заходит в деталь, тоньше
/// 25 мм, опора обязана стоять по середине — иначе футорке не за что держаться.
///
/// Ловушка здесь в том, что «по середине» проверяется ПОСТОРОННЕЙ оси: у бока
/// 16x700x500 тонкая сторона — X, и смещение по Z (вдоль пятисотки) законно.
/// Асимметричный габарит здесь обязателен: на квадратной детали обе оси дают
/// один ответ, и тест был бы зелёным против кода, который путает их местами.</summary>
public class ScrewLegCentringTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    private ScrewLegElement Leg(Vector3 pos)
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", pos);
        _spawned.Add(go);
        return go.GetComponent<ScrewLegElement>();
    }

    /// <summary>Бок 16 мм, стоящий на торце: тонкая сторона — X, длинная — Z.</summary>
    private KitchenElement ThinSide() =>
        Board("Бок", new Vector3(0f, 0.400f, 0f), new Vector3Int(16, 700, 500));

    [Test]
    public void OnTheCentreOfAThinEnd_NothingIsReported()
    {
        var host = ThinSide();
        var leg = Leg(new Vector3(0f, 0.020f, 0f));

        Assert.IsFalse(ScrewLegCentring.TryFindOffCentre(leg, host, out _),
            "опора ровно по центру торца — крепление правильное, жаловаться не на что");
    }

    [Test]
    public void OffTheCentreAcrossTheThinSide_IsReportedWithTheOffset()
    {
        var host = ThinSide();
        var leg = Leg(new Vector3(4f * U, 0.020f, 0f));

        Assert.IsTrue(ScrewLegCentring.TryFindOffCentre(leg, host, out var offCentre),
            "четыре миллиметра от центра шестнадцатимиллиметрового торца — футорка вылезет");
        Assert.AreEqual(ScrewLegCentring.AxisX, offCentre.Axis, "жалоба обязана назвать ТОНКУЮ ось");
        Assert.AreEqual(16f, offCentre.SpanMM, 0.01f, "и размер той стороны, из-за которой жалуется");
        Assert.AreEqual(4f, offCentre.OffsetMM, 0.01f, "и само смещение — по нему ставят опору на место");
    }

    [Test]
    public void OffTheCentreAlongTheLongSide_IsNobodysBusiness()
    {
        var host = ThinSide();
        var leg = Leg(new Vector3(0f, 0.020f, 0.200f));

        Assert.IsFalse(ScrewLegCentring.TryFindOffCentre(leg, host, out _),
            "вдоль пятисотки опору можно ставить где угодно — правило про ТОНКУЮ сторону");
    }

    [Test]
    public void OnAThickBottomPanel_OffCentreIsFine()
    {
        var host = Board("Дно", new Vector3(0f, 0.150f, 0f), new Vector3Int(600, 18, 500));
        var leg = Leg(new Vector3(0.250f, 0.020f, 0.200f));

        Assert.IsFalse(ScrewLegCentring.TryFindOffCentre(leg, host, out _),
            "у дна обе стороны, через которые заходит резьба, больше 25 мм: "
            + "футорка держится, где её ни поставь");
    }

    [Test]
    public void AFractionOfAMillimetreOff_IsStillCentred()
    {
        var host = ThinSide();
        var leg = Leg(new Vector3(0.4f * U, 0.020f, 0f));

        Assert.IsFalse(ScrewLegCentring.TryFindOffCentre(leg, host, out _),
            "допуск на попадание — 0,5 мм, иначе предупреждение висело бы вечно "
            + "от одного округления сетки");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Оба слота декора носителя обязаны пережить круг «сохранение → загрузка».
///
/// Тест написан как страховка под переименование <c>ITabletop</c>: имена в C#
/// меняются, а имена полей в файле — <c>tabletopMaterialId</c> и
/// <c>legsMaterialId</c> — обязаны остаться прежними, иначе ранее сохранённый
/// проект откроется с серым декором и молча. Компилятор такую утечку не ловит:
/// поля сериализуются по ИМЕНИ, и переименованное поле просто не найдётся в
/// json, а <c>JsonUtility</c> подставит умолчание вместо ошибки.
///
/// Проверяется именно тот путь, которым ходит пользователь —
/// <c>CaptureScene → Serialize → Deserialize → RestoreScene</c>, — а не одна
/// только запись в <c>ElementData</c>: восстановление слотов живёт в
/// <c>ElementRestorers</c> отдельной функцией, и потерять её в списке типов
/// можно независимо от захвата.
///
/// Список носителей ВЫВЕДЕН из <c>EveryElementType.Makers</c> фильтром по
/// интерфейсу, а не выписан руками: типов сейчас одиннадцать и будет больше,
/// а список, выписанный в четвёртый раз, расходится молча (CONVENTIONS.md →
/// «A field list written out more than twice gets a parity test»). Поэтому
/// новый носитель попадает под этот круг сам.
///
/// Оба id — РАЗНЫЕ и оба не заводские: совпавший с умолчанием id не отличить
/// от потерянного, а одинаковые id не поймают перепутанные местами слоты.
/// </summary>
public class DecorSlotRoundTripTests
{
    private const string FirstSlotDecor = "test_decor_first_slot";
    private const string SecondSlotDecor = "test_decor_second_slot";

    [SetUp]
    public void SetUp()
    {
        EveryElementType.ClearScene();
        MaterialCatalog.Register(new MaterialDef(FirstSlotDecor, "Декор первого слота",
            "тест", new Color(0.21f, 0.43f, 0.65f)));
        MaterialCatalog.Register(new MaterialDef(SecondSlotDecor, "Декор второго слота",
            "тест", new Color(0.67f, 0.45f, 0.23f)));
    }

    [TearDown]
    public void TearDown()
    {
        EveryElementType.ClearScene();
        GroupManager.Clear();
        CommandStack.Clear();
        MaterialCatalog.Reset();
        MaterialManager.ClearCache();
    }

    private static List<Type> CarrierTypes() =>
        EveryElementType.Makers
            .Where(m => typeof(ITabletop).IsAssignableFrom(m.type))
            .Select(m => m.type)
            .ToList();

    private static IEnumerable<TestCaseData> Carriers() =>
        CarrierTypes().Select(t => new TestCaseData(t).SetName(
            "BothDecorSlots_SurviveSaveAndLoad_" + t.Name));

    [TestCaseSource(nameof(Carriers))]
    public void BothDecorSlots_OfACarrier_SurviveSaveAndLoad(Type type)
    {
        var element = EveryElementType.Spawn(type, "Носитель");
        var slots = (ITabletop)element;
        slots.TabletopMaterialId = FirstSlotDecor;
        slots.LegsMaterialId = SecondSlotDecor;

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { element }));
        EveryElementType.ClearScene();
        SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!);

        var restored = PartRegistry.GetAll().OfType<ITabletop>().ToList();
        Assert.AreEqual(1, restored.Count,
            type.Name + ": загрузка обязана вернуть ровно один носитель двух слотов; "
            + "ноль означает, что тип выпал из списка в ElementRestorers");
        Assert.AreEqual(FirstSlotDecor, restored[0].TabletopMaterialId,
            type.Name + ": первый слот декора обязан пережить круг. Красный здесь "
            + "означает, что переименование задело имя поля tabletopMaterialId в файле "
            + "сохранения — ранее сохранённые проекты откроются серыми");
        Assert.AreEqual(SecondSlotDecor, restored[0].LegsMaterialId,
            type.Name + ": и второй слот тоже, и он не поменялся местами с первым — "
            + "id здесь намеренно разные");
    }

    [Test]
    public void SaveFile_OfACarrier_KeepsBothDecorFieldNames()
    {
        var element = EveryElementType.Spawn(typeof(TableElement), "Стол");
        var slots = (ITabletop)element;
        slots.TabletopMaterialId = FirstSlotDecor;
        slots.LegsMaterialId = SecondSlotDecor;

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new[] { element }));

        StringAssert.Contains("\"tabletopMaterialId\":\"" + FirstSlotDecor + "\"", json,
            "имя поля в файле — внешний формат, а не имя в C#: переименование интерфейса "
            + "не имеет права его коснуться");
        StringAssert.Contains("\"legsMaterialId\":\"" + SecondSlotDecor + "\"", json,
            "то же самое для второго слота");
    }

    [Test]
    public void CarrierList_DerivedFromTheInterface_CoversBothExtremes()
    {
        var carriers = CarrierTypes();

        Assert.GreaterOrEqual(carriers.Count, 11,
            "носителей двух слотов сейчас одиннадцать; меньше означает, что фильтр по "
            + "интерфейсу перестал совпадать и круговые проверки выше молча опустели");
        CollectionAssert.Contains(carriers, typeof(TableElement),
            "стол — тот случай, где «столешница» всё ещё верна");
        CollectionAssert.Contains(carriers, typeof(SocketElement),
            "розетка — тот случай, где «столешница» нелепа; ради неё интерфейс и "
            + "переименовывается, и она обязана быть под кругом");
    }
}

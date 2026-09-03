using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Розетка и выключатель красят себя САМИ, и это не украшение.
///
/// Общий путь <c>MaterialManager.Apply</c> для элемента без своей ветки пишет
/// материал ПРЯМО В РЕНДЕРЕР, мимо элемента, и по умолчанию это серый ЛДСП —
/// декор мебели. Электроустановочное изделие белое пластиковое, и его умолчание
/// живёт в самом элементе. Попасть под серую покраску легче всего не руками
/// пользователя, а двумя штатными путями: дублирование зовёт
/// <c>ApplyById(el, source.MaterialId)</c>, а загрузка проекта применяет
/// materialId каждому элементу подряд. То есть серой розетка выходила бы у
/// КОПИИ и после открытия файла — и молча, потому что материал не участвует ни
/// в одном снимке размеров (ровно этот дефект уже ловил BathtubDecorTests).
///
/// Слотов здесь ДВА, и это главная причина, по которой оба устройства носят
/// <c>IHasTwoDecorSlots</c>: у розетки «Рамка» и «Контакты», у выключателя «Рамка» и
/// «Клавиша» — так же, как у дивана «Обивка/Подушки» и у кровати
/// «Каркас/Матрас». Контакты и клавиша обязаны иметь СВОЙ заводской вид, и
/// выбор декора для рамки не имеет права утащить их за собой: тёмные контакты —
/// это то, по чему розетка читается как розетка.
///
/// Свойство проверяется ДВУСТОРОННЕ, односторонний тест был бы хуже, чем
/// никакого: заводской вид обязан пережить Apply с умолчательным id, а
/// СОЗНАТЕЛЬНО выбранный декор обязан пережить и Apply, и перестройку меша.
/// Заглушив первое возвратом пластика всегда, получили бы розетку, которую
/// нельзя перекрасить.</summary>
public class WallDeviceDecorTests
{
    private const string DecorId = "test_wall_device_decor";

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        MaterialCatalog.Register(new MaterialDef(DecorId, "Тестовый декор", "плитка",
            new Color(0.2f, 0.4f, 0.6f)));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        MaterialCatalog.Reset();
    }

    private static SocketElement NewSocket()
    {
        var go = new GameObject("Розетка");
        var socket = go.AddComponent<SocketElement>();
        socket.PartName = "Розетка";
        socket.ApplyDimensions();
        return socket;
    }

    private static LightSwitchElement NewSwitch()
    {
        var go = new GameObject("Выключатель");
        var source = go.AddComponent<LightSwitchElement>();
        source.PartName = "Выключатель";
        source.ApplyDimensions();
        return source;
    }

    private static Material? PaintOfChild(KitchenElement element, string childName)
    {
        var child = element.transform.Find(childName);
        Assert.IsNotNull(child, $"деталь {childName} обязана существовать: без неё тест "
            + "проверял бы пустоту, а не материал");
        var renderer = child!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, $"деталь {childName} обязана иметь рендерер — иначе "
            + "материал некуда положить");
        return renderer!.sharedMaterial;
    }

    private static Material? FrameOf(KitchenElement device)
        => PaintOfChild(device, WallDeviceLayout.RimTopName + "0");

    private static Material? ContactOf(SocketElement socket)
        => PaintOfChild(socket, SocketLayout.PinHoleName + "0L");

    private static Material? KeyOf(LightSwitchElement source)
        => PaintOfChild(source, LightSwitchLayout.KeyName + "0");

    [Test]
    public void ANewSocket_IsWhitePlasticWithDarkContacts_NotTheFurnitureDefault()
    {
        var socket = NewSocket();

        Assert.AreSame(WallDeviceMaterials.Plastic, FrameOf(socket),
            "рамка рождается белой пластиковой: умолчание каталога — серый ЛДСП, "
            + "и это декор мебели, а не электроустановки");
        Assert.AreSame(WallDeviceMaterials.Contact, ContactOf(socket),
            "гнёзда и лапки тёмные: именно по тёмному колодцу розетка и читается "
            + "как розетка, а не как заглушка");
    }

    [Test]
    public void ANewSwitch_HasItsOwnKeyMaterial()
    {
        var source = NewSwitch();

        Assert.AreSame(WallDeviceMaterials.Plastic, FrameOf(source),
            "рамка выключателя из того же пластика, что и рамка розетки");
        Assert.AreSame(WallDeviceMaterials.Key, KeyOf(source),
            "клавиша — второй слот декора, и у неё свой заводской вид");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheFactoryLookAlone()
    {
        var socket = NewSocket();

        MaterialManager.ApplyById(socket, MaterialCatalog.DefaultId);

        Assert.AreSame(WallDeviceMaterials.Plastic, FrameOf(socket),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым». Через этот вызов ходят дублирование и загрузка проекта, поэтому "
            + "серой розетка выходила бы у КОПИИ и после открытия файла");
        Assert.AreSame(WallDeviceMaterials.Contact, ContactOf(socket),
            "второй слот тем же путём: у него своё умолчание, и оно тоже обязано выжить");
    }

    [Test]
    public void AChosenDecor_LandsOnTheFrameAndLeavesTheContactsDark()
    {
        var socket = NewSocket();

        MaterialManager.ApplyById(socket, DecorId);

        Assert.AreNotSame(WallDeviceMaterials.Plastic, FrameOf(socket),
            "выбранный декор обязан лечь на рамку: вето на умолчание не должно "
            + "превратиться в розетку, которую нельзя перекрасить");
        Assert.AreEqual(DecorId, socket.MaterialId, "выбор обязан ещё и сохраниться");
        Assert.AreSame(WallDeviceMaterials.Contact, ContactOf(socket),
            "декор рамки не имеет права утащить за собой контакты — это ВТОРОЙ слот, "
            + "и красится он отдельно");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var socket = NewSocket();
        MaterialManager.ApplyById(socket, DecorId);
        var chosen = FrameOf(socket);

        socket.PlateWidthMM = WallDeviceLayout.DefaultPlateWidthMM + 20;

        Assert.AreSame(chosen, FrameOf(socket),
            "перестройка решает про материал заново, и кэш «что я ставил в прошлый раз» "
            + "вернул бы сюда белый пластик поверх выбранного декора при первом же "
            + "изменении размера");
        Assert.AreSame(WallDeviceMaterials.Contact, ContactOf(socket),
            "второй слот переживает перестройку тем же порядком");
    }

    [Test]
    public void TheSwitchKeySlot_IsPaintedIndependentlyOfTheFrame()
    {
        var source = NewSwitch();

        source.LegsMaterialId = DecorId;

        Assert.AreNotSame(WallDeviceMaterials.Key, KeyOf(source),
            "клавишу можно перекрасить отдельно — иначе второй слот бесполезен");
        Assert.AreSame(WallDeviceMaterials.Plastic, FrameOf(source),
            "и рамка при этом остаётся заводской");
    }
}

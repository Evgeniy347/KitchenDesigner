using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Фабрика обязана вернуть ПОСТРОЕННЫЙ элемент, а не голый объект с
/// компонентом. Ловушка общая для всякого типа с ВЫЧИСЛЯЕМЫМ габаритом, и
/// смеситель со стойкой уже в неё попали — розетка и выключатель устроены так
/// же, поэтому вопрос задаётся и им.
///
/// Меш строится из ApplyDimensions. Каждая другая фабрика в проекте зовёт его
/// не глядя — присваиванием DimensionsMM, потому что размер приходит к ней
/// аргументом. Здесь размер выводится из формы, присваивать было нечего, и
/// построение оставалось на сеттерах свойств формы. А сеттер, получив то же
/// значение, что уже лежит в поле, выходит НЕ ТРОНУВ ничего: на умолчательной
/// спецификации не сработал бы ни один из четырёх, и элемент вышел бы без
/// деталей и без коллайдера — ни увидеть, ни выделить мышью. В приложении это
/// замаскировано (Awake зовёт ApplyDimensions сам), в EditMode Awake не
/// вызывается вовсе.
///
/// Почему это не ловят уже написанные тесты — самое полезное здесь. И сверка
/// паритета, и проверки дублирования заводят элемент НЕЗАВОДСКИМИ числами, и
/// правильно делают: значение, совпавшее с умолчанием, не отличить от
/// потерянного. Но ровно эти незаводские числа заставляют сеттер сработать,
/// элемент строится по дороге, и всё зелёное. Два вопроса требуют
/// ПРОТИВОПОЛОЖНЫХ входных данных: «донесли ли значение» — незаводскими,
/// «построился ли вообще» — только заводскими.
///
/// Копия проверяется отдельно: дублирование зовёт ту же фабрику со
/// спецификацией оригинала, поэтому копия умолчательного элемента шла бы тем
/// же мёртвым путём. Восстановление сцены зовёт её же.
///
/// В отличие от смесителя, эти два строят детали ДОЧЕРНИМИ объектами
/// (FurniturePartSet), а коллайдер получают коробкой по габариту — поэтому
/// спрашивается рендерер рамки среди детей, а не меш на корне.</summary>
public class WallDeviceBuildTests
{
    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static void AssertBuilt(GameObject go, Vector3Int expectedDims, string what)
    {
        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element, what + ": фабрика обязана вернуть объект с элементом");

        var frame = go.transform.Find(WallDeviceLayout.RimTopName + "0");
        Assert.IsNotNull(frame, what + ": нет перекладины рамки — ApplyDimensions не звали ни "
            + "разу, и элемент вышел из фабрики пустым объектом");

        var filter = frame!.GetComponent<MeshFilter>();
        Assert.IsNotNull(filter, what + ": деталь рамки без MeshFilter");
        Assert.IsNotNull(filter!.sharedMesh, what + ": MeshFilter без меша");
        Assert.Greater(filter.sharedMesh!.vertexCount, 0,
            what + ": меш пустой — протяжка профиля ничего не построила");

        Assert.IsNotNull(frame.GetComponent<MeshRenderer>(),
            what + ": нет MeshRenderer — элемент невидим, и красить его нечем: "
            + "MaterialManager и пипетка ищут рендерер именно здесь");

        var box = go.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, what + ": нет коллайдера — элемент нельзя выделить мышью, "
            + "и это хуже, чем невидимость: невидимое хотя бы заметно сразу");
        Assert.Greater(box!.size.sqrMagnitude, 0f,
            what + ": коллайдер нулевого размера мышью не поймать");

        Assert.AreEqual(expectedDims, element!.DimensionsMM,
            what + ": габарит вычисляется в перестройке, и умолчательный габарит вместо "
            + "вычисленного значит, что перестройки не было");
    }

    [Test]
    public void ANewSocket_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = WallDeviceSpec.Default;

        AssertBuilt(ElementFactory.CreateSocket(spec, "Розетка", Vector3.zero),
            spec.DimensionsMM, "розетка на умолчаниях");
    }

    [Test]
    public void ANewLightSwitch_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = WallDeviceSpec.Default;

        AssertBuilt(
            ElementFactory.CreateLightSwitch(spec, true, null, "Выключатель", Vector3.zero),
            spec.DimensionsMM, "выключатель на умолчаниях");
    }

    [Test]
    public void ADuplicatedSocket_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = WallDeviceSpec.Default;
        var source = ElementFactory.CreateSocket(spec, "Розетка", Vector3.zero)
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(source, "фабрика обязана вернуть объект с элементом");

        AssertBuilt(ElementFactory.Duplicate(source!), spec.DimensionsMM,
            "копия розетки на умолчаниях");
    }

    [Test]
    public void ADuplicatedLightSwitch_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = WallDeviceSpec.Default;
        var source = ElementFactory
            .CreateLightSwitch(spec, true, null, "Выключатель", Vector3.zero)
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(source, "фабрика обязана вернуть объект с элементом");

        AssertBuilt(ElementFactory.Duplicate(source!), spec.DimensionsMM,
            "копия выключателя на умолчаниях");
    }

    [Test]
    public void ADuplicatedSwitch_KeepsControllingTheSameLamps()
    {
        var lamp = ElementFactory.CreateLightSource("Люстра", Vector3.zero)
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(lamp, "источник света нужен, иначе имя нечему принадлежать");

        var source = ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, false,
            new[] { "Люстра" }, "Выключатель", Vector3.zero).GetComponent<ILightSwitch>();
        Assert.IsNotNull(source, "фабрика обязана вернуть объект с выключателем");

        var copy = ElementFactory.Duplicate((KitchenElement)source!).GetComponent<ILightSwitch>();

        Assert.IsNotNull(copy, "копия обязана остаться выключателем");
        Assert.AreEqual(new[] { "Люстра" }, copy!.LightNames,
            "связи живут в списке выключателя, и копия обязана унести их с собой — "
            + "иначе скопированный выключатель молча ничем не управляет");
        Assert.IsFalse(copy.IsOn,
            "состояние клавиши — тоже свойство копии, а не всегда «включено»");
    }
}

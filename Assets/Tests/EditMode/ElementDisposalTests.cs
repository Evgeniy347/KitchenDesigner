using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Кто возвращается в пул, а кого уничтожают. Раньше на это отвечала лестница
/// GetComponent&lt;XxxElement&gt; внутри ElementFactoryInstance.DestroyElement;
/// теперь способ уничтожения объявляет сам элемент
/// (<see cref="KitchenElement.Disposal"/>), а фабрика его только исполняет.
/// </summary>
public class ElementDisposalTests
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

    [Test]
    public void PlainBoard_GoesBackToThePartPool()
    {
        var board = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        var el = board.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        Assert.AreEqual(ElementDisposal.PartPool, el!.Disposal);

        ElementFactory.DestroyElement(board);
        Assert.IsTrue(board != null, "деталь уходит в пул, а не в небытие — её переиспользуют");
        Assert.IsFalse(board!.activeSelf);
    }

    [Test]
    public void Wall_IsStillPooled_ItIsAPlainPartWithAWallComponent()
    {
        var wall = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "W", Vector3.zero);
        var el = wall.GetComponent<KitchenElement>();
        Assert.AreEqual(ElementDisposal.PartPool, el.Disposal,
            "стена — обычный KitchenElement с довеском: пул умеет снять с неё компонент Wall");
        Object.DestroyImmediate(wall);
    }

    [Test]
    public void Facade_GoesBackToTheFacadePool_ButAnAssembledOneIsDestroyed()
    {
        var facade = ElementFactory.CreateFacade(new Vector3Int(400, 700, 18), "F", Vector3.zero);
        Assert.AreEqual(ElementDisposal.FacadePool, facade.GetComponent<FacadeElement>().Disposal);

        var assembled = ElementFactory.CreateAssembledFacade(
            new Vector3Int(400, 700, 18), "AF", Vector3.zero);
        Assert.AreEqual(ElementDisposal.Destroy,
            assembled.GetComponent<AssembledFacadeElement>().Disposal,
            "сборный фасад НЕ пулим: процедурный меш и дочерние объекты не переживают сброс пула, "
            + "а он — подкласс фасада, и без отдельного решения уехал бы в фасадный пул");

        ElementFactory.DestroyElement(assembled);
        Assert.IsTrue(assembled == null, "сборный фасад обязан быть уничтожен, а не спрятан в пул");
    }

    /// <summary>Пул раздаёт ПРИМИТИВНЫЙ КУБ с компонентом KitchenElement. Всё
    /// остальное — приборы, лампа, пол, ДВП — своей геометрией на этот куб не
    /// похоже: попади они в пул, следующая «доска» досталась бы с чужими детьми
    /// и чужим компонентом.</summary>
    [Test]
    public void Appliances_LightFloorAndPanel_AreDestroyed_NotReleasedIntoThePartPool()
    {
        var made = new List<GameObject>
        {
            ElementFactory.CreateCooktop("Hob", Vector3.zero),
            ElementFactory.CreateOven("Oven", Vector3.zero),
            ElementFactory.CreateDishwasher("Dw", Vector3.zero),
            ElementFactory.CreateLightSource("Lamp", Vector3.zero),
            ElementFactory.CreateFloor(new Vector3Int(3000, 100, 3000), "Floor", Vector3.zero),
            ElementFactory.Instance.CreatePanel(new Vector3Int(600, 400, 3), "Hdf", Vector3.zero),
        };

        foreach (var go in made)
        {
            var el = go.GetComponent<KitchenElement>();
            Assert.AreEqual(ElementDisposal.Destroy, el.Disposal,
                el.DisplayTypeName + " не должен попадать в пул деталей: пул раздаёт куб, "
                + "а этот элемент несёт свой компонент и своих детей");
        }

        foreach (var go in made) ElementFactory.DestroyElement(go);
        foreach (var go in made)
            Assert.IsTrue(go == null, "объект обязан быть уничтожен, а не спрятан в пул");
    }

    /// <summary>Проём снимаем ДО уничтожения — иначе столешница остаётся с дырой
    /// от несуществующей мойки (в PlayMode Destroy отложен до конца кадра, и
    /// OnDestroy мойки сюда опоздал бы).</summary>
    [Test]
    public void DestroyingASink_ClosesTheHoleInTheCountertopFirst()
    {
        var top = ElementFactory.CreatePart(new Vector3Int(1200, 650, 38), "Top", Vector3.zero);
        var topEl = top.GetComponent<KitchenElement>();
        topEl.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        topEl.DimensionsMM = new Vector3Int(1200, 650, 38);

        var sinkGo = ElementFactory.CreateSink("Sink", new Vector3(0f, 0.019f + 0.05f, 0f));
        var sink = sinkGo.GetComponent<SinkElement>();
        sink.SnapToPart();
        Assert.IsTrue(topEl.HasCutout(sink), "предусловие: мойка врезалась в столешницу");

        sink.PrepareForDestruction();

        Assert.IsFalse(topEl.HasCutout(sink),
            "перед уничтожением мойка обязана снять с детали свой проём");
    }

    /// <summary>Деталь из пула обязана вернуться ЧИСТОЙ: иначе следующая доска
    /// достаётся с чужими пазами и чужим проёмом под врезную технику.</summary>
    [Test]
    public void PooledBoard_ComesBackWithoutTheGroovesOfItsPreviousLife()
    {
        var first = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "First", Vector3.zero);
        var el = first.GetComponent<KitchenElement>();
        el.SetGrooves(new[] { new GrooveSpec(GrooveKind.Through, GrooveSide.Top) });
        el.GroupId = 7;
        Assert.AreEqual(1, el.Grooves.Count, "предусловие: паз есть");

        ElementFactory.DestroyElement(first);
        var second = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Second", Vector3.zero);
        var reused = second.GetComponent<KitchenElement>();

        Assert.AreEqual(0, reused.Grooves.Count,
            "деталь из пула не должна приносить пазы прошлой жизни");
        Assert.AreEqual(0, reused.GroupId, "и чужую группу тоже");
    }
}

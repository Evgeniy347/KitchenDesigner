using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Порог, ниже которого «препятствие перед фасадом» — не препятствие.
///
/// Здесь живёт причина, которая раньше была комментарием в FacadeValidator.cs:
/// у деталей, стоящих вплотную, проекции пересекаются на доли микрона (в трассах
/// встречались «коллизии» в 0.0002 мм), и без порога каждая такая пара
/// сообщалась пользователю как ошибка. Плюс исключение потомков фасада: стекло
/// и ручка стоят перед его лицевой гранью по определению.</summary>
public class FacadeValidatorNoiseTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private static List<KitchenElement> All() => PartRegistry.GetAll();

    [Test]
    public void NeighbourOverlappingByAHairsBreadth_IsNotAnObstruction()
    {
        var facade = MakeFacade("Фасад", new Vector3Int(400, 300, 18), Vector3.zero);
        float toU = AppConstants.MM_TO_UNITS;
        float sliverMM = FacadeValidator.MinOverlapMm * 0.5f;
        float sideOffset = (200f + 100f - sliverMM) * toU;

        MakeElement("Сосед", new Vector3Int(200, 200, 18),
            new Vector3(sideOffset, 0f, 0.038f));

        Assert.IsEmpty(FacadeValidator.FindFaceObstructions(facade, All()),
            $"перекрытие {sliverMM} мм — это числовой шум от деталей, стоящих вплотную, "
            + "а не деталь перед фасадом: без порога такую «ошибку» получала бы каждая "
            + "нормально собранная кухня");
    }

    [Test]
    public void TheSameNeighbourSlidBackByAMillimetre_IsAnObstruction()
    {
        var facade = MakeFacade("Фасад", new Vector3Int(400, 300, 18), Vector3.zero);
        float toU = AppConstants.MM_TO_UNITS;
        float overlapMM = FacadeValidator.MinOverlapMm + 1f;
        float sideOffset = (200f + 100f - overlapMM) * toU;

        MakeElement("Сосед", new Vector3Int(200, 200, 18),
            new Vector3(sideOffset, 0f, 0.038f));

        var obstructions = FacadeValidator.FindFaceObstructions(facade, All());

        Assert.AreEqual(1, obstructions.Count,
            "положительный контроль на ТОЙ ЖЕ геометрии: перекрытие чуть выше порога "
            + "обязано находиться — иначе предыдущий тест был бы зелен над пустотой");
        Assert.AreEqual(overlapMM, obstructions[0].overlapWidthMm, 0.05f);
    }

    [Test]
    public void ChildrenOfTheFacade_AreNeverItsOwnObstruction()
    {
        var facade = MakeFacade("Фасад", new Vector3Int(400, 300, 18), Vector3.zero);

        var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handleGo.name = "Ручка";
        _spawned.Add(handleGo);
        handleGo.transform.SetParent(facade.transform, worldPositionStays: false);
        var handle = handleGo.AddComponent<KitchenElement>();
        handle.PartName = "Ручка";
        handle.DimensionsMM = new Vector3Int(120, 20, 20);
        handleGo.transform.position = new Vector3(0f, 0f, 0.02f);
        PartRegistry.Register(handle);

        Assert.IsEmpty(FacadeValidator.FindFaceObstructions(facade, All()),
            "стекло, ручка и прочие дети фасада стоят перед его лицевой гранью по "
            + "определению — считать их препятствием значит ругаться на сам фасад");
    }
}

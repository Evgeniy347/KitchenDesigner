using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Общая база для EditMode-фикстур, которым нужен список заспавненных объектов и
/// типовая постройка детали или фасада. Сведено по пункту 2 docs/TODO.md: сюда
/// переехали ТОЛЬКО тела, совпадавшие в копиях буквально, строка в строку.
///
/// Имя метода называет СПОСОБ постройки, а не результат, и это не украшение.
/// Копии расходились именно способом, и разница видна только на падении:
/// «примитив» — куб с MeshFilter/MeshRenderer/BoxCollider, который видит луч;
/// «фабрика» — реальный путь приложения (ElementFactory) со всей его настройкой;
/// а третьим способом был голый GameObject вообще без этих компонентов. Одно имя
/// MakeFacade над тремя способами не сводило разницу, а прятало её — чем это
/// кончается, дословно записано в FacadeMcpTests над MakePrimitiveElement.
///
/// Размеры, позиция и поворот остаются АРГУМЕНТАМИ и потому на стороне фикстуры:
/// 600×716 у дверцы и 400×86 у фронта ящика — выбор конкретного теста, а не общее
/// знание, и он обязан остаться написанным в её собственном файле.
///
/// [SetUp]/[TearDown] здесь СОЗНАТЕЛЬНО нет. Набор сбрасываемой статики у фикстур
/// разный (PartRegistry, GroupManager, CommandStack в разных сочетаниях), и общий
/// Clear() либо расширил бы, либо сузил чистку конкретного теста — тесты потекли бы
/// друг в друга, см. agents/TEST-DESIGN.md про SnapIntegrationTests, падавший только
/// в полном прогоне ровно по этой причине. Наследник объявляет свой [TearDown] и сам
/// утилизирует <see cref="_spawned"/>.
/// </summary>
public abstract class ElementTestBase
{
    protected readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Деталь-примитив: куб с мешем, рендерером и коллайдером на заданной
    /// позиции. Регистрация в PartRegistry здесь не дублирует Awake, а исправляет
    /// его: Awake отработал при AddComponent, когда PartName был ещё пуст.</summary>
    protected KitchenElement MakePrimitiveElement(string name, Vector3Int dims, Vector3 pos)
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

    /// <summary>Фасад-примитив БЕЗ зазоров: GapLeft/Right/Top/Bottom остаются
    /// нулевыми. Фикстуре, которой зазоры нужны, они нужны как параметр теста —
    /// она ставит их у себя.</summary>
    protected FacadeElement MakePrimitiveFacade(string name, Vector3Int dims, Vector3 pos)
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

    /// <summary>Фасад через ElementFactory — тот же путь, которым его создаёт
    /// приложение; регистрирует он себя сам. Зазоры 2 мм со всех сторон: именно
    /// столько стояло во всех сведённых сюда копиях, и это рабочий зазор навески,
    /// а не «ничего конкретного».</summary>
    protected FacadeElement MakeFactoryFacade(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(dims, name, pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }
}

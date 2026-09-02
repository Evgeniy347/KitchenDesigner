using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Один экземпляр КАЖДОГО типа элемента, заведённый через настоящую фабрику.
///
/// Живёт отдельным классом, а не внутри одного набора, ровно по той же причине,
/// что и ElementTypeCatalog: список типов, выписанный руками в третий раз,
/// начинает расходиться молча. Здесь он один, а его согласие с объявлениями
/// классов в сборке ядра проверяет ElementTypeRegistryExerciseTests
/// (CONVENTIONS.md → «A field list written out more than twice gets a parity
/// test»).
///
/// Аргументы фабрики взяты НЕ заводские везде, где фабрика их принимает:
/// значение, совпавшее с умолчанием, не отличить от полностью потерянного.
/// </summary>
public static class EveryElementType
{
    public static readonly (Type type, Func<string, GameObject> make)[] Makers =
    {
        (typeof(KitchenElement), n => ElementFactory.CreatePart(new Vector3Int(823, 417, 19), n, Vector3.zero)),
        (typeof(FacadeElement), n => ElementFactory.CreateFacade(new Vector3Int(447, 713, 19), n, Vector3.zero)),
        (typeof(AssembledFacadeElement), n => ElementFactory.CreateAssembledFacade(new Vector3Int(451, 719, 19), n, Vector3.zero, AssembledFill.Glass)),
        (typeof(PanelElement), n => ElementFactory.Instance.CreatePanel(new Vector3Int(613, 409, 4), n, Vector3.zero)),
        (typeof(RadialShelfElement), n => ElementFactory.CreateRadialShelf(607, 411, 19, 137, n, Vector3.zero)),
        (typeof(DrawerElement), n => ElementFactory.CreateDrawer(DrawerType.C, 450, DrawerColor.White, 407, n, Vector3.zero)),
        (typeof(TableElement), n => ElementFactory.CreateTable(new Vector3Int(1207, 753, 703), n, Vector3.zero)),
        (typeof(RadiusTableElement), n => ElementFactory.CreateRadiusTable(new Vector3Int(1213, 757, 709), n, Vector3.zero)),
        (typeof(StoolElement), n => ElementFactory.CreateStool(new Vector3Int(363, 453, 367), 23, n, Vector3.zero)),
        (typeof(ChairElement), n => ElementFactory.CreateChair(new Vector3Int(453, 903, 457), 27, 463, n, Vector3.zero)),
        (typeof(SofaElement), n => ElementFactory.CreateSofa(new Vector3Int(1807, 803, 903), 31, 427, n, Vector3.zero)),
        (typeof(PouffeElement), n => ElementFactory.CreatePouffe(new Vector3Int(407, 423, 403), 37, 83, n, Vector3.zero)),
        (typeof(BedElement), n => ElementFactory.CreateBed(new Vector3Int(1607, 503, 2003), false, false, n, Vector3.zero)),
        (typeof(PillarElement), n => ElementFactory.CreatePillar(713, n, Vector3.zero, 87)),
        (typeof(ScrewLegElement), n => ElementFactory.CreateScrewLeg(n, Vector3.zero)),
        (typeof(FloorElement), n => ElementFactory.CreateFloor(new Vector3Int(3007, 23, 3011), n, Vector3.zero)),
        (typeof(LightSourceElement), n => ElementFactory.CreateLightSource(n, Vector3.zero)),
        (typeof(SinkElement), n => ElementFactory.CreateSink(n, Vector3.zero)),
        (typeof(CooktopElement), n => ElementFactory.CreateCooktop(n, Vector3.zero)),
        (typeof(OvenElement), n => ElementFactory.CreateOven(n, Vector3.zero)),
        (typeof(DishwasherElement), n => ElementFactory.CreateDishwasher(n, Vector3.zero)),
        (typeof(WindowElement), n => ElementFactory.CreateWindow(new Vector3Int(907, 1213, 103), n, Vector3.zero, GlassTint.Tinted, 63)),
        (typeof(DoorElement), n => ElementFactory.CreateDoor(new Vector3Int(903, 2007, 107), n, Vector3.zero, DoorSashType.Blind)),
    };

    /// <summary>Типы, выведенные из СБОРКИ, а не из списка выше: база тоже
    /// считается — «доска» это она и есть, и через реестры она ходит наравне
    /// со всеми.</summary>
    public static List<Type> Declared() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => typeof(KitchenElement).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    public static KitchenElement Spawn(Type type, string name)
    {
        var make = Makers.First(m => m.type == type).make;
        var go = make(name);
        Assert.IsNotNull(go, "фабрика вернула null для " + type.Name);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el, "фабрика обязана вернуть объект с KitchenElement: " + type.Name);
        Assert.AreEqual(type, el.GetType(),
            "фабричный вызов для " + type.Name + " собрал другой тип — список Makers разошёлся с фабрикой");
        return el;
    }

    public static void ClearScene()
    {
        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>())
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }
}

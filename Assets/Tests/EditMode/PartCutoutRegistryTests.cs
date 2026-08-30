using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Список врезной техники, который ведёт сама ДЕТАЛЬ. Хранится он за
/// интерфейсной ссылкой <see cref="IPartCutout"/>, и это ровно тот случай, где
/// подменённое сравнение Unity не работает: «уничтожен, но не null» видно только
/// у её собственных типов.
/// </summary>
public class PartCutoutRegistryTests
{
    private class GhostCutout : MonoBehaviour, IPartCutout
    {
        public string PartName => "Ghost";

        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part) => new GrooveMesh.Rect2
        {
            xMin = -0.2f, xMax = 0.2f, yMin = -0.2f, yMax = 0.2f,
        };

        public int HoleAxisIn(KitchenElement part) => 2;
    }

    private GameObject? _top;
    private GameObject? _ghost;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new[] { _top, _ghost })
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _top = null;
        _ghost = null;
    }

    private KitchenElement Countertop()
    {
        _top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var el = _top.AddComponent<KitchenElement>();
        el.PartName = "Top";
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(1200, 650, 38);
        PartRegistry.Register(el);
        return el;
    }

    private GhostCutout RegisteredCutout(KitchenElement top)
    {
        _ghost = new GameObject("GhostCutout");
        var cutout = _ghost.AddComponent<GhostCutout>();
        top.RegisterCutout(cutout);
        Assert.IsTrue(top.HasCutout(cutout), "предусловие: врезка зарегистрирована");
        return cutout;
    }

    [Test]
    public void DestroyedCutout_IsNoLongerReportedAsAttached()
    {
        var top = Countertop();
        var cutout = RegisteredCutout(top);

        Object.DestroyImmediate(_ghost);
        _ghost = null;

        Assert.IsFalse(top.HasCutout(cutout),
            "уничтоженный MonoBehaviour за интерфейсной ссылкой: обычное `== null` его не ловит, "
            + "и деталь считала бы себя врезанной в несуществующий прибор");
    }

    [Test]
    public void DestroyedCutout_LeavesNoHoleInThePartMesh()
    {
        var top = Countertop();
        RegisteredCutout(top);
        Assert.AreEqual(1, top.CutoutHoleRects().Count, "предусловие: проём есть");

        Object.DestroyImmediate(_ghost);
        _ghost = null;

        Assert.AreEqual(0, top.CutoutHoleRects().Count,
            "иначе меш детали остаётся с дырой от прибора, которого больше нет");
        Assert.AreEqual(2, top.CutoutHoleAxis,
            "без живых врезок ось проёма возвращается к каноническому Z");
    }

    [Test]
    public void RegisterCutout_OnAPartThatCannotBeGrooved_IsIgnored()
    {
        var top = Countertop();
        var facadeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var facade = facadeGo.AddComponent<FacadeElement>();
        facade.PartName = "Facade";

        _ghost = new GameObject("GhostCutout");
        var cutout = _ghost.AddComponent<GhostCutout>();
        facade.RegisterCutout(cutout);

        Assert.IsFalse(facade.HasCutout(cutout),
            "врезка живёт только в базовой «Детали»: у фасада своя процедурная геометрия");
        Assert.IsFalse(top.HasCutout(cutout));
        Object.DestroyImmediate(facadeGo);
    }
}

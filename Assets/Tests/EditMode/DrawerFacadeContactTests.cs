using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DrawerFacadeContactTests : ElementTestBase
{
    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private DrawerElement MakeDrawer(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, pos);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos, int width = 400, int height = 86, int depth = 18) =>
        MakeFactoryFacade(name, new Vector3Int(width, height, depth), pos);

    // ── Фасад в контакте с фронтом ящика ────────────────────────────────
    // Ящик Type A: 400×86×350 мм. Фронтальная грань (+Z) в z=0.175.
    // Фасад 400×86×18 мм: задняя грань (−Z) совпадает с фронтом ящика при
    // z = 0.184 (0.175 полуглубина ящика + 0.009 полуглубина фасада).

    [Test]
    public void IsFacadeInContact_FacadeTouchingFront_ReturnsTrue()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f));
        Assert.IsTrue(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_FacadeFarAway_ReturnsFalse()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.3f));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_FacadeWithinGap_ReturnsTrue()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        // Зазор 0.4 мм (< 0.5 мм ContactDistMM) — всё ещё контакт
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f + 0.0004f));
        Assert.IsTrue(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_FacadeBeyondThreshold_ReturnsFalse()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        // Зазор 0.6 мм (> 0.5 мм) — не контакт
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f + 0.0006f));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_FacadePerpendicular_ReturnsFalse()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f));
        // Поворот на 90° вокруг Y — грани больше не параллельны
        facade.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_FacadeOffset_NoOverlap_ReturnsFalse()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        // Фасад смещён по X на 1 м — не перекрывается с фронтом ящика в плоскости
        var facade = MakeFacade("Фасад", new Vector3(1f, 0.043f, 0.184f));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    [Test]
    public void IsFacadeInContact_PartialOverlap_ReturnsTrue()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        // Фасад 200×43×18 мм — половина от фронта, но перекрытие > 50% по каждой оси (100%/50%)
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f), 200, 43, 18);
        Assert.IsTrue(DrawerLinks.IsFacadeInContact(drawer, facade));
    }

    // ── Контакт с открытым ящиком ───────────────────────────────────────
    // IsFacadeInContact использует ClosedPosition/ClosedRotation,
    // игнорируя анимационное смещение transform.position.

    [Test]
    public void IsFacadeInContact_OpenDrawer_UsesClosedPosition()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f));

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);
        Assert.IsTrue(drawer.IsOpen, "ящик открыт (transform.position смещён)");
        // transform.position уже уехал вперёд, но ClosedPosition осталась на месте
        Assert.IsTrue(DrawerLinks.IsFacadeInContact(drawer, facade),
            "фасад в контакте с ЗАКРЫТОЙ позицией ящика");
    }

    [Test]
    public void IsFacadeInContact_AfterCheck_DrawerRestored()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var origPos = drawer.transform.position;
        var origRot = drawer.transform.rotation;
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f));

        DrawerLinks.IsFacadeInContact(drawer, facade);

        Assert.AreEqual(origPos, drawer.transform.position, "позиция восстановлена");
        Assert.AreEqual(origRot, drawer.transform.rotation, "поворот восстановлен");
    }

    // ── Граничные случаи ────────────────────────────────────────────────

    [Test]
    public void IsFacadeInContact_NullArguments_ReturnsFalse()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, 0.184f));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(null!, facade));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(drawer, null!));
        Assert.IsFalse(DrawerLinks.IsFacadeInContact(null!, null!));
    }

    [Test]
    public void IsFacadeInContact_FacadeTouchingBack_ReturnsTrue()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0f, 0.043f, 0f));
        // Фасад сзади: задняя грань ящика (−Z) на z=−0.175,
        // фронт фасада (+Z) должен быть там же → фасад на z=−0.184
        var facade = MakeFacade("Фасад", new Vector3(0f, 0.043f, -0.184f));
        Assert.IsTrue(DrawerLinks.IsFacadeInContact(drawer, facade));
    }
}

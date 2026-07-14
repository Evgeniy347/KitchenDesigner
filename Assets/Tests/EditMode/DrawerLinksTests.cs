using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Связи ящика (пара, фасад) держатся на именах — тесты стерегут их целостность:
/// создание пары, переименование с обновлением обратных ссылок, синхронизацию
/// фасада с анимацией ящика и очистку связей при дублировании.
/// </summary>
public class DrawerLinksTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    private FacadeElement MakeFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), name, pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    // ── CreatePair ──────────────────────────────────────────────────────

    [Test]
    public void CreatePair_LinksBothWays_AndStacksAbove()
    {
        var lower = MakeDrawer("D1", new Vector3(0f, 0.043f, 0f));

        var upper = DrawerLinks.CreatePair(lower);
        Assert.IsNotNull(upper);
        _spawned.Add(upper!.gameObject);

        Assert.IsTrue(lower.IsDouble, "источник помечен двойным");
        Assert.IsTrue(upper.IsDouble, "пара помечена двойной");
        Assert.IsTrue(upper.IsUpperDrawer, "пара — верхний ящик");
        Assert.IsFalse(lower.IsUpperDrawer, "источник — нижний ящик");
        Assert.AreEqual(DrawerConstants.UPPER_DRAWER_TYPE, upper.Type, "верхний — внутренний тип A");
        Assert.IsFalse(upper.Movable, "верхний двигается только с нижним");
        Assert.AreEqual(upper.PartName, lower.PairedDrawerName, "прямая ссылка");
        Assert.AreEqual(lower.PartName, upper.PairedDrawerName, "обратная ссылка");

        // Контуры проёмов друг над другом: шаг = полусумма высот контуров.
        float step = (DrawerConstants.GetMinOpeningHeight(lower.Type)
                    + DrawerConstants.GetMinOpeningHeight(upper.Type)) * 0.5f * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(lower.transform.position.y + step, upper.transform.position.y, 0.0001f,
            "пара стоит вплотную сверху");
    }

    [Test]
    public void CreatePair_LowerTypeD_UpperStillTypeA()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.D, 500, DrawerColor.Black, 450, "Big", Vector3.zero);
        _spawned.Add(go);
        var lower = go.GetComponent<DrawerElement>();

        var upper = DrawerLinks.CreatePair(lower);
        Assert.IsNotNull(upper);
        _spawned.Add(upper!.gameObject);

        Assert.AreEqual(DrawerType.A, upper.Type, "верхний всегда низкий (A)");
        Assert.AreEqual(450, upper.InternalWidth, "ширина наследуется");
        Assert.AreEqual(DrawerColor.Black, upper.Color, "цвет наследуется");
        Assert.AreEqual(500, upper.NominalLength, "длина по умолчанию — как у нижнего");
    }

    [Test]
    public void CreatePair_WhenPairExists_ReturnsNull()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        Assert.IsNotNull(upper);
        _spawned.Add(upper!.gameObject);

        int countBefore = PartRegistry.GetAll().Count;
        Assert.IsNull(DrawerLinks.CreatePair(lower), "повторное создание пары — no-op");
        Assert.IsNull(DrawerLinks.CreatePair(upper), "создание пары от пары — no-op");
        Assert.AreEqual(countBefore, PartRegistry.GetAll().Count, "новых элементов не появилось");
    }

    [Test]
    public void CreatePair_FromUpperDrawer_ReturnsNull()
    {
        // Пара создаётся только от нижнего ящика — верхний сам «ведомый».
        var source = MakeDrawer("Top", new Vector3(0f, 0.5f, 0f));
        source.IsUpperDrawer = true;

        Assert.IsNull(DrawerLinks.CreatePair(source));
    }

    // ── DetachPair / жёсткая привязка верхнего ──────────────────────────

    [Test]
    public void DetachPair_ClearsLinksAndReturnsUpper()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);

        var upperGo = DrawerLinks.DetachPair(lower);

        Assert.AreEqual(upper.gameObject, upperGo, "возвращён верхний ящик");
        Assert.IsFalse(lower.IsDouble, "нижний больше не двойной");
        Assert.IsEmpty(lower.PairedDrawerName, "ссылка нижнего очищена");
        Assert.IsEmpty(upper.PairedDrawerName, "ссылка верхнего очищена");
    }

    [Test]
    public void SyncToLower_FollowsLowerPosition()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);

        lower.transform.position = new Vector3(1f, 0.2f, -0.5f);
        upper.SyncToLower();

        float step = (DrawerConstants.GetMinOpeningHeight(lower.Type)
                    + DrawerConstants.GetMinOpeningHeight(upper.Type)) * 0.5f * AppConstants.MM_TO_UNITS;
        var expected = lower.transform.position + Vector3.up * step;
        Assert.AreEqual(expected.x, upper.transform.position.x, 1e-4f);
        Assert.AreEqual(expected.y, upper.transform.position.y, 1e-4f);
        Assert.AreEqual(expected.z, upper.transform.position.z, 1e-4f);
    }

    [Test]
    public void LowerColorAndWidth_PropagateToUpper()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);

        lower.Color = DrawerColor.White;
        lower.InternalWidth = 550;

        Assert.AreEqual(DrawerColor.White, upper.Color, "цвет верхнего следует за нижним");
        Assert.AreEqual(550, upper.InternalWidth, "ширина верхнего следует за нижним");
    }

    [Test]
    public void CycleDoubleState_WithoutPair_BehavesSafely()
    {
        // Битые данные: флаг двойного есть, пары нет — цикл не должен падать.
        var d = MakeDrawer("Одинокий", Vector3.zero);
        d.IsDouble = true;
        d.PairedDrawerName = "Несуществующий";

        Assert.DoesNotThrow(() =>
        {
            d.CycleDoubleState(); // Closed → BothOpen: нижний открывается
            d.StepAnimation(1f);
        });
        Assert.IsTrue(d.IsOpen, "нижний без пары открывается сам");
    }

    // ── Rename ──────────────────────────────────────────────────────────

    [Test]
    public void Rename_Drawer_UpdatesPairBackReference()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);

        DrawerLinks.Rename(lower, "Нижний короб");

        Assert.AreEqual("Нижний короб", lower.PartName);
        Assert.AreEqual("Нижний короб", upper.PairedDrawerName, "ссылка пары обновлена");
    }

    [Test]
    public void Rename_Facade_UpdatesAttachedFacadeName()
    {
        var drawer = MakeDrawer("D1", Vector3.zero);
        var facade = MakeFacade("F1", new Vector3(0f, 0f, 0.2f));
        drawer.AttachedFacadeName = "F1";

        DrawerLinks.Rename(facade, "Фронт");

        Assert.AreEqual("Фронт", facade.PartName);
        Assert.AreEqual("Фронт", drawer.AttachedFacadeName, "ссылка ящика на фасад обновлена");
    }

    [Test]
    public void UniqueName_AddsSuffixOnCollision()
    {
        MakeDrawer("Ящик GTV", Vector3.zero);
        Assert.AreEqual("Ящик GTV 2", DrawerLinks.UniqueName("Ящик GTV"));
        Assert.AreEqual("Свободное имя", DrawerLinks.UniqueName("Свободное имя"));
    }

    // ── Фасад следует за ящиком ─────────────────────────────────────────

    [Test]
    public void SetOpen_SyncsAttachedFacade()
    {
        var drawer = MakeDrawer("D1", Vector3.zero);
        var facade = MakeFacade("F1", new Vector3(0f, 0f, 0.2f));
        facade.Mode = DoorMode.DrawerOut;
        drawer.AttachedFacadeName = "F1";

        drawer.SetOpen(true);
        Assert.IsTrue(facade.IsOpen, "фасад открывается вместе с ящиком");

        drawer.SetOpen(false);
        Assert.IsFalse(facade.IsOpen, "фасад закрывается вместе с ящиком");
    }

    [Test]
    public void DoubleState_SyncsFacadesOfBothDrawers()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);

        var lowerFacade = MakeFacade("FL", new Vector3(0f, 0f, 0.2f));
        var upperFacade = MakeFacade("FU", new Vector3(0f, 0.086f, 0.2f));
        lower.AttachedFacadeName = "FL";
        upper.AttachedFacadeName = "FU";

        lower.DoubleState = DoubleDrawerState.BothOpen;
        Assert.IsTrue(lowerFacade.IsOpen, "нижний фасад открыт (BothOpen)");
        Assert.IsTrue(upperFacade.IsOpen, "верхний фасад открыт (BothOpen)");

        lower.DoubleState = DoubleDrawerState.LowerOnly;
        Assert.IsTrue(lowerFacade.IsOpen, "нижний фасад открыт (LowerOnly)");
        Assert.IsFalse(upperFacade.IsOpen, "верхний фасад закрыт (LowerOnly)");

        lower.DoubleState = DoubleDrawerState.Closed;
        Assert.IsFalse(lowerFacade.IsOpen, "нижний фасад закрыт (Closed)");
        Assert.IsFalse(upperFacade.IsOpen, "верхний фасад закрыт (Closed)");
    }

    [Test]
    public void ForceClose_ClosesAttachedFacade()
    {
        var drawer = MakeDrawer("D1", Vector3.zero);
        var facade = MakeFacade("F1", new Vector3(0f, 0f, 0.2f));
        facade.Mode = DoorMode.DrawerOut;
        drawer.AttachedFacadeName = "F1";

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);
        facade.StepDoor(1f);

        drawer.ForceClose();
        Assert.IsFalse(drawer.IsOpen);
        Assert.IsFalse(facade.IsOpen, "ForceClose ящика мгновенно закрывает фасад");
        Assert.AreEqual(0f, facade.DoorProgress, 0.0001f, "фасад вернулся в закрытую позу");
    }

    // ── Duplicate ───────────────────────────────────────────────────────

    [Test]
    public void Duplicate_ClearsNameLinks()
    {
        var lower = MakeDrawer("D1", Vector3.zero);
        var upper = DrawerLinks.CreatePair(lower);
        _spawned.Add(upper!.gameObject);
        lower.AttachedFacadeName = "F1";

        var copyGo = ElementFactory.Duplicate(lower);
        _spawned.Add(copyGo);
        var copy = copyGo.GetComponent<DrawerElement>();

        Assert.IsNotNull(copy);
        Assert.IsTrue(copy.IsDouble, "флаг двойного сохраняется");
        Assert.IsEmpty(copy.PairedDrawerName, "копия не крадёт чужую пару");
        Assert.IsEmpty(copy.AttachedFacadeName, "копия не крадёт чужой фасад");
    }
}

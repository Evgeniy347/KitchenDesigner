using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Кинематика открывания фасада: что он считает своей ЗАКРЫТОЙ позой и кого не
/// считает препятствием. Всё это раньше стояло комментариями внутри
/// <c>FacadeElement.SetOpen</c>, <c>ForceClose</c> и <c>StepDoor</c>.
/// </summary>
public class FacadeOpenKinematicsTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private FacadeElement Facade(string name, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        go.transform.position = position;
        var facade = go.AddComponent<FacadeElement>();
        facade.PartName = name;
        facade.DimensionsMM = new Vector3Int(400, 700, 18);
        PartRegistry.Register(facade);
        return facade;
    }

    private DrawerElement Drawer(string name, Vector3 position, string attachedFacade)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var drawer = go.AddComponent<DrawerElement>();
        drawer.PartName = name;
        drawer.NominalLength = 500;
        drawer.InternalWidth = 900;
        drawer.AttachedFacadeName = attachedFacade;
        go.transform.position = position;
        PartRegistry.Register(drawer);
        return drawer;
    }

    private static void RunAnimation(FacadeElement facade, int frames = 40)
    {
        for (int i = 0; i < frames; i++) facade.StepDoor(0.05f);
    }

    /// <summary>Пользователь может перетащить закрытый фасад ручками. Следующее
    /// открывание обязано крутить его от НОВОЙ позы — иначе дверца улетает
    /// туда, где стояла в прошлый раз.</summary>
    [Test]
    public void Open_AfterTheFacadeWasDragged_RotatesAboutItsNewClosedPose()
    {
        var facade = Facade("F", Vector3.zero);
        facade.SetOpen(true);
        RunAnimation(facade);
        facade.SetOpen(false);
        RunAnimation(facade);

        var moved = new Vector3(1.3f, 0.2f, -0.4f);
        facade.transform.position = moved;
        Assert.AreEqual(moved, facade.ClosedPosition,
            "закрытая дверца: её трансформ и есть закрытая поза");

        facade.SetOpen(true);
        RunAnimation(facade);

        Assert.AreEqual(moved, facade.ClosedPosition,
            "открывание должно было захватить НОВУЮ позу, а не вернуться к старой");

        facade.SetOpen(false);
        RunAnimation(facade);
        Assert.Less((facade.transform.position - moved).magnitude, 1e-3f,
            "закрывшись, фасад возвращается туда, откуда его открыли");
    }

    /// <summary>Закрытая поза — ИСТОЧНИК ИСТИНЫ: у открытой дверцы она заморожена
    /// и за трансформом не идёт, иначе после перезагрузки дверца «уезжает».</summary>
    [Test]
    public void OpenFacade_KeepsItsClosedPose_EvenIfTheTransformIsWrittenTo()
    {
        var facade = Facade("F", Vector3.zero);
        facade.SetOpen(true);
        RunAnimation(facade);
        Assert.IsFalse(facade.IsDoorClosed, "предусловие: дверца открыта");

        var frozen = facade.ClosedPosition;
        facade.transform.position += new Vector3(5f, 0f, 0f);

        Assert.AreEqual(frozen, facade.ClosedPosition,
            "поза открытой дверцы заморожена: в проект пишется она, а не смещённый трансформ");
        Assert.IsFalse(facade.PoseFollowsTransform,
            "поэтому открытую дверцу нельзя двигать и растягивать, пока она не закрыта");
    }

    private KitchenElement BlockerInTheSwingPath(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(2000, 700, 200);
        el.transform.position = new Vector3(0f, 0f, 0.35f);
        PartRegistry.Register(el);
        return el;
    }

    /// <summary>Положительный контроль для двух тестов ниже: в этой же точке
    /// ЧУЖАЯ деталь дверцу действительно останавливает. Без него «не мешает»
    /// было бы зелёным просто потому, что там нечему мешать.</summary>
    [Test]
    public void Open_StopsAtAForeignObstacleInTheSwingPath()
    {
        var facade = Facade("F", Vector3.zero);
        BlockerInTheSwingPath("Stranger");

        facade.SetOpen(true);
        RunAnimation(facade);

        Assert.Less(facade.DoorProgress, 1f,
            "гашение о препятствие обязано работать — иначе два теста ниже ничего не проверяют");
    }

    /// <summary>Прикреплённые детали (дно, стенки нестандартного ящика) едут
    /// вместе с фасадом — иначе он упирался бы в собственный короб и вставал на
    /// первом же миллиметре.</summary>
    [Test]
    public void Open_DoesNotTreatItsOwnAttachedPartsAsObstacles()
    {
        var facade = Facade("F", Vector3.zero);
        var box = BlockerInTheSwingPath("Bottom");
        box.AttachedToName = "F";

        facade.SetOpen(true);
        RunAnimation(facade);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-3f,
            "собственный короб не может быть помехой: он едет вместе с фасадом");
    }

    /// <summary>Второй положительный контроль: ЧУЖОЙ ящик в той же точке дверцу
    /// останавливает.</summary>
    [Test]
    public void Open_StopsAtAForeignDrawerInTheSwingPath()
    {
        var facade = Facade("F", Vector3.zero);
        Drawer("Stranger", new Vector3(0f, 0f, 0.35f), attachedFacade: "");

        facade.SetOpen(true);
        RunAnimation(facade);

        Assert.Less(facade.DoorProgress, 1f,
            "чужой ящик на пути — обычное препятствие");
    }

    /// <summary>Пристёгнутый ящик — не препятствие для СВОЕГО фасада: иначе фасад
    /// видит уже открытый ящик как помеху и блокирует сам себя.</summary>
    [Test]
    public void Open_DoesNotTreatItsOwnDrawerAsAnObstacle()
    {
        var facade = Facade("F", Vector3.zero);
        Drawer("D", new Vector3(0f, 0f, 0.35f), attachedFacade: "F");

        facade.SetOpen(true);
        RunAnimation(facade);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-3f,
            "ящик, к которому фасад пристёгнут, не должен останавливать его анимацию");
    }

    /// <summary>Закрывая фасад, закрываем и его ящик — иначе состояния расходятся
    /// и сцена оказывается «не то открыта, не то закрыта».</summary>
    [Test]
    public void ForceClose_AlsoClosesTheDrawerItIsAttachedTo()
    {
        var facade = Facade("F", Vector3.zero);
        var drawer = Drawer("D", new Vector3(0f, 0f, -0.2f), attachedFacade: "F");

        facade.SetOpen(true);
        drawer.SetOpen(true);
        RunAnimation(facade);
        Assert.IsTrue(drawer.IsOpen, "предусловие: ящик открыт");

        facade.ForceClose();

        Assert.IsFalse(facade.IsOpen);
        Assert.IsFalse(drawer.IsOpen,
            "ящик и его фасад — одна дверца: их состояния не имеют права разойтись");
    }

    /// <summary>Фасад-пассажир не анимируется сам: трансформом владеет хост
    /// (посудомойка), и ForceClose для пассажира — no-op.</summary>
    [Test]
    public void Passenger_DoesNotAnimateItself_AndIgnoresForceClose()
    {
        var facade = Facade("F", Vector3.zero);
        facade.CaptureClosedPose();
        facade.IsPassenger = true;

        facade.SetOpen(true);
        var before = facade.transform.position;
        RunAnimation(facade);

        Assert.AreEqual(before, facade.transform.position,
            "у пассажира своя анимация выключена — позу ему ставит хост");
        Assert.IsTrue(facade.IsOpen, "но целевое состояние он всё равно знает: по нему рисуется кнопка");

        facade.ForceClose();
        Assert.AreEqual(before, facade.transform.position,
            "ForceClose пассажира не двигает: хост вернёт его сам, приведя свою дверцу");
    }
}

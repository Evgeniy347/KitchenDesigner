using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ящик: что ведёт нижняя коробка пары, кто отвечает за позу выдвинутого
/// ящика и что происходит с пристёгнутым фасадом.
///
/// Здесь живут причины, которые раньше были комментариями в DrawerElement.cs:
/// закрытая поза — источник истины (иначе после загрузки открытый ящик улетает
/// от нуля), выдвинутый ящик за трансформом не идёт, ящик анимирует свой фасад
/// сам и потому снимает с него режим пассажира.</summary>
public class DrawerElementContractTests : ElementTestBase
{
    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private DrawerElement MakeDrawer(string name, Vector3 pos = default)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, pos);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos) =>
        MakeFactoryFacade(name, new Vector3Int(400, 86, 18), pos);

    private (DrawerElement lower, DrawerElement upper) MakePair()
    {
        var lower = MakeDrawer("Нижний");
        var upperGo = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400,
            "Верхний", new Vector3(0f, 0.1f, 0f));
        _spawned.Add(upperGo);
        var upper = upperGo.GetComponent<DrawerElement>();
        lower.IsDouble = true;
        upper.IsDouble = true;
        upper.IsUpperDrawer = true;
        lower.PairedDrawerName = upper.PartName;
        upper.PairedDrawerName = lower.PartName;
        return (lower, upper);
    }

    // --- Пара ведётся от нижней коробки ---

    [Test]
    public void DrawerSystem_SetOnTheLowerDrawer_ReachesTheUpperOne()
    {
        var (lower, upper) = MakePair();
        Assert.AreEqual(DrawerSystem.Gtv, upper.System, "обе коробки начинают с GTV");

        lower.System = DrawerSystem.Movento;

        Assert.AreEqual(DrawerSystem.Movento, upper.System,
            "обе коробки двойного ящика — одна система выдвижения: у пары не бывает "
            + "покупного металлического короба снизу и деревянного сверху");
    }

    [Test]
    public void DrawerSystem_SetOnTheUpperDrawer_DoesNotDragTheLowerOneWithIt()
    {
        var (lower, upper) = MakePair();

        upper.System = DrawerSystem.Movento;

        Assert.AreEqual(DrawerSystem.Gtv, lower.System,
            "ведёт нижняя коробка — как с цветом и шириной: иначе правка верхней "
            + "перебивала бы настройку, которую пользователь задал на нижней");
    }

    // --- Закрытая поза как источник истины ---

    [Test]
    public void DrawerOpenedAfterLoad_SlidesFromWhereItStands_NotFromTheOrigin()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(1.5f, 0.4f, -0.7f));
        var standsAt = drawer.transform.position;

        drawer.SetOpen(true);
        drawer.StepAnimation(0.01f);

        Assert.Less(Vector3.Distance(drawer.transform.position, standsAt), 0.05f,
            "закрытая поза захватывается ДО того, как трансформ уедет: без этого "
            + "загруженный открытым ящик анимировался бы от Vector3.zero и улетал");
        Assert.AreEqual(standsAt, drawer.ClosedPosition,
            "и именно эта поза уходит в проект, а не смещённый трансформ");
    }

    [Test]
    public void OpenDrawer_IsNotTransformable_AndKeepsItsClosedGeometry()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0.5f, 0.3f, 0f));
        var closedCentre = drawer.GetFaces()[0].center;

        drawer.SetOpen(true);
        for (int i = 0; i < 40; i++) drawer.StepAnimation(0.1f);

        Assert.IsFalse(drawer.PoseFollowsTransform, "выдвинутый ящик за трансформом не идёт");
        Assert.IsFalse(drawer.Transformable, "и потому не двигается и не растягивается");
        Assert.AreEqual(closedCentre, drawer.GetFaces()[0].center,
            "геометрия считается от закрытой позы: выдвинутый ящик не должен «пересекать» "
            + "свой же корпус в статической проверке");
        Assert.AreEqual(closedCentre, drawer.GetFacesAt(new Vector3(9f, 9f, 9f))[0].center,
            "примерка в другую позицию выдвинутый ящик тоже не двигает");
    }

    [Test]
    public void ClosedDrawer_FollowsItsTransform_LikeAnyOtherPart()
    {
        var drawer = MakeDrawer("Ящик", new Vector3(0.5f, 0.3f, 0f));

        Assert.IsTrue(drawer.PoseFollowsTransform,
            "положительный контроль: задвинутый ящик — обычная подвижная деталь");
        Assert.IsTrue(drawer.Transformable);
        Assert.AreEqual(new Vector3(9f, 9f, 9f).x,
            drawer.GetFacesAt(new Vector3(9f, 9f, 9f))[0].center.x - 0.2f, 1e-3f,
            "и примерка в другую позицию его геометрию туда переносит");
    }

    // --- Пристёгнутый фасад ---

    [Test]
    public void AttachingAFacadeToADrawer_TakesThePassengerFlagOffThePreviousFacade()
    {
        var drawer = MakeDrawer("Ящик");
        var oldFacade = MakeFacade("Старый", new Vector3(0f, 0f, 0.184f));
        var newFacade = MakeFacade("Новый", new Vector3(1f, 0f, 0.184f));
        oldFacade.IsPassenger = true;

        drawer.OnAttachedFacadeChanged(oldFacade, newFacade);

        Assert.IsFalse(oldFacade.IsPassenger,
            "ящик анимирует фасад той же кинематикой, что и себя, — режим пассажира "
            + "остался бы от прежней навески на посудомойке и заморозил бы фасад");
        Assert.IsFalse(newFacade.IsPassenger, "новый фасад пассажиром и не был");
    }
}

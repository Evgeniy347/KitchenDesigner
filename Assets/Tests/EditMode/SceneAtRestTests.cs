using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Сторож ПОКОЯ сцены. Пользователь: «fps 2-3 кадра, если оставить открытым
/// фасад» — при том что анимация давно кончилась и в сцене не двигается ничего. Профиль
/// (test-results/perf/perf_20260911_183920.csv) показал цикл: `SceneAnalyzer.Analyze`
/// вызывался кадрами 3000, 3017, 3033, 3050, 3067, 3084, 3101 — ровно каждые 17 кадров,
/// то есть раз в 0,28 с, непрерывно, и в каждом таком кадре `SceneChangeTracker.Poll`
/// стоил 3-4 мс вместо сотых долей, то есть находил сцену ИЗМЕНЁННОЙ.
///
/// Замкнутый круг: сам `Analyze` двигал детали, чтобы их померить. `DrawerLinks`
/// ставил ящик (и фасад-пассажира) в закрытую позу, спрашивал про контакт и возвращал
/// назад. Поза в итоге та же, но запись в `transform` поднимает `hasChanged`, `Poll`
/// объявляет сцену изменённой, `SceneRevision` растёт, заслонка `SceneSettleThrottle`
/// перезаводится и через 0,25 с зовёт `Analyze` снова. Замер порождал повод для
/// следующего замера — вечный двигатель ценой в полсекунды заморозки на каждый оборот.
///
/// Лечится это не в `Analyze`, а в самом замере: измерение не имеет права оставлять след.
/// `PoseProbe.At` не пишет вовсе, когда поза уже равна нужной, а когда пишет — возвращает
/// и позу, и `hasChanged` в то состояние, которое застал.
///
/// Считаем РАБОТУ: сколько раз сцена сочла себя изменённой (`SettleDerivedLinks`).
/// На неподвижной сцене — ноль, сколько бы раз её ни анализировали.</summary>
public class SceneAtRestTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        SceneChangeTracker.Poll();
        SceneChangeTracker.TakeSettlings();
        PoseProbe.TakePosesMoved();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        SceneChangeTracker.TakeSettlings();
        PoseProbe.TakePosesMoved();
    }

    private FacadeElement ACarcassWithAnOpenFacade()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        MakePrimitiveElement("Side", new Vector3Int(560, 716, 18),
            new Vector3(-0.309f, 0.358f, -0.289f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));
        MakeADrawerWearingItsOwnFacade();

        facade.SetOpen(true);
        for (int i = 0; i < 60 && facade.DoorProgress < 1f; i++)
        {
            facade.StepDoor(0.016f);
            SceneChangeTracker.Poll();
        }
        SceneChangeTracker.Poll();
        SceneChangeTracker.TakeSettlings();
        return facade;
    }

    /// <summary>Ящик со своим фасадом — это и есть тот, кого замер двигал: `CollectDrawerFacadeLinks`
    /// спрашивает `DrawerLinks.IsFacadeInContact` про КАЖДЫЙ ящик с привязанным фасадом. Без ящика
    /// в фикстуре сенсор ниже был бы зелёным и на старом коде, то есть не проверял бы ничего.</summary>
    private void MakeADrawerWearingItsOwnFacade()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Box";
        go.transform.position = new Vector3(1.2f, 0.2f, 0f);
        var drawer = go.AddComponent<DrawerElement>();
        drawer.PartName = "Box";
        drawer.AttachedFacadeName = "BoxFacade";
        PartRegistry.Register(drawer);
        _spawned.Add(go);

        MakePrimitiveFacade("BoxFacade", new Vector3Int(600, 150, 18),
            new Vector3(1.2f, 0.2f, 0.26f));
    }

    /// <summary>Главный сенсор. Фасад открыт и стоит; сцену анализируют десять раз подряд,
    /// как это делает тулбар. Сцена обязана НИ РАЗУ не счесть себя изменённой — иначе каждый
    /// анализ заводит следующий, и пользователь получает 2-3 fps на неподвижной кухне.</summary>
    [Test]
    public void SceneAtRestWithAnOpenFacade_NeverDecidesItChanged_HoweverOftenItIsAnalyzed()
    {
        var facade = ACarcassWithAnOpenFacade();
        Assert.Greater(facade.DoorProgress, 0f,
            "положительный контроль сцены: фасад обязан стоять ОТКРЫТЫМ, иначе весь путь "
            + "измерения закрытой позы не задействован и тест зелен на пустом месте");

        for (int frame = 0; frame < 10; frame++)
        {
            SceneAnalyzer.Analyze();
            SceneChangeTracker.Poll();
        }

        Assert.AreEqual(0, SceneChangeTracker.TakeSettlings(),
            "разбор сцены не имеет права быть поводом для следующего разбора: число здесь "
            + "равно числу оборотов вечного двигателя, каждый из которых стоит полсекунды");
    }

    /// <summary>Положительный контроль к счётчику: он обязан считать. Настоящее движение
    /// обязано быть замечено — иначе «ноль» выше значил бы, что сцена вообще перестала
    /// замечать изменения, а это дефект куда хуже лагов.</summary>
    [Test]
    public void APartThatReallyMoved_MakesTheSceneDecideItChanged()
    {
        var facade = ACarcassWithAnOpenFacade();

        facade.transform.position += new Vector3(0.05f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.AreEqual(1, SceneChangeTracker.TakeSettlings(),
            "деталь действительно переехала — сцена обязана это заметить ровно один раз");
    }

    /// <summary>Сенсор на сам замер, ближе к месту. Ящик, уже стоящий в закрытой позе, не
    /// обязан быть сдвинут вовсе: <c>PoseProbe</c> сперва спрашивает, отличается ли поза.
    /// Раньше здесь была безусловная пара записей в <c>transform</c> на каждый ящик при
    /// каждом разборе.</summary>
    [Test]
    public void MeasuringAClosedDrawer_MovesNoPoseAtAll()
    {
        var facade = ACarcassWithAnOpenFacade();
        PoseProbe.TakePosesMoved();

        SceneAnalyzer.Analyze();

        Assert.AreEqual(0, PoseProbe.TakePosesMoved(),
            "детали, уже стоящие в позе, которую хочет померить замер, двигать не за чем");
        Assert.IsNotNull(facade, "фикстура жива");
    }
}

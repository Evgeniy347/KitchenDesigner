using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож ПОКАДРОВОЙ СТОИМОСТИ открытия фасада — вторая половина жалобы
/// «ооочень жёсткие лаги при перемещении объектов и при открытии фасада». Считает работу,
/// а не миллисекунды: сколько раз за ОДИН жест открытия построен список препятствий
/// (<c>OpeningCollision.BuildObstacles</c> — обход всего <c>PartRegistry</c> с
/// <c>GetVertices()</c> на каждый элемент), сколько раз поднялась ревизия сцены и сколько
/// было полных валидаций. Всё это — работа, растущая со сценой.
///
/// Правильный ответ — один обход на жест: препятствия зависят от сцены, а не от кадра, и
/// <c>FacadeElement.SafeProgress</c> кэширует их по <c>SceneRevision.Version</c>. Кэш держится
/// ровно до тех пор, пока анимация двери не поднимает ревизию сама: дверца помечает себя и
/// своих пассажиров через <c>SceneChangeTracker.NoteSelfAnimated</c>, и достаточно одному
/// звену этой цепочки отвалиться, чтобы каждый кадр анимации снова стоил полного обхода
/// сцены. Проверять это глазами нечем — отсюда сенсор.
///
/// Кадр здесь воспроизводится как в приложении: <c>StepDoor</c> (Update) и сразу за ним
/// <c>SceneChangeTracker.Poll</c> (LateUpdate). И сцена перед жестом ОСАЖИВАЕТСЯ одним
/// <c>Poll</c>: только что заспавненные детали несут поднятый <c>transform.hasChanged</c>, и
/// без осадки первый же кадр жеста тратит лишний обход на них, а не на дверцу — сенсор мерил
/// бы фикстуру.</summary>
public class FacadeOpenFrameWorkTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        OpeningCollision.TakeBuildObstacleCalls();
        ConstraintValidator.TakeSceneValidations();
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
        OpeningCollision.TakeBuildObstacleCalls();
        ConstraintValidator.TakeSceneValidations();
    }

    private FacadeElement OpeningFacadeBesideANeighbour()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        MakePrimitiveElement("Side", new Vector3Int(560, 716, 18),
            new Vector3(-0.309f, 0.358f, -0.289f));
        SceneChangeTracker.Poll();
        OpeningCollision.TakeBuildObstacleCalls();
        ConstraintValidator.TakeSceneValidations();
        facade.SetOpen(true);
        return facade;
    }

    private static int RunTheGesture(FacadeElement facade)
    {
        int frames = 0;
        while (facade.DoorProgress < 1f && frames < 60)
        {
            facade.StepDoor(0.016f);
            SceneChangeTracker.Poll();
            frames++;
        }
        return frames;
    }

    /// <summary>Главный сенсор: весь жест открытия обязан стоить ОДИН обход сцены за
    /// препятствиями. Число, равное числу кадров анимации, и есть покадровый обход
    /// <c>PartRegistry</c> с построением геометрии каждой детали — та самая стоимость,
    /// которая на большом проекте роняет fps.</summary>
    [Test]
    public void OpeningGesture_BuildsTheObstacleListOnceForTheWholeGesture()
    {
        var facade = OpeningFacadeBesideANeighbour();

        int frames = RunTheGesture(facade);

        Assert.Greater(frames, 1,
            "положительный контроль: жест обязан занять больше одного кадра, иначе «один "
            + "обход на жест» выполняется само собой и сенсор не проверяет ничего");
        Assert.AreEqual(1, OpeningCollision.TakeBuildObstacleCalls(),
            $"за {frames} кадров анимации список препятствий обязан быть построен один раз: "
            + "он зависит от сцены, а не от кадра. Больше одного = каждый кадр открытия "
            + "обходит весь PartRegistry и строит геометрию каждой детали заново");
    }

    /// <summary>Гипотеза, которую надо было проверить счётчиками, а не рассуждением:
    /// «анимация двигает трансформ каждый кадр, значит <c>SceneRevision</c> растёт каждый
    /// кадр, и всякий покадровый потребитель, у которого ревизия в ключе кэша, пересчитывает
    /// всю сцену». Здесь она ОПРОВЕРГНУТА и остаётся опровергнутой: дверца помечает себя и
    /// пассажиров через <c>NoteSelfAnimated</c>, поэтому за весь жест ревизия не двигается
    /// вовсе и полных валидаций сцены ноль.
    ///
    /// Покраснеет — значит цена открытия выросла ровно на столько кадров, сколько назовёт
    /// сообщение, и болезнь та же, что была у перетаскивания. Числа печатаются и в зелёном
    /// прогоне: сенсор, который молчит, читается как «это даром».</summary>
    [Test]
    public void OpeningGesture_NeitherBumpsTheSceneRevision_NorValidatesTheScene()
    {
        var facade = OpeningFacadeBesideANeighbour();
        int revisionBefore = SceneRevision.Version;
        PartRegistryInstance.TakeGetAllCalls();

        int frames = RunTheGesture(facade);

        int bumps = SceneRevision.Version - revisionBefore;
        int validations = ConstraintValidator.TakeSceneValidations();
        int copies = PartRegistryInstance.TakeGetAllCalls();
        int walks = OpeningCollision.TakeBuildObstacleCalls();
        TestContext.WriteLine($"жест открытия: кадров {frames}, ревизий {bumps}, "
            + $"валидаций сцены {validations}, копий реестра {copies}, обходов {walks}");

        Assert.Greater(frames, 1, "положительный контроль: жест обязан занять больше кадра");
        Assert.AreEqual(0, bumps,
            $"за {frames} кадров анимации ревизия сцены не должна двигаться ни разу: дверца "
            + "движется сама и помечает это NoteSelfAnimated. Число порядка числа кадров = "
            + "каждый кадр открытия обесценивает КАЖДЫЙ кэш, ключом которого служит ревизия");
        Assert.AreEqual(0, validations,
            $"за {frames} кадров открытия сцена не должна валидироваться ни разу");
    }

    /// <summary>Отрицательный контроль к кэшу: сцена изменилась — прошлый список препятствий
    /// устарел и обязан быть построен заново. Без этого теста «один раз» можно было бы
    /// получить, замкнув кэш навсегда, и дверь перестала бы замечать поставленную перед ней
    /// деталь.</summary>
    [Test]
    public void AfterTheSceneChanged_TheNextOpeningFrameBuildsTheObstacleListAgain()
    {
        var facade = OpeningFacadeBesideANeighbour();
        facade.StepDoor(0.016f);
        SceneChangeTracker.Poll();
        OpeningCollision.TakeBuildObstacleCalls();

        MakePrimitiveElement("Latecomer", new Vector3Int(400, 400, 18),
            new Vector3(0.8f, 0.358f, 0.4f));
        facade.StepDoor(0.016f);

        Assert.AreEqual(1, OpeningCollision.TakeBuildObstacleCalls(),
            "в сцене появилась деталь — дверь обязана пересчитать препятствия, иначе она "
            + "проедет сквозь то, что поставили перед ней");
    }
}

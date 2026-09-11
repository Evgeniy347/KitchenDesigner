using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож ПОКАДРОВОЙ СТОИМОСТИ открытия фасада — вторая половина жалобы
/// «ооочень жёсткие лаги при перемещении объектов и при открытии фасада». Считает работу,
/// а не миллисекунды: сколько раз за ОДИН жест открытия построен список препятствий
/// (<c>OpeningCollision.BuildObstacles</c> — обход всего <c>PartRegistry</c> с
/// <c>GetVertices()</c> на каждый элемент, то есть работа, растущая со сценой).
///
/// Правильный ответ — один раз на жест: препятствия зависят от сцены, а не от кадра, и
/// <c>FacadeElement.SafeProgress</c> кэширует их по <c>SceneRevision.Version</c>. Кэш этот
/// держится ровно до тех пор, пока анимация двери не поднимает ревизию сама: дверь помечает
/// себя и своих пассажиров через <c>SceneChangeTracker.NoteSelfAnimated</c>, и достаточно
/// одному звену этой цепочки отвалиться, чтобы каждый кадр анимации снова стоил полного
/// обхода сцены. Проверять это глазами нечем — отсюда сенсор.
///
/// Кадр здесь воспроизводится как в приложении: <c>StepDoor</c> (Update) и сразу за ним
/// <c>SceneChangeTracker.Poll</c> (LateUpdate).</summary>
public class FacadeOpenFrameWorkTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        OpeningCollision.TakeBuildObstacleCalls();
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
    }

    private FacadeElement OpeningFacadeBesideANeighbour()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        MakePrimitiveElement("Side", new Vector3Int(560, 716, 18),
            new Vector3(-0.309f, 0.358f, -0.289f));
        facade.SetOpen(true);
        return facade;
    }

    /// <summary>Главный сенсор: весь жест открытия обязан стоить ОДИН обход сцены за
    /// препятствиями. Число, равное числу кадров анимации, и есть покадровый обход
    /// <c>PartRegistry</c> с построением геометрии каждой детали — та самая стоимость,
    /// которая на проекте пользователя роняет fps.</summary>
    [Test]
    public void OpeningGesture_BuildsTheObstacleListOnceForTheWholeGesture()
    {
        var facade = OpeningFacadeBesideANeighbour();

        int frames = 0;
        while (facade.DoorProgress < 1f && frames < 60)
        {
            facade.StepDoor(0.016f);
            SceneChangeTracker.Poll();
            frames++;
        }

        Assert.Greater(frames, 1,
            "положительный контроль: жест обязан занять больше одного кадра, иначе «один "
            + "обход на жест» выполняется само собой и сенсор не проверяет ничего");
        Assert.AreEqual(1, OpeningCollision.TakeBuildObstacleCalls(),
            $"за {frames} кадров анимации список препятствий обязан быть построен один раз: "
            + "он зависит от сцены, а не от кадра. Больше одного = каждый кадр открытия "
            + "обходит весь PartRegistry и строит геометрию каждой детали заново");
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

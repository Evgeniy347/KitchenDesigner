using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож ПОКАДРОВОЙ СТОИМОСТИ перетаскивания. Считает не время (в batch оно
/// флаки), а РАБОТУ, растущую со сценой: сколько полных валидаций сцены сделал один кадр
/// жеста и для скольких элементов при этом построена геометрия. Образец — счётчик
/// <c>ScrewLegHostLink.HostGeometriesBuiltByLastApplyAll</c>, а не секундомер.
///
/// Зачем он появился: пользователь сообщил «ооочень жёсткие лаги при перемещении», и все
/// 5600 тестов при этом были зелёными — покадровую стоимость не стерёг никто. Цена кадра
/// измерена на ядре под dotnet, на размере проекта пользователя (401 деталь):
/// <c>SnapCore.TrySnap</c> ≈ 4,1 мс и <c>ValidationCore.Validate</c> ≈ 3,9 мс, обе линейно
/// по числу деталей (0,53 / 0,54 мс при 50 деталях). Обе эти работы кадр перетаскивания
/// делал ЗАНОВО даже тогда, когда мышь стояла и кандидат позиции не менялся ни на микрон.
///
/// Валидация здесь — представитель всей покадровой работы: подсветка перетаскивания
/// (<c>UpdateDragTint</c>) спрашивает шлюз, шлюз снимает <c>SceneViolations.OfScene()</c>, а
/// это полная валидация ВСЕЙ сцены. Считать её дешевле и надёжнее, чем гоняться за
/// миллисекундами, и счётчик краснеет в обе стороны — см. положительный контроль.</summary>
public class DragFrameWorkTests : ElementTestBase
{
    private ElementMover? _mover;
    private bool _snapBefore;
    private bool _blockBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _snapBefore = KitchenSettings.Instance.SnapEnabled;
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        var go = new GameObject("ElementMover");
        _spawned.Add(go);
        _mover = go.AddComponent<ElementMover>();
        ConstraintValidator.TakeSceneValidations();
        ConstraintValidator.TakeElementGeometriesBuilt();
    }

    [TearDown]
    public void TearDown()
    {
        if (_mover != null) _mover.FinishDragNow();
        if (_mover != null) _mover.RestoreDragMaterial();
        KitchenSettings.Instance.SnapEnabled = _snapBefore;
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ConstraintValidator.TakeSceneValidations();
        ConstraintValidator.TakeElementGeometriesBuilt();
    }

    private KitchenElement StartDragging()
    {
        var dragged = MakePrimitiveElement("Dragged", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18),
            new Vector3(0.6f, 0.36f, 0f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));

        _mover!.BeginDragOn(dragged);
        _mover.SaveDragMaterial(dragged);
        return dragged;
    }

    /// <summary>Положительный контроль, и он обязан стоять ПЕРВЫМ: без него «ноль работы»
    /// ниже было бы зелёным просто потому, что счётчик не считает или кадр не делает ничего
    /// вообще. Кадр, который реально двинул деталь, обязан заплатить хотя бы одну полную
    /// валидацию и построить геометрию хотя бы для трёх деталей сцены.</summary>
    [Test]
    public void DragFrameThatMovedThePart_ValidatesTheWholeSceneAtLeastOnce()
    {
        var dragged = StartDragging();
        ConstraintValidator.TakeSceneValidations();
        ConstraintValidator.TakeElementGeometriesBuilt();

        _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.05f, 0f, 0f));

        Assert.GreaterOrEqual(ConstraintValidator.TakeSceneValidations(), 1,
            "кадр, сдвинувший деталь, обязан заново спросить у сцены, законно ли она стоит — "
            + "иначе подсветка перетаскивания врёт");
        Assert.GreaterOrEqual(ConstraintValidator.TakeElementGeometriesBuilt(), 3,
            "полная валидация строит геометрию КАЖДОЙ детали сцены — на этом и растёт цена");
    }

    /// <summary>Главный сенсор. Мышь стоит: кандидат позиции тот же, сцена та же, прилипание
    /// то же — значит и ответ тот же, и повторять восьмимиллисекундную работу не за чем.
    /// До правки счётчик показывал здесь одну полную валидацию на КАЖДЫЙ такой кадр, то есть
    /// 60 валидаций всей сцены в секунду на неподвижной мыши.
    ///
    /// Два кадра перед замером — не ритуал: первый двигает деталь, второй даёт
    /// <c>SceneChangeTracker.Poll</c> закрыть ревизию сцены, поднятую этим движением. Только
    /// с третьего кадра ключ повтора совпадает целиком.</summary>
    [Test]
    public void DragFrameThatMovedNothing_ValidatesTheSceneNotAtAll()
    {
        var dragged = StartDragging();
        var candidate = dragged.transform.position + new Vector3(0.05f, 0f, 0f);

        _mover!.DragFrameOn(candidate);
        SceneChangeTracker.Poll();
        _mover.DragFrameOn(candidate);
        SceneChangeTracker.Poll();

        ConstraintValidator.TakeSceneValidations();
        ConstraintValidator.TakeElementGeometriesBuilt();

        _mover.DragFrameOn(candidate);

        Assert.AreEqual(0, ConstraintValidator.TakeSceneValidations(),
            "мышь не двигалась, сцена не менялась — кадр обязан не делать работы, растущей "
            + "со сценой. Ненулевое число здесь = те самые лаги перетаскивания");
        Assert.AreEqual(0, ConstraintValidator.TakeElementGeometriesBuilt(),
            "ни одной детали не за чем строить геометрию заново на неизменившемся кадре");
    }

    /// <summary>Отрицательный контроль к повтору: как только кандидат позиции сдвинулся,
    /// работа обязана вернуться. Без этого теста «ноль» выше можно было бы получить,
    /// заморозив подсветку на весь жест — то есть сломав её.</summary>
    [Test]
    public void DragFrameAfterTheMouseMovedAgain_ValidatesTheSceneOnceMore()
    {
        var dragged = StartDragging();
        var candidate = dragged.transform.position + new Vector3(0.05f, 0f, 0f);

        _mover!.DragFrameOn(candidate);
        SceneChangeTracker.Poll();
        _mover.DragFrameOn(candidate);
        SceneChangeTracker.Poll();
        ConstraintValidator.TakeSceneValidations();

        _mover.DragFrameOn(candidate + new Vector3(0.05f, 0f, 0f));

        Assert.GreaterOrEqual(ConstraintValidator.TakeSceneValidations(), 1,
            "мышь поехала дальше — ответ шлюза устарел и обязан быть пересчитан");
    }
}

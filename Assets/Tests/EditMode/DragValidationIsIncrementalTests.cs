using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Шов между приложением и инкрементальной валидацией. Правила и их
/// эквивалентность полному проходу проверены на быстром пути
/// (<c>IncrementalValidationTests</c>, <c>GestureValidationTests</c>); здесь
/// проверяется ровно то, чего быстрый путь не видит: что во время настоящего
/// перетаскивания <c>ConstraintValidator.Validate</c> действительно идёт по
/// замороженному графу, что заморозок за жест единицы, а не по одной на кадр, и
/// что ответ, который получает подсветка, совпадает с полной валидацией той же
/// сцены — на реальных элементах сцены, а не на синтетических коробках.
///
/// Без этого файла работу можно было бы «сделать» целиком в ядре и не
/// подключить: все тесты ядра остались бы зелёными, а пользователь — со своими
/// 7 fps.</summary>
public class DragValidationIsIncrementalTests : ElementTestBase
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
    }

    [TearDown]
    public void TearDown()
    {
        if (_mover != null) _mover.FinishDragNow();
        if (_mover != null) _mover.RestoreDragMaterial();
        KitchenSettings.Instance.SnapEnabled = _snapBefore;
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement StartDragging()
    {
        var dragged = MakePrimitiveElement("Dragged", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18),
            new Vector3(0.6f, 0.36f, 0f));
        MakePrimitiveElement("Shelf", new Vector3Int(564, 18, 560),
            new Vector3(0.3f, 0.4f, 0f));
        MakeFloorAnchor();
        AssertTheSceneHasAnAnchor();

        _mover!.BeginDragOn(dragged);
        _mover.SaveDragMaterial(dragged);
        return dragged;
    }

    /// <summary>Пол — не «деталь, названная Floor». Роль решается в одном месте,
    /// <c>ValidationSnapshot.KindOf</c>, и полом там считается носитель
    /// <see cref="BasePlate"/> либо <c>FloorElement</c>. Примитив с подходящим
    /// именем якорем не является.</summary>
    private KitchenElement MakeFloorAnchor()
    {
        var floor = MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));
        floor.gameObject.AddComponent<BasePlate>();
        return floor;
    }

    /// <summary>Страж стенда, а не продукта, и он обязан стоять до первого
    /// замера. Сцена без якоря — законная причина ОТКАЗАТЬ в заморозке: там
    /// корень обхода связности зависит от порядка списка (см.
    /// <c>ValidationLocalityTests.SceneWithoutAnchors_...</c>). Без этой проверки
    /// «заморозок ноль» читается как «шов не подключён», хотя шов подключён и
    /// отказывает правильно — именно так этот файл и покраснел в первый раз.</summary>
    private static void AssertTheSceneHasAnAnchor()
    {
        var snapshots = new List<ValidationElement>();
        ValidationSnapshot.Build(PartRegistry.GetAll(), snapshots);
        Assert.IsTrue(ValidationCore.HasAnchor(snapshots),
            "в сцене стенда обязан быть якорь: без него заморозка отказывает по СВОЕЙ "
            + "причине, и тесты ниже мерили бы не шов, а этот отказ");
    }

    /// <summary>Ответ, который приложение отдаёт подсветке, против независимой
    /// полной валидации той же сцены. Сравниваются ИМЕНА нарушителей: именно их
    /// видит пользователь красным.</summary>
    private static void AssertTheAppAgreesWithAFullPass(string when)
    {
        var scene = PartRegistry.GetAll();

        var snapshots = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, snapshots);
        var full = ValidationCore.Validate(snapshots);
        var expected = new List<string>();
        foreach (int i in full.Violations) expected.Add(snapshots[i].Name);
        expected.Sort(StringComparer.Ordinal);

        var actual = new List<string>();
        foreach (var v in ConstraintValidator.Validate(scene).violations)
            if (v != null) actual.Add(v.PartName);
        actual.Sort(StringComparer.Ordinal);

        CollectionAssert.AreEqual(expected, actual,
            $"{when}: подсветка получила не тот список нарушителей, что полная валидация. "
            + "Инкрементальный проход обязан СОВПАДАТЬ, а не приближать — по этому списку "
            + "пользователь принимает решения");
    }

    /// <summary>Положительный контроль, и он обязан стоять первым: если жест не
    /// доходит до инкрементального прохода вовсе, «мало пар» ниже было бы
    /// зелёным на пустом месте.</summary>
    [Test]
    public void ADragFrame_GoesThroughTheFrozenGraph()
    {
        var dragged = StartDragging();
        int before = ConstraintValidator.GestureFreezes;
        ConstraintValidator.TakeSceneValidations();

        _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.05f, 0f, 0f));
        SceneChangeTracker.Poll();
        _mover.DragFrameOn(dragged.transform.position + new Vector3(0.05f, 0f, 0f));

        Assert.GreaterOrEqual(ConstraintValidator.TakeSceneValidations(), 1,
            "кадр жеста обязан вообще дойти до валидации сцены — если нет, то ноль заморозок "
            + "ниже означает не «шов не подключён», а «кадр сюда не заходил»");
        Assert.Greater(ConstraintValidator.GestureFreezes, before,
            "кадр жеста обязан заморозить сцену — иначе инкрементальный проход не подключён "
            + "и каждый кадр по-прежнему стоит полную валидацию");
    }

    /// <summary>Главный сенсор цены: заморозок за жест единицы, а не по одной на
    /// кадр. Заморозка стоит полную валидацию, поэтому «заморозка каждый кадр» —
    /// это ровно та же цена, что была до работы, только запутаннее.</summary>
    [Test]
    public void ALongDrag_FreezesAHandfulOfTimes_NotOncePerFrame()
    {
        var dragged = StartDragging();
        int before = ConstraintValidator.GestureFreezes;

        for (int i = 0; i < 20; i++)
        {
            _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.01f, 0f, 0f));
            SceneChangeTracker.Poll();
        }

        int freezes = ConstraintValidator.GestureFreezes - before;
        Assert.LessOrEqual(freezes, 4,
            $"двадцать кадров жеста заморозили сцену {freezes} раз — множество муверов "
            + "обязано стабилизироваться, иначе кадр стоит полную валидацию");
        Assert.Less(ConstraintValidator.GesturePairsInLastFrame, 60,
            $"кадр жеста обязан стоить десятки пар, а стоит "
            + $"{ConstraintValidator.GesturePairsInLastFrame}");
    }

    /// <summary>Совпадение с полным проходом на реальной сцене, в три момента:
    /// до жеста, в середине жеста и после него. Середина — то, ради чего всё
    /// затевалось; края — чтобы заморозка не пережила жест.</summary>
    [Test]
    public void TheHighlightAgreesWithAFullPass_BeforeDuringAndAfterTheDrag()
    {
        var dragged = StartDragging();
        AssertTheAppAgreesWithAFullPass("в начале жеста");

        for (int i = 0; i < 6; i++)
        {
            _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.02f, 0.01f, 0f));
            SceneChangeTracker.Poll();
            AssertTheAppAgreesWithAFullPass($"кадр жеста {i}");
        }

        _mover!.FinishDragNow();
        SceneChangeTracker.Poll();
        AssertTheAppAgreesWithAFullPass("после жеста");
    }

    /// <summary>Жест кончился — замороженный граф обязан быть отпущен. Иначе
    /// следующая правка сцены командой, отменой или MCP считалась бы по графу
    /// прошлого перетаскивания.</summary>
    [Test]
    public void AfterTheDrag_TheFrozenGraphIsDropped()
    {
        var dragged = StartDragging();
        _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.05f, 0f, 0f));
        SceneChangeTracker.Poll();

        _mover.FinishDragNow();
        ConstraintValidator.Validate(PartRegistry.GetAll());
        int after = ConstraintValidator.GestureFreezes;

        ConstraintValidator.Validate(PartRegistry.GetAll());
        Assert.AreEqual(after, ConstraintValidator.GestureFreezes,
            "жеста нет — валидация обязана идти полным проходом и ничего не замораживать");
    }
}

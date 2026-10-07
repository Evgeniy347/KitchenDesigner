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
        ConstraintValidator.Ground = ImpliedGround.None;
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
        ConstraintValidator.Ground = ImpliedGround.At(0f);
        AssertTheSceneHasAnAnchor();

        _mover!.BeginDragOn(dragged);
        _mover.SaveDragMaterial(dragged);
        return dragged;
    }

    /// <summary>Страж стенда, а не продукта, и он обязан стоять до первого
    /// замера. Сцена без якоря — законная причина ОТКАЗАТЬ в заморозке: там
    /// землёй объявляется самый нижний уровень ВСЕЙ сцены, а такой ответ
    /// нельзя пересчитать по одной детали (см.
    /// <c>ConnectivityWithoutAnchorTests</c>). Без этой проверки
    /// «заморозок ноль» читается как «шов не подключён», хотя шов подключён и
    /// отказывает правильно — именно так этот файл и покраснел в первый раз.</summary>
    private static void AssertTheSceneHasAnAnchor()
    {
        var snapshots = new List<ValidationElement>();
        ValidationSnapshot.Build(PartRegistry.GetAll(), snapshots);
        Assert.IsTrue(ValidationCore.HasAnchor(snapshots, ConstraintValidator.Ground),
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
        var full = ValidationCore.Validate(snapshots, ConstraintValidator.Ground);
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
    /// зелёным на пустом месте.
    ///
    /// Прилипание здесь выключено намеренно, и это ТА ЖЕ ловушка, которую
    /// <c>DragFrameWorkTests.DragFrameThatMovedThePart_ValidatesTheWholeSceneAtLeastOnce</c>
    /// уже назвал на этом же стенде: грань детали в исходной позе совпадает с
    /// гранью соседки (обе 600 мм, центры 0 и 0,6 м, общая грань на x = 0,3 м),
    /// и кандидат в 50 мм вглубь неё возвращался прилипанием ровно на место —
    /// <c>ElementMover</c> не писал в <c>transform</c> вовсе. Кадр не менял в
    /// сцене НИЧЕГО, а тест назывался «кадр жеста».
    ///
    /// Подмена была не видна, пока <c>TakeSceneValidations</c> считал ВЫЗОВЫ:
    /// вызов был, и счётчик показывал единицу. Считать он теперь стал РАБОТУ —
    /// полные валидации, — и кадр, ничего не изменивший, честно показал ноль.
    /// Это не ослабление: два соседних теста того же прогона запирают вывод с
    /// обеих сторон. <c>DragFrameThatMovedThePart_…</c> (прилипание выключено,
    /// деталь действительно едет) даёт ≥ 1 валидацию и ≥ 1 пересборку геометрии
    /// — значит калитка движение видит;
    /// <c>TheHighlightAgreesWithAFullPass_BeforeDuringAndAfterTheDrag</c>
    /// сверяет список нарушителей с независимым полным проходом на КАЖДОМ из
    /// шести кадров жеста и совпадает — значит калитка не слепа. Остаётся ровно
    /// одно объяснение нуля: деталь не двигалась.
    ///
    /// Поэтому предпосылка теперь ПРОВЕРЯЕМАЯ и стоит первой: стенд обязан
    /// доказать, что деталь уехала. Отвалится шов — упадут требования ниже;
    /// перестанет двигаться стенд — упадёт предпосылка и НАЗОВЁТ себя, а не
    /// уведёт в несуществующий дефект шва.</summary>
    [Test]
    public void ADragFrame_GoesThroughTheFrozenGraph()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var dragged = StartDragging();
        int before = ConstraintValidator.GestureFreezes;
        ConstraintValidator.TakeSceneValidations();
        var start = dragged.transform.position;

        _mover!.DragFrameOn(start + new Vector3(0.05f, 0f, 0f));
        SceneChangeTracker.Poll();
        var afterFirstFrame = dragged.transform.position;
        _mover.DragFrameOn(afterFirstFrame + new Vector3(0.05f, 0f, 0f));

        TestContext.WriteLine($"деталь: старт x={start.x:F4}, после кадра "
            + $"x={afterFirstFrame.x:F4}, сейчас x={dragged.transform.position.x:F4}");

        Assert.AreNotEqual(start.x, afterFirstFrame.x,
            "стенд обязан РЕАЛЬНО сдвинуть деталь, иначе оба требования ниже стерегут не "
            + "то. Ноль валидаций у кадра, не изменившего в сцене ни одного числа, — это "
            + "калитка по входу работает как задумано, а не отвалившийся шов");
        Assert.GreaterOrEqual(ConstraintValidator.TakeSceneValidations(), 1,
            "кадр жеста, сдвинувший деталь, обязан дойти до валидации сцены — если нет, то "
            + "ноль заморозок ниже означает не «шов не подключён», а «кадр сюда не заходил»");
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

    /// <summary>Правило, которое до сих пор ВЫВОДИЛОСЬ читателем из двух
    /// классов, а теперь стоит в имени теста: ответ, оставшийся от жеста, и
    /// есть ответ покоя.
    ///
    /// После конца жеста <c>ValidationReuse</c> держит результат, посчитанный
    /// <c>FrozenValidation.Revalidate</c> по ЗАМОРОЖЕННОМУ графу, и полной
    /// валидации не запускает, пока вход не изменится. Законно это ровно
    /// настолько, насколько инкрементальный проход равен полному, — и это
    /// равенство проверено (<c>IncrementalPass_MatchesFullValidation_*</c>,
    /// <c>GestureValidationTests</c>). Но правило, собираемое читателем из двух
    /// наборов в разных папках, живёт до первого, кто его не соберёт: тогда
    /// инкрементальный ответ тихо станет ответом покоя, и «ноль валидаций на
    /// кадре покоя» будет означать «мы досматриваем вчерашний сон».
    ///
    /// Обе посылки проверяются на месте, и обе несущие. Без первой («деталь
    /// реально уехала») жеста не было вовсе. Без второй («кадр покоя не считал
    /// заново») сравнение сверяло бы полный проход с полным проходом и
    /// проходило бы всегда.</summary>
    [Test]
    public void TheAnswerLeftOverFromAGesture_IsTheSameAsAFullValidationAtRest()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var dragged = StartDragging();
        int freezesBefore = ConstraintValidator.GestureFreezes;
        var start = dragged.transform.position;

        for (int i = 0; i < 4; i++)
        {
            _mover!.DragFrameOn(dragged.transform.position + new Vector3(0.02f, 0f, 0f));
            SceneChangeTracker.Poll();
        }

        Assert.AreNotEqual(start.x, dragged.transform.position.x,
            "посылка стенда: за четыре кадра деталь обязана уехать, иначе жеста не было "
            + "и «ответ от жеста» ниже — это ответ полного прохода");
        Assert.Greater(ConstraintValidator.GestureFreezes, freezesBefore,
            "посылка стенда: ответ обязан прийти с ЗАМОРОЖЕННОГО графа. Без заморозки "
            + "жест считался полным проходом, и утверждение ниже сверяло бы полный проход "
            + "с полным проходом");

        _mover!.FinishDragNow();
        ConstraintValidator.TakeSceneValidations();

        ConstraintValidator.Validate(PartRegistry.GetAll());
        int recomputed = ConstraintValidator.TakeSceneValidations();
        Assert.AreEqual(0, recomputed,
            $"посылка стенда: кадр покоя после жеста сделал {recomputed} полных валидаций, "
            + "то есть отдал СВЕЖИЙ ответ, а не оставшийся от жеста. Именно оставшийся и "
            + "проверяется ниже — со свежим сравнение бессмысленно");

        AssertTheAppAgreesWithAFullPass(
            "покой после жеста: ответ достался от замороженного графа");
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

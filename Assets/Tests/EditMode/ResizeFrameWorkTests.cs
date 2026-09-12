using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож ПОКАДРОВОЙ СТОИМОСТИ растягивания ручками — родной брат
/// <c>DragFrameWorkTests</c>. Считает не время (в batch оно флаки), а РАБОТУ, растущую со
/// сценой: сколько раз кадр жеста перекрасил подсветку и сколько полных валидаций сцены
/// за этим последовало.
///
/// Зачем он появился: дамп test-results/perf/perf_20260911_191534.csv дал 68 тяжёлых кадров
/// из 177, и все они пришли одним путём — <c>ResizeHandleManager.UpdateResize</c> зовёт
/// <c>ElementHighlighter.RefreshHighlights</c> КАЖДЫЙ кадр жеста, а тот валидирует всю
/// сцену и обходит каждую деталь. Кадр, в котором размер не изменился, платил ровно
/// столько же, сколько кадр, в котором деталь выросла.
///
/// Считаем именно ВЫЗОВЫ <c>RefreshHighlights</c> и валидации, а не число реально
/// заменённых материалов: материал на неизменившемся кадре и так совпадает с прежним, и
/// счётчик «перекрашенных тел» был бы нулём даже на сломанном коде — то есть зелёным
/// впустую.</summary>
public class ResizeFrameWorkTests : ElementTestBase
{
    private ResizeHandleManager? _manager;
    private ElementHighlighter? _highlighter;
    private ElementHighlighter? _highlighterBefore;
    private ResizeHandleManager.HandleMode _modeBefore;
    private bool _snapBefore;
    private bool _blockBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _modeBefore = ResizeHandleManager.Mode;
        _snapBefore = KitchenSettings.Instance.SnapEnabled;
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        KitchenSettings.Instance.SnapEnabled = false;

        var managerGo = new GameObject("ResizeHandleManager");
        _spawned.Add(managerGo);
        _manager = managerGo.AddComponent<ResizeHandleManager>();

        var hlGo = new GameObject("ElementHighlighter");
        _spawned.Add(hlGo);
        _highlighter = hlGo.AddComponent<ElementHighlighter>();
        _highlighterBefore = ElementHighlighter.Instance;
        ElementHighlighter.Instance = _highlighter;

        ConstraintValidator.TakeSceneValidations();
    }

    [TearDown]
    public void TearDown()
    {
        if (_manager != null) _manager.FinishDragNow();
        ResizeHandleManager.SetMode(_modeBefore);
        ElementHighlighter.Instance = _highlighterBefore;
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
    }

    private ResizeHandleManager Manager => _manager!;

    private KitchenElement ABoardBesideItsNeighbours()
    {
        var stretched = MakePrimitiveElement("Stretched", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18),
            new Vector3(0.9f, 0.36f, 0f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));
        return stretched;
    }

    private KitchenElement StartStretching()
    {
        var stretched = ABoardBesideItsNeighbours();
        SceneChangeTracker.Poll();
        Manager.BeginDragOn(stretched, 0);
        return stretched;
    }

    private void SettleTheHandleAt(float pointerDelta)
    {
        for (int frame = 0; frame < 4; frame++)
        {
            Manager.ResizeFrameBy(pointerDelta);
            SceneChangeTracker.Poll();
        }
    }

    private int RefreshesOf(System.Action frame)
    {
        int before = _highlighter!.RefreshCount;
        frame();
        return _highlighter.RefreshCount - before;
    }

    /// <summary>Положительный контроль, и он обязан стоять ПЕРВЫМ: без него «ноль работы»
    /// ниже было бы зелёным просто потому, что счётчик ничего не считает или кадр не делает
    /// ничего вовсе. Кадр, растянувший деталь, обязан перекрасить подсветку и заново
    /// спросить у сцены, законна ли она.</summary>
    [Test]
    public void ResizeFrameThatChangedTheSize_RefreshesTheHighlightAtLeastOnce()
    {
        var stretched = StartStretching();
        var dimsBefore = stretched.DimensionsMM;
        ConstraintValidator.TakeSceneValidations();

        int refreshes = RefreshesOf(() => Manager.ResizeFrameBy(0.05f));

        Assert.AreNotEqual(dimsBefore, stretched.DimensionsMM,
            "кадр обязан был реально растянуть деталь — иначе тест ниже меряет не то");
        Assert.GreaterOrEqual(refreshes, 1,
            "деталь выросла — подсветка обязана быть пересчитана, иначе она врёт про "
            + "законность новой сцены");
        Assert.GreaterOrEqual(ConstraintValidator.TakeSceneValidations(), 1,
            "перекраска подсветки идёт через полную валидацию сцены — на этом и растёт "
            + "цена кадра");
    }

    /// <summary>Главный сенсор. Ручку держат неподвижно: ход тот же, сцена та же, прилипание
    /// то же, применённый размер тот же — значит и подсветка выйдет та же. До правки здесь
    /// была одна перекраска на КАЖДЫЙ такой кадр, то есть 60 полных валидаций сцены в
    /// секунду на неподвижной мыши (68 тяжёлых кадров из 177 в дампе).
    ///
    /// Кадры перед замером — не ритуал: первый растягивает деталь, следующий даёт
    /// <c>SceneChangeTracker.Poll</c> закрыть ревизию сцены, поднятую этим растягиванием.
    /// Пока жест не устоялся, ключ повтора совпасть не может.</summary>
    [Test]
    public void ResizeFrameThatChangedNothing_RefreshesTheHighlightNotAtAll()
    {
        StartStretching();

        SettleTheHandleAt(0.05f);
        ConstraintValidator.TakeSceneValidations();

        int refreshes = RefreshesOf(() => Manager.ResizeFrameBy(0.05f));

        Assert.AreEqual(0, refreshes,
            "ручку не двигали, сцена не менялась, размер тот же — кадру незачем повторять "
            + "работу, растущую со сценой. Ненулевое число здесь = те самые лаги растягивания");
        Assert.AreEqual(0, ConstraintValidator.TakeSceneValidations(),
            "ни одной валидации всей сцены на кадре, который ничего не изменил");
    }

    /// <summary>Отрицательный контроль к повтору: как только ручку потянули дальше, работа
    /// обязана вернуться. Без этого теста «ноль» выше можно было бы получить, заморозив
    /// подсветку на весь жест — то есть сломав её.</summary>
    [Test]
    public void ResizeFrameAfterThePointerMovedAgain_RefreshesTheHighlightOnceMore()
    {
        var stretched = StartStretching();

        SettleTheHandleAt(0.05f);
        var dimsSettled = stretched.DimensionsMM;

        int refreshes = RefreshesOf(() => Manager.ResizeFrameBy(0.1f));

        Assert.AreNotEqual(dimsSettled, stretched.DimensionsMM,
            "ручку потянули дальше — деталь обязана вырасти ещё");
        Assert.GreaterOrEqual(refreshes, 1,
            "размер изменился — прошлая подсветка описывает не эту сцену и обязана быть "
            + "пересчитана");
    }

    /// <summary>Ручки работают и во втором режиме — «Move». Путь другой (<c>UpdateMove</c>),
    /// а дефект был тот же, поэтому калитка одна на оба: кадр, не сдвинувший деталь, не
    /// перекрашивает подсветку.</summary>
    [Test]
    public void MoveFrameThatChangedNothing_RefreshesTheHighlightNotAtAll()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        var moved = StartStretching();
        var posBefore = moved.transform.position;

        int first = RefreshesOf(() => Manager.MoveFrameBy(0.05f));
        SceneChangeTracker.Poll();
        for (int frame = 0; frame < 4; frame++)
        {
            Manager.MoveFrameBy(0.05f);
            SceneChangeTracker.Poll();
        }

        int repeat = RefreshesOf(() => Manager.MoveFrameBy(0.05f));

        Assert.AreNotEqual(posBefore, moved.transform.position,
            "первый кадр обязан был реально сдвинуть деталь");
        Assert.GreaterOrEqual(first, 1,
            "кадр, сдвинувший деталь, перекрашивает подсветку — положительный контроль");
        Assert.AreEqual(0, repeat,
            "деталь стоит на месте — перекрашивать нечего");
    }
}

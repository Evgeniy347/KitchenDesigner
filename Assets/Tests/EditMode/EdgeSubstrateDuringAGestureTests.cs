using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Решение пользователя, дословно: «при перемещении кромки то вообще не нужны,
/// только когда деталь устанавливается». Значит подложка торцов не пересобирается ВО ВРЕМЯ
/// жеста ни разу — один раз, когда деталь поставлена.
///
/// Цена вопроса измерена: третий профиль (test-results/perf/perf_20260911_184756.csv,
/// 153 кадра, «7 fps при перетаскивании») показал <c>EdgeSubstrate.SyncScene</c> по
/// <b>43-46 мс в каждом кадре перетаскивания</b> при кадре в 133-145 мс — треть кадра на
/// работу, результат которой до отпускания мыши никто не видит.
///
/// Это не «пересобрать умнее», а «не делать во время жеста»: калитка — `SceneGesture`,
/// единственное место, где спрашивается «идёт ли жест», общее для перетаскивания и
/// растягивания. Считаем пересборки, а не миллисекунды.</summary>
public class EdgeSubstrateDuringAGestureTests : ElementTestBase
{
    private ElementMover? _mover;
    private ElementHighlighter? _highlighter;
    private ElementHighlighter? _highlighterBefore;
    private bool _blockBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;

        var moverGo = new GameObject("ElementMover");
        _spawned.Add(moverGo);
        _mover = moverGo.AddComponent<ElementMover>();

        var hlGo = new GameObject("ElementHighlighter");
        _spawned.Add(hlGo);
        _highlighter = hlGo.AddComponent<ElementHighlighter>();
        _highlighterBefore = ElementHighlighter.Instance;
        ElementHighlighter.Instance = _highlighter;

        EdgeSubstrate.TakeSceneSyncs();
    }

    [TearDown]
    public void TearDown()
    {
        if (_mover != null) _mover.FinishDragNow();
        if (_mover != null) _mover.RestoreDragMaterial();
        ElementHighlighter.Instance = _highlighterBefore;
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        EdgeSubstrate.TakeSceneSyncs();
    }

    private KitchenElement ABoardBesideItsNeighbours()
    {
        var dragged = MakePrimitiveElement("Dragged", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18),
            new Vector3(0.6f, 0.36f, 0f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));
        return dragged;
    }

    /// <summary>Главный сенсор. Десять кадров жеста, и в каждом сцену просят пересобрать
    /// подложку — ровно то, что делает приложение через `ElementHighlighter`. Ни одной
    /// пересборки быть не должно. До правки здесь было десять, по 44 мс каждая.</summary>
    [Test]
    public void DuringADrag_TheEdgeSubstrateIsNotRebuiltEvenOnce()
    {
        var dragged = ABoardBesideItsNeighbours();
        var all = PartRegistry.GetAll();
        _mover!.BeginDragOn(dragged);
        EdgeSubstrate.TakeSceneSyncs();

        for (int frame = 0; frame < 10; frame++)
        {
            _mover.DragFrameOn(dragged.transform.position + new Vector3(0.01f * frame, 0f, 0f));
            EdgeSubstrate.SyncScene(all);
        }

        Assert.AreEqual(0, EdgeSubstrate.TakeSceneSyncs(),
            "пока деталь едет, торцы никому не нужны — пользователь решил это прямо: "
            + "«при перемещении кромки то вообще не нужны, только когда деталь "
            + "устанавливается». Число здесь — это по 44 мс на кадр");
    }

    /// <summary>Вторая половина того же требования и первый противоположный вход: как
    /// только деталь поставлена, подложка обязана быть пересобрана — иначе торцы навсегда
    /// останутся такими, какими были до жеста, и «экономия» превратится в дефект.</summary>
    [Test]
    public void WhenThePartIsPutDown_TheEdgeSubstrateIsRebuiltExactlyOnce()
    {
        var dragged = ABoardBesideItsNeighbours();
        _mover!.BeginDragOn(dragged);
        _mover.DragFrameOn(dragged.transform.position + new Vector3(0.05f, 0f, 0f));
        EdgeSubstrate.TakeSceneSyncs();

        _mover.FinishDragNow();

        Assert.AreEqual(1, EdgeSubstrate.TakeSceneSyncs(),
            "деталь поставлена — вот теперь торцы и пересобираются, один раз на жест");
    }

    /// <summary>Второй противоположный вход: без жеста всё по-прежнему. Без него калитка
    /// могла бы быть закрыта навсегда, и тест выше остался бы зелёным.</summary>
    [Test]
    public void WithoutAGesture_AnOrdinaryEditStillRebuildsTheEdgeSubstrate()
    {
        ABoardBesideItsNeighbours();
        var all = PartRegistry.GetAll();
        EdgeSubstrate.TakeSceneSyncs();

        Assert.IsFalse(SceneGesture.InProgress, "жеста нет — это и проверяется");
        EdgeSubstrate.SyncScene(all);

        Assert.AreEqual(1, EdgeSubstrate.TakeSceneSyncs(),
            "вне жеста подложка собирается как собиралась");
    }

    /// <summary>Калитка одна на оба жеста: растягивание ручками ровно так же не нуждается в
    /// торцах, пока тянут. Спрашивается это в одном месте — `SceneGesture`.</summary>
    [Test]
    public void DuringAHandleResize_TheEdgeSubstrateIsNotRebuiltEither()
    {
        var dragged = ABoardBesideItsNeighbours();
        var all = PartRegistry.GetAll();
        var handles = new GameObject("ResizeHandleManager");
        _spawned.Add(handles);
        var manager = handles.AddComponent<ResizeHandleManager>();
        manager.BeginDragOn(dragged, 0);
        EdgeSubstrate.TakeSceneSyncs();

        EdgeSubstrate.SyncScene(all);
        int during = EdgeSubstrate.TakeSceneSyncs();
        manager.FinishDragNow();

        Assert.AreEqual(0, during,
            "пока тянут ручку, торцы не нужны по той же причине, что и при перетаскивании");
    }
}

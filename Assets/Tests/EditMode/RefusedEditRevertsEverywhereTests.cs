using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Три пути отказываются от правки по одному и тому же признаку — нарушение,
/// ВНЕСЁННОЕ этой правкой (`EditGate`) — но откатывают её каждый по-своему:
/// `ContextMenuUI.Apply` возвращает снимок свойств и роняет захват команд,
/// `ElementMover.FinishDrag` ещё и отменяет пересборку трубных пролётов и посадочные
/// размеры, `ResizeHandleManager.FinishDrag` возвращает размеры и позицию. Своди их силой в
/// одну процедуру — потеряешь часть отката; оставь как есть — и разница в ПОЛНОТЕ не ловится
/// ничем, потому что у каждого пути был свой тест про свои поля, и ни один не спрашивал, что
/// стало со сценой ЦЕЛИКОМ.
///
/// Это и есть недостающий сенсор (`agents/TEST-DESIGN.md` → «A defect the platform cannot
/// report stays invisible until you build the sensor»): отпечаток всей сцены — каждый
/// зарегистрированный элемент, все его `[Undoable]`-свойства, размеры, позиция и поворот —
/// снятый до жеста и после него. Неполный откат любого из трёх путей краснеет здесь именем
/// того поля, которое осталось изменённым, и не только на самой детали: сосед, которого путь
/// задел и забыл вернуть, попадает в тот же отпечаток.
///
/// Второе общее требование — отказ обязан быть НАЗВАН. До этих тестов его выполняла одна
/// панель, а оба жеста возвращали деталь на место молча: с точки зрения пользователя
/// перетаскивание просто «не работало». Поэтому текст нарушения теперь выдаёт сам шлюз
/// (`EditGate.Refuses`), а проверяется он у всех трёх путей одним и тем же набором
/// утверждений — иначе следующий путь снова окажется немым.</summary>
public class RefusedEditRevertsEverywhereTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockBefore;
    private ResizeHandleManager.HandleMode _modeBefore;
    private StatusBarUI? _statusBar;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = true;
        _modeBefore = ResizeHandleManager.Mode;
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        _root = new GameObject("RefusedEditRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();
        _statusBar = _root.AddComponent<StatusBarUI>();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        ResizeHandleManager.SetMode(_modeBefore);
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        _statusBar = null;
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        PartRegistry.Clear();
    }

    private KitchenElement SpawnPart(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(600, 700, 18);
        PartRegistry.Register(el);
        return el;
    }

    /// <summary>Та же валидная опора, что и у `BlockedApplyRevertsEverythingTests`: два щита
    /// 600 мм грань в грань, сцена чиста. Любая правка, вгоняющая первый во второй, ВНОСИТ
    /// COL-01, которого до неё не было, — значит блокировке есть на что опереться, и тест не
    /// скатывается в «деталь нарушает вообще».</summary>
    private KitchenElement SpawnTouchingPair()
    {
        var a = SpawnPart("Panel", Vector3.zero);
        SpawnPart("Neighbour", new Vector3(0.6f, 0f, 0f));
        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: сцена до правки обязана быть валидной");
        return a;
    }

    /// <summary>Отпечаток ВСЕЙ сцены, а не одной детали. Путь, который вернул деталь, но
    /// оставил изменённым соседа (пересобранный пролёт трубы, посаженный на место щит),
    /// отличается от полного отката ровно здесь и больше нигде.</summary>
    private static List<string> SceneFingerprint()
    {
        var all = PartRegistry.GetAll();
        all.Sort((x, y) => string.CompareOrdinal(x.PartName, y.PartName));
        var lines = new List<string>();
        foreach (var el in all)
        {
            if (el == null) continue;
            var bag = UndoableProperties.Capture(el);
            for (int i = 0; i < bag.Count; i++)
                lines.Add($"{el.PartName}.{bag.PropertyAt(i).Name}={bag.ValueAt(i)}");
            lines.Add($"{el.PartName}.dims={el.DimensionsMM}");
            lines.Add($"{el.PartName}.pos={el.transform.position:F4}");
            lines.Add($"{el.PartName}.rot={el.transform.rotation.eulerAngles:F2}");
        }
        return lines;
    }

    private static string Diff(List<string> before, List<string> after)
    {
        var lines = new List<string>();
        int n = Mathf.Min(before.Count, after.Count);
        for (int i = 0; i < n; i++)
            if (before[i] != after[i]) lines.Add($"{before[i]} → {after[i]}");
        if (before.Count != after.Count)
            lines.Add($"размер отпечатка {before.Count} → {after.Count}");
        return lines.Count == 0 ? "(ничего)" : string.Join("; ", lines);
    }

    private ContextMenuUI BuildMenu()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);
        return ctx;
    }

    private T SpawnBehaviour<T>(string name) where T : MonoBehaviour
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go.AddComponent<T>();
    }

    /// <summary>`prepare` строит путь и доводит его до точки, из которой правка ещё не сделана
    /// (панель открыта, жест начат), и возвращает саму правку. Отпечаток снимается между ними,
    /// иначе в разницу попадёт подготовка, а не откат.</summary>
    private void AssertRefusedEditLeavesTheSceneUntouched(string path,
        Func<KitchenElement, Action> prepare)
    {
        var el = SpawnTouchingPair();
        _statusBar!.SimulateAwakeForTests();
        Assert.AreSame(_statusBar, StatusBarUI.Instance,
            "полоса обязана стать текущей сразу после симуляции Awake");

        var perform = prepare(el);
        _statusBar!.ShowTransient("", StatusLevel.Info);
        CommandStack.Clear();

        var before = SceneFingerprint();
        perform();
        var after = SceneFingerprint();

        Assert.IsNotNull(_statusBar!.ActiveText,
            $"{path}: об отказе обязано быть сказано вслух — молчаливый откат читается как "
            + "«не работает». Если здесь null, значит путь либо промолчал, либо правку вообще "
            + "не отклонил");
        Assert.IsTrue(_statusBar!.ActiveText!.StartsWith(EditGate.RefusalPrefix),
            $"{path}: сообщение начинается с причины отказа: {_statusBar!.ActiveText}");
        Assert.IsTrue(_statusBar!.ActiveText!.Contains(IssueCatalog.CodeOverlap),
            $"{path}: названо обязано быть ВНЕСЁННОЕ нарушение, с его кодом: "
            + _statusBar!.ActiveText);

        Assert.AreEqual(before, after,
            $"{path}: отклонённая правка не имеет права оставить в сцене НИ ОДНОГО изменения — "
            + "ни на самой детали, ни на соседе. Осталось: " + Diff(before, after));
        Assert.AreEqual(0, CommandStack.UndoCount,
            $"{path}: ничего не применилось — значит и отменять нечего; запись в стеке означала "
            + "бы, что отмена вернёт состояние, которого пользователь никогда не видел");
    }

    /// <summary>Панели нужен ТРЕТИЙ щит, а двум жестам — нет, и это не подпорка под тест.
    /// Правка размера в панели уводит деталь от конфликта, если он возникает ровно с одной
    /// стороны (`ResizeAnchoring`), — значит с одним соседом COL-01 больше не вносится и
    /// отклонять нечего. Зажатая с двух сторон деталь растёт симметрично от центра, как
    /// раньше, и вход теста снова становится входом. Перетаскивание и ручки размера этого
    /// правила не знают: они двигают деталь туда, куда сказал пользователь.</summary>
    [Test]
    public void PanelApply_RefusedForAViolation_RevertsEverythingAndNamesIt()
    {
        AssertRefusedEditLeavesTheSceneUntouched("панель свойств", el =>
        {
            SpawnPart("Mirror", new Vector3(-0.6f, 0f, 0f));
            Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
                "предусловие: три щита в ряд — сцена всё ещё валидна");
            var ctx = BuildMenu();
            ctx.Open(el);
            return () =>
            {
                ctx.SetWidthFieldTextForTests("1200");
                ctx.SimulateApplyForTests();
            };
        });
    }

    [Test]
    public void Drag_RefusedForAViolation_RevertsEverythingAndNamesIt()
    {
        AssertRefusedEditLeavesTheSceneUntouched("перетаскивание", el =>
        {
            var mover = SpawnBehaviour<ElementMover>("ElementMover");
            mover.BeginDragOn(el);
            return () =>
            {
                el.transform.position = new Vector3(0.3f, 0f, 0f);
                mover.FinishDragNow();
            };
        });
    }

    [Test]
    public void HandleResize_RefusedForAViolation_RevertsEverythingAndNamesIt()
    {
        AssertRefusedEditLeavesTheSceneUntouched("ручки размера", el =>
        {
            var handles = SpawnBehaviour<ResizeHandleManager>("ResizeHandleManager");
            handles.BeginDragOn(el, 0);
            return () =>
            {
                el.DimensionsMM = new Vector3Int(1200, 700, 18);
                handles.FinishDragNow();
            };
        });
    }

    /// <summary>Положительный контроль для третьего пути — у двух других он уже есть в
    /// `BlockedApplyRevertsEverythingTests`. Без него зелёный тест выше значил бы «ресайз через
    /// ручки не делает вообще ничего»: отпечаток тогда совпадает сам собой, а отказ пришлось бы
    /// объяснять чем угодно.</summary>
    [Test]
    public void HandleResize_WithoutBlocking_IsAppliedAndUndoable()
    {
        var el = SpawnTouchingPair();
        KitchenSettings.Instance.BlockOnViolation = false;
        var handles = SpawnBehaviour<ResizeHandleManager>("ResizeHandleManager");
        handles.BeginDragOn(el, 0);
        CommandStack.Clear();

        el.DimensionsMM = new Vector3Int(1200, 700, 18);
        handles.FinishDragNow();

        Assert.AreEqual(1200, el.DimensionsMM.x,
            "без блокировки ресайз обязан остаться в сцене");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "применённый ресайз обязан лечь в отмену ровно одним шагом");
    }

    /// <summary>Противоположный вход, тот же, что уже защищает панель и перетаскивание: деталь,
    /// которая нарушала ДО жеста (одинокая — не на что опереться), обязана остаться
    /// растягиваемой. Шлюз, умеющий только отказывать, отобрал бы у неё последний способ
    /// починиться.</summary>
    [Test]
    public void HandleResize_OfAPartThatAlreadyViolated_IsAppliedEvenWithBlockingOn()
    {
        var el = SpawnPart("Lonely", Vector3.zero);
        Assert.IsFalse(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: одинокая деталь нарушает и до правки — на этом весь тест и держится");

        var handles = SpawnBehaviour<ResizeHandleManager>("ResizeHandleManager");
        handles.BeginDragOn(el, 0);
        CommandStack.Clear();

        el.DimensionsMM = new Vector3Int(900, 700, 18);
        handles.FinishDragNow();

        Assert.AreEqual(900, el.DimensionsMM.x,
            "нарушение было и осталось тем же — растягивание блокировать не за что");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "применённый ресайз обязан лечь в отмену");
    }
}

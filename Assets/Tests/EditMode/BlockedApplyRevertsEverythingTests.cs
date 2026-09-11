using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Дефект D3, который пользователь ловил руками. Применение, заблокированное
/// нарушением (`BlockOnViolation`), возвращало ТРИ поля из дюжины: `DimensionsMM`, позицию и
/// поворот. А выше по `ContextMenuUI.Apply` уже отработали редакторы типов, выбор второго
/// слота материала, зазоры, толщина кромки и `ApplyAfterPosition` — всё это оставалось в
/// сцене, панель показывала старую геометрию, и «отменить» отменяло не то, что применилось.
///
/// Правильный откат — не список полей, который кто-то вспомнил, а снимок: `UndoableProperties`
/// до применения плюс `CommandStack.EndCapture(commit: false)`, который откатывает КАЖДУЮ
/// команду, выполненную внутри захвата. Тесты сравнивают снимок целиком и считают записи
/// отмены, а не миллисекунды.
///
/// Оба теста раньше строились на ОДИНОКОЙ детали: она нарушает («не на что опереться») и до
/// правки, и после, — и этим включали блокировку без подпорок. Ровно этот вход и оказался
/// дефектом: шлюз спрашивал «нарушает ли деталь вообще», а не «внесла ли нарушение ЭТА
/// правка», поэтому деталь, которая уже нарушает, нельзя было починить через панель — весь
/// ввод откатывался целиком (conventions/CORRECTNESS.md → «A gate that can only refuse must
/// have somewhere to fall back to»). Поэтому одинокая деталь переехала в противоположный
/// вход: теперь она проверяет, что правка ПРОХОДИТ и ложится в отмену. Блокировку включает
/// пара щитов в контакте грань-в-грань (сцена валидна) и правка, которая их пересекает.</summary>
public class BlockedApplyRevertsEverythingTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockBefore;
    private StatusBarUI? _statusBar;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = true;
        _root = new GameObject("BlockedApplyRoot");
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
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        _statusBar = null;
        if (_root != null) Object.DestroyImmediate(_root);
        PartRegistry.Clear();
    }

    private KitchenElement SpawnPart(string name) => SpawnPart(name, Vector3.zero);

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

    /// <summary>Валидная сцена, в которой блокировке есть на что опереться: два щита 600 мм
    /// стоят вплотную, грань в грань, и связаны контактом — ни один не висит в воздухе.
    /// Правка ширины первого до 1200 мм растит его симметрично от центра и вгоняет на 300 мм
    /// в соседа, то есть ВНОСИТ COL-01, которого до правки не было.</summary>
    private (KitchenElement edited, KitchenElement neighbour) SpawnTouchingPair()
    {
        var a = SpawnPart("Щит", Vector3.zero);
        var b = SpawnPart("Сосед", new Vector3(0.6f, 0f, 0f));
        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: сцена до правки обязана быть валидной, иначе тест снова проверяет "
            + "«деталь нарушает вообще», а не «правка внесла нарушение»");
        return (a, b);
    }

    private static List<string> SnapshotOf(KitchenElement el)
    {
        var bag = UndoableProperties.Capture(el);
        var parts = new List<string>();
        for (int i = 0; i < bag.Count; i++)
            parts.Add($"{bag.PropertyAt(i).Name}={bag.ValueAt(i)}");
        parts.Add($"dims={el.DimensionsMM}");
        parts.Add($"pos={el.transform.position:F4}");
        parts.Add($"rot={el.transform.rotation.eulerAngles:F2}");
        return parts;
    }

    private static string Diff(List<string> before, List<string> after)
    {
        var lines = new List<string>();
        int n = Mathf.Min(before.Count, after.Count);
        for (int i = 0; i < n; i++)
            if (before[i] != after[i]) lines.Add($"{before[i]} → {after[i]}");
        return string.Join("; ", lines);
    }

    private ContextMenuUI BuildMenu()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);
        return ctx;
    }

    [Test]
    public void ApplyThatIntroducesAViolation_LeavesNoEditInTheScene_AndNoUndoRecord()
    {
        var (el, _) = SpawnTouchingPair();
        var ctx = BuildMenu();
        ctx.Open(el);
        CommandStack.Clear();

        var before = SnapshotOf(el);

        ctx.SetNameFieldTextForTests("Переименованная");
        ctx.SetWidthFieldTextForTests("1200");
        ctx.SimulateApplyForTests();

        var after = SnapshotOf(el);

        Assert.AreEqual(600, el.DimensionsMM.x,
            "предусловие: блокировка обязана была сработать и откатить геометрию — иначе тест "
            + "проверяет обычное применение");
        Assert.AreEqual(before, after,
            "заблокированное применение не имеет права оставить в сцене НИ ОДНОЙ правки. "
            + "Осталось: " + Diff(before, after));
        Assert.AreEqual(0, CommandStack.UndoCount,
            "ничего не применилось — значит и отменять нечего; запись в стеке означала бы, "
            + "что отмена вернёт состояние, которого пользователь никогда не видел");
    }

    /// <summary>Отказ обязан быть НАЗВАН, иначе поля молча возвращаются к прежним значениям и
    /// пользователь видит только то, что панель «не работает». В статус-баре — код и текст
    /// именно того нарушения, которое внесла правка (COL-01 «Детали пересекаются в объёме»),
    /// а не общая фраза про недопустимое изменение.</summary>
    [Test]
    public void BlockedApply_NamesTheViolationItRefused_InTheStatusBar()
    {
        var (el, _) = SpawnTouchingPair();
        var ctx = BuildMenu();
        ctx.Open(el);

        // EditMode не вызывает MonoBehaviour-колбэки сам по себе (см. c45d2279):
        // `AddComponent` не запускает `Awake`, значит `StatusBarUI.Instance` без
        // явного толчка остаётся null. Тот же приём, что и у PerfMonitor
        // (`SimulateAwakeForTests`, 0db46322).
        _statusBar!.SimulateAwakeForTests();
        Assert.AreSame(_statusBar, StatusBarUI.Instance,
            "полоса обязана стать текущей сразу после симуляции Awake");

        ctx.SetWidthFieldTextForTests("1200");
        _statusBar!.ShowTransient("", KitchenDesigner.Core.Update.StatusLevel.Info);
        ctx.SimulateApplyForTests();

        Assert.AreEqual(600, el.DimensionsMM.x, "предусловие: правка обязана быть отклонена");
        Assert.IsNotNull(_statusBar!.ActiveText, "об отказе обязано быть сказано вслух");
        Assert.IsTrue(_statusBar!.ActiveText!.StartsWith(EditGate.RefusalPrefix),
            "сообщение начинается с причины отказа: " + _statusBar!.ActiveText);
        Assert.IsTrue(_statusBar!.ActiveText!.Contains(
                KitchenDesigner.Core.Analysis.IssueCatalog.CodeOverlap),
            "названо обязано быть ВНЕСЁННОЕ нарушение, с его кодом: " + _statusBar!.ActiveText);
        Assert.IsTrue(_statusBar!.ActiveText!.Contains("Сосед"),
            "и вторая деталь пары, иначе неясно, во что уперлась правка: " + _statusBar!.ActiveText);
    }

    /// <summary>Противоположный вход и главный смысл правки: деталь, которая нарушала ДО
    /// правки (одинокая — не на что опереться), обязана оставаться редактируемой. Иначе
    /// починить её нельзя вовсе — шлюз, умеющий только отказывать, отбирает весь ввод, а
    /// нарушение остаётся на месте.</summary>
    [Test]
    public void ApplyOnAPartThatAlreadyViolated_IsAppliedAndUndoable_EvenWithBlockingOn()
    {
        var el = SpawnPart("Одинокая");
        Assert.IsFalse(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: одинокая деталь нарушает и до правки — на этом весь тест и держится");

        var ctx = BuildMenu();
        ctx.Open(el);
        CommandStack.Clear();

        ctx.SetNameFieldTextForTests("Переименованная");
        ctx.SetWidthFieldTextForTests("900");
        ctx.SimulateApplyForTests();

        Assert.AreEqual(900, el.DimensionsMM.x,
            "нарушение было и осталось тем же — блокировать эту правку не за что");
        Assert.AreEqual("Pereimenovannaya", el.PartName,
            "имя обязано примениться вместе с шириной — транслитом, как велит ElementNaming.Rule");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "применённая правка обязана лечь в отмену одним шагом");
    }

    /// <summary>Положительный контроль: без блокировки те же самые правки обязаны примениться
    /// и лечь в стек одним шагом. Иначе зелёный выше значил бы «панель не работает вообще».
    ///
    /// Имя проходит через `ElementNaming.Normalize` (см. `ElementNaming.Rule`): в имени детали
    /// разрешены только латиница, цифры, `-` и `_`, потому что по имени деталь адресуется из
    /// MCP и из сохранения. Поэтому набранное кириллицей «Переименованная» применяется
    /// транслитом — и ждём мы здесь именно транслит, а не исходную строку.</summary>
    [Test]
    public void SameEditWithoutBlocking_IsAppliedAndUndoable()
    {
        var el = SpawnPart("Деталь");
        KitchenSettings.Instance.BlockOnViolation = false;
        var ctx = BuildMenu();
        ctx.Open(el);
        CommandStack.Clear();

        ctx.SetNameFieldTextForTests("Переименованная");
        ctx.SetWidthFieldTextForTests("900");
        ctx.SimulateApplyForTests();

        Assert.AreEqual(900, el.DimensionsMM.x, "без блокировки ширина обязана примениться");
        Assert.AreEqual("Pereimenovannaya", el.PartName,
            "без блокировки имя обязано примениться — транслитом, как велит ElementNaming.Rule");
        Assert.AreEqual(1, CommandStack.UndoCount, "одна правка — ровно один шаг отмены");
    }

    /// <summary>Тот же откат в перетаскивании: `ElementMover.RevertMoveSet` возвращал позицию и
    /// поворот, но не размеры, хотя жест их меняет (посадка, подгонка пролёта трубы). Жест
    /// обязан быть отклонён по тому же признаку, что и правка в панели: нарушение ВНЕСЕНО
    /// этим жестом.</summary>
    [Test]
    public void BlockedDrag_RevertsDimensionsToo_NotOnlyPositionAndRotation()
    {
        var (el, _) = SpawnTouchingPair();
        var moverGo = new GameObject("ElementMover");
        _spawned.Add(moverGo);
        var mover = moverGo.AddComponent<ElementMover>();

        mover.BeginDragOn(el);
        el.DimensionsMM = new Vector3Int(1200, 700, 18);
        el.transform.position = new Vector3(0.1f, 0f, 0f);
        mover.FinishDragNow();

        Assert.AreEqual(new Vector3Int(600, 700, 18), el.DimensionsMM,
            "жест отменён по нарушению — размеры обязаны вернуться вместе с позицией, "
            + "иначе деталь остаётся раздутой без единой записи отмены");
        Assert.AreEqual(0f, el.transform.position.x, 1e-4f,
            "предусловие: позиция откатывается (её откат был и до правки)");
        Assert.AreEqual(0, CommandStack.UndoCount, "отменённый жест ничего не пишет в стек");
    }

    /// <summary>Тот же противоположный вход для жеста: одинокую деталь (она нарушает и до, и
    /// после) обязано быть можно перетащить. Раньше жест откатывался целиком, и деталь,
    /// висящую в воздухе, нельзя было подвинуть к опоре — то есть починить.</summary>
    [Test]
    public void DragOfAPartThatAlreadyViolated_IsAppliedAndUndoable_EvenWithBlockingOn()
    {
        var el = SpawnPart("Одинокая");
        var moverGo = new GameObject("ElementMover");
        _spawned.Add(moverGo);
        var mover = moverGo.AddComponent<ElementMover>();

        mover.BeginDragOn(el);
        el.transform.position = new Vector3(2f, 0f, 0f);
        mover.FinishDragNow();

        Assert.AreEqual(2f, el.transform.position.x, 1e-4f,
            "нарушение было и осталось тем же — жест блокировать не за что, иначе деталь "
            + "в воздухе нельзя подвинуть к опоре");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "применённый жест обязан лечь в отмену");
    }
}

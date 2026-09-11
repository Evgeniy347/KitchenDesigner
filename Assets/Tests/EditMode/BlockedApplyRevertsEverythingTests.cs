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
/// ТРОЙКА щитов в контакте грань-в-грань (сцена валидна) и правка, которая растит средний в
/// оба соседа сразу: с одним соседом панель теперь уводит деталь от конфликта вместо того,
/// чтобы его вносить (`ResizeAnchoring`), и отклонять стало нечего — этот обычный случай
/// проверяется здесь же, рядом с отказом.</summary>
public class BlockedApplyRevertsEverythingTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private StatusBarUI? _statusBar;
    private GameObject? _menuRoot;
    private ContextMenuUI? _menu;
    private ProjectLoadStateGuard? _globals;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — ~0,31 с, а
    /// четыре теста строили её заново, хотя продукт собирает панель единожды и дальше
    /// только переоткрывает (<see cref="ContextMenuLayoutTests"/>). Через <c>Open</c>
    /// здесь проходит каждый тест, который панель трогает, — а <c>Open</c> и есть тот
    /// сброс, которым живёт боевой сценарий.
    ///
    /// Холст панели — СВОЙ, отдельно от потестового <c>_root</c>: на <c>_root</c> висит
    /// <see cref="StatusBarUI"/>, которому нужен свежий экземпляр в каждом тесте, и
    /// пережить тест он не может. Своего <c>SelectionManager</c> класс не заводит.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        _menuRoot = new GameObject("BlockedApplyMenuRoot");
        var canvas = _menuRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _menuRoot.AddComponent<CanvasScaler>();
        _menuRoot.AddComponent<GraphicRaycaster>();
        var go = new GameObject("Ctx");
        go.transform.SetParent(_menuRoot.transform);
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_menuRoot.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menuRoot != null) Object.DestroyImmediate(_menuRoot);
        _menu = null;
        _menuRoot = null;
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего, а весь класс только и делает, что применяет
    /// правки. <c>DisarmAll</c> снимает взвод кнопок удаления, а фокус — потому что
    /// <c>RefreshUnfocused</c> МОЛЧА пропускает сфокусированное поле.</summary>
    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        PartRegistry.Clear();
        CommandStack.Clear();
        KitchenSettings.Instance.BlockOnViolation = true;
        _root = new GameObject("BlockedApplyRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();
        _statusBar = _root.AddComponent<StatusBarUI>();
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО уничтожения спавнов: он обнуляет
    /// <c>_target</c> панели, иначе живая панель уехала бы в следующий тест с
    /// уничтоженной деталью в руках.</summary>
    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
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
        _globals!.Restore();
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

    /// <summary>Та же валидная сцена, но щиты стоят с ОБЕИХ сторон. Так вход остался входом:
    /// с одним соседом правка ширины до 1200 мм больше не вносит COL-01 вовсе — панель теперь
    /// уводит деталь от единственного конфликта (`ResizeAnchoring`), и шлюзу нечего отклонять.
    /// Зажатая с двух сторон деталь растёт по-прежнему симметрично от центра и вгоняет по
    /// 300 мм в каждого соседа — блокировке снова есть на что опереться, а заодно это
    /// сценовая проверка правила «конфликт с обеих сторон — поведение прежнее».
    /// Левый щит назван так, чтобы подстрока «Сосед» нашлась в тексте отказа независимо от
    /// того, какую из двух пар шлюз назовёт первой.</summary>
    private (KitchenElement edited, KitchenElement neighbour) SpawnPinnedTriple()
    {
        var (a, b) = SpawnTouchingPair();
        SpawnPart("Сосед-слева", new Vector3(-0.6f, 0f, 0f));
        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: три щита в ряд — сцена всё ещё валидна, отклонять пока нечего");
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

    [Test]
    public void ApplyThatIntroducesAViolation_LeavesNoEditInTheScene_AndNoUndoRecord()
    {
        var (el, _) = SpawnPinnedTriple();
        var ctx = _menu!;
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
        var (el, _) = SpawnPinnedTriple();
        var ctx = _menu!;
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

    /// <summary>Обычный случай рядом с отказом: сосед ОДИН, и та же правка ширины до 1200 мм
    /// теперь не отклоняется, а уводит деталь от конфликта на половину прироста — правая грань
    /// остаётся там же, где была, весь прирост уходит влево. Шлюз при этом молчит, потому что
    /// нарушения не внесено; смещение считается ДО шлюза именно затем, чтобы тот судил то
    /// состояние, которое увидит пользователь, а не промежуточное.
    ///
    /// И размер, и смещение обязаны откатываться ОДНИМ Ctrl+Z: они едут в одной
    /// `ResizeCommand`, а не двумя.</summary>
    [Test]
    public void ApplyThatWouldOverlapOnOneSide_ShiftsAwayInsteadOfBeingRefused()
    {
        var (el, _) = SpawnTouchingPair();
        var ctx = _menu!;
        ctx.Open(el);
        CommandStack.Clear();

        ctx.SetWidthFieldTextForTests("1200");
        ctx.SimulateApplyForTests();

        Assert.AreEqual(1200, el.DimensionsMM.x,
            "конфликт возникал ровно с одной стороны — правку положено применить, а не отклонить");
        Assert.AreEqual(-0.3f, el.transform.position.x, 1e-4f,
            "деталь обязана уйти на половину прироста от соседа, чтобы правая грань осталась "
            + "на месте");
        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "смещение затем и считается, чтобы сцена осталась валидной");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "размер и смещение — одна правка и один шаг отмены");

        CommandStack.Undo();

        Assert.AreEqual(600, el.DimensionsMM.x, "отмена возвращает размер");
        Assert.AreEqual(0f, el.transform.position.x, 1e-4f,
            "и позицию тем же шагом — иначе деталь осталась бы смещённой без причины");
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

        var ctx = _menu!;
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
        var ctx = _menu!;
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

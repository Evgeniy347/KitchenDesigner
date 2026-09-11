using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Шлюз правки судил в ДВУХ радиусах: панель свойств спрашивала про нарушение на
/// самой детали, перетаскивание и ручки размера — про нарушение в радиусе
/// <c>snapThreshold × 2</c>. Один и тот же по смыслу запрет срабатывал на жесте и молчал в
/// панели, и пользователь видел разное поведение на одну и ту же правку.
///
/// Радиус сведён к ШИРОКОМУ, и это осознанная смена поведения продукта, а не рефакторинг.
/// Причина: узкий радиус — не более строгое правило, а более СЛЕПОЕ. Нарушение, которое
/// правка вносит, почти всегда парное: пересечение диагностика кладёт в снимок обеим
/// деталям, поэтому узкий радиус ловит его всё равно. А единственный класс нарушений, который
/// он пропускает, — тот, где деталь ломает СОСЕДА, не ломаясь сама: сосед остался без опоры
/// (COL-02), потому что правка увела из-под него единственный контакт. Сузить радиус до
/// детали значило бы снять этот запрет и с жестов — то есть отнять защиту, а не добавить
/// строгости.
///
/// Поэтому здесь два ПРОТИВОПОЛОЖНЫХ входа, и каждый прогоняется по всем трём путям:
/// правка, ломающая саму деталь (COL-01, пересечение с соседями), и правка, ломающая соседа
/// в пределах радиуса (COL-02 на соседе, сама деталь чиста). Совпадать обязаны оба исхода:
/// разъедься радиусы снова — второй вход покраснеет ровно на том пути, который отстал.
///
/// Сцена одна на оба входа и подобрана так, чтобы вход остался входом при
/// <c>ResizeAnchoring</c> (см. `conventions/CORRECTNESS.md`): деталь зажата щитами с обеих
/// сторон по X, поэтому правка ширины растит её симметрично от центра и уводить её от
/// конфликта некуда; по Y соседей в контакте нет, поэтому правка высоты тоже считается от
/// центра, и обе грани по высоте едут к центру одинаково.</summary>
public class EditGateSingleRadiusTests
{
    private const string Focus = "Sredniy";
    private const string SideNeighbour = "Levyi";
    private const string ShelfNeighbour = "Polka";

    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockBefore;
    private float _thresholdBefore;
    private ResizeHandleManager.HandleMode _modeBefore;
    private StatusBarUI? _statusBar;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        _thresholdBefore = KitchenSettings.Instance.SnapThreshold;
        KitchenSettings.Instance.BlockOnViolation = true;
        KitchenSettings.Instance.SnapThreshold = KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM;
        _modeBefore = ResizeHandleManager.Mode;
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        _root = new GameObject("EditGateRadiusRoot");
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
        KitchenSettings.Instance.SnapThreshold = _thresholdBefore;
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

    private KitchenElement Spawn(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        PartRegistry.Register(el);
        return el;
    }

    /// <summary>Три детали, сцена валидна. «Левый» и правимая деталь — щиты 600×700×18 грань
    /// в грань по X. «Полка» 600×100×18 приставлена к правой грани правимой детали своей
    /// верхней третью (диапазон по высоте 280…380 мм при её 350 мм) — перекрытие граней 70 из
    /// 100 мм, то есть настоящий контакт, а не касание вскользь. Держится полка ТОЛЬКО на
    /// правимой детали: уведи её грань вниз — и полке не на чем стоять, хотя сама правимая
    /// деталь останется в контакте с «Левым» и чистой.</summary>
    private KitchenElement SpawnScene()
    {
        Spawn(SideNeighbour, new Vector3Int(600, 700, 18), Vector3.zero);
        var mid = Spawn(Focus, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
        Spawn(ShelfNeighbour, new Vector3Int(600, 100, 18), new Vector3(1.2f, 0.33f, 0f));

        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).isValid,
            "предусловие: до правки сцена обязана быть валидной, иначе тест проверяет "
            + "«деталь нарушает вообще», а не «правка внесла нарушение»");
        return mid;
    }

    private static KitchenElement Part(string name) =>
        PartRegistry.GetAll().First(e => e.PartName == name);

    private static List<string> ViolatingNames() =>
        ConstraintValidator.Validate(PartRegistry.GetAll()).violations
            .Where(e => e != null).Select(e => e.PartName).OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

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

    private void ArmStatusBar()
    {
        _statusBar!.SimulateAwakeForTests();
        Assert.AreSame(_statusBar, StatusBarUI.Instance,
            "полоса обязана стать текущей сразу после симуляции Awake");
        _statusBar!.ShowTransient("", StatusLevel.Info);
    }

    private void AssertRefused(string path, string expectedCode, string expectedSubject,
        KitchenElement mid, Vector3Int dimsBefore, Vector3 posBefore)
    {
        Assert.IsNotNull(_statusBar!.ActiveText,
            $"{path}: об отказе обязано быть сказано вслух — молчаливый откат читается как "
            + "«инструмент не работает»");
        Assert.IsTrue(_statusBar!.ActiveText!.StartsWith(EditGate.RefusalPrefix),
            $"{path}: сообщение начинается с причины отказа: {_statusBar!.ActiveText}");
        Assert.IsTrue(_statusBar!.ActiveText!.Contains(expectedCode),
            $"{path}: названо обязано быть ВНЕСЁННОЕ нарушение, с его кодом {expectedCode}: "
            + _statusBar!.ActiveText);
        Assert.IsTrue(_statusBar!.ActiveText!.Contains(expectedSubject),
            $"{path}: и деталь, из-за которой правка отклонена ({expectedSubject}): "
            + _statusBar!.ActiveText);
        Assert.AreEqual(dimsBefore, mid.DimensionsMM,
            $"{path}: отклонённая правка обязана вернуть размеры");
        Assert.AreEqual(posBefore.x, mid.transform.position.x, 1e-4f,
            $"{path}: и позицию по X");
        Assert.AreEqual(posBefore.y, mid.transform.position.y, 1e-4f,
            $"{path}: и позицию по Y");
        Assert.AreEqual(0, CommandStack.UndoCount,
            $"{path}: ничего не применилось — отменять нечего");
    }

    // ─ Вход 1: правка ломает САМУ деталь (пересечение с обоими соседями) ─

    [Test]
    public void PanelApply_EditThatBreaksThePartItself_IsRefused()
    {
        var mid = SpawnScene();
        var ctx = BuildMenu();
        ctx.Open(mid);
        ArmStatusBar();
        CommandStack.Clear();

        ctx.SetWidthFieldTextForTests("720");
        ctx.SimulateApplyForTests();

        AssertRefused("панель свойств", IssueCatalog.CodeOverlap, Focus,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    [Test]
    public void Drag_EditThatBreaksThePartItself_IsRefused()
    {
        var mid = SpawnScene();
        var mover = SpawnBehaviour<ElementMover>("ElementMover");
        mover.BeginDragOn(mid);
        ArmStatusBar();
        CommandStack.Clear();

        mid.transform.position = new Vector3(0.54f, 0f, 0f);
        mover.FinishDragNow();

        AssertRefused("перетаскивание", IssueCatalog.CodeOverlap, Focus,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    [Test]
    public void HandleResize_EditThatBreaksThePartItself_IsRefused()
    {
        var mid = SpawnScene();
        var handles = SpawnBehaviour<ResizeHandleManager>("ResizeHandleManager");
        handles.BeginDragOn(mid, 0);
        ArmStatusBar();
        CommandStack.Clear();

        mid.DimensionsMM = new Vector3Int(720, 700, 18);
        handles.FinishDragNow();

        AssertRefused("ручки размера", IssueCatalog.CodeOverlap, Focus,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    // ─ Вход 2: правка ломает СОСЕДА в пределах радиуса, сама деталь чиста ─

    [Test]
    public void PanelApply_EditThatBreaksANeighbourWithinTheRadius_IsRefused()
    {
        var mid = SpawnScene();
        var ctx = BuildMenu();
        ctx.Open(mid);
        ArmStatusBar();
        CommandStack.Clear();

        ctx.SetHeightFieldTextForTests("560");
        ctx.SimulateApplyForTests();

        AssertRefused("панель свойств", IssueCatalog.CodeUnsupported, ShelfNeighbour,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    [Test]
    public void Drag_EditThatBreaksANeighbourWithinTheRadius_IsRefused()
    {
        var mid = SpawnScene();
        var mover = SpawnBehaviour<ElementMover>("ElementMover");
        mover.BeginDragOn(mid);
        ArmStatusBar();
        CommandStack.Clear();

        mid.transform.position = new Vector3(0.6f, -0.12f, 0f);
        mover.FinishDragNow();

        AssertRefused("перетаскивание", IssueCatalog.CodeUnsupported, ShelfNeighbour,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    [Test]
    public void HandleResize_EditThatBreaksANeighbourWithinTheRadius_IsRefused()
    {
        var mid = SpawnScene();
        var handles = SpawnBehaviour<ResizeHandleManager>("ResizeHandleManager");
        handles.BeginDragOn(mid, 0);
        ArmStatusBar();
        CommandStack.Clear();

        mid.DimensionsMM = new Vector3Int(600, 560, 18);
        handles.FinishDragNow();

        AssertRefused("ручки размера", IssueCatalog.CodeUnsupported, ShelfNeighbour,
            mid, new Vector3Int(600, 700, 18), new Vector3(0.6f, 0f, 0f));
    }

    /// <summary>Положительный контроль ко второму входу, и он же доказательство, что вход
    /// именно такой, каким назван. Без блокировки та же правка применяется — и оставляет без
    /// опоры ТОЛЬКО полку: правимая деталь в список нарушителей не попадает. Значит отказ выше
    /// даёт широкий радиус, а не совпадение: узкому радиусу, который спрашивает лишь про саму
    /// деталь, здесь нечего было бы найти.</summary>
    [Test]
    public void WithoutBlocking_TheNeighbourBreakingEdit_LeavesOnlyTheNeighbourViolating()
    {
        var mid = SpawnScene();
        KitchenSettings.Instance.BlockOnViolation = false;
        var ctx = BuildMenu();
        ctx.Open(mid);
        CommandStack.Clear();

        ctx.SetHeightFieldTextForTests("560");
        ctx.SimulateApplyForTests();

        Assert.AreEqual(560, mid.DimensionsMM.y,
            "без блокировки правка высоты обязана примениться — иначе контроль проверяет не то");
        Assert.AreEqual(new List<string> { ShelfNeighbour }, ViolatingNames(),
            "правка обязана оставить без опоры ровно соседа и никого больше: если сюда попала "
            + "и сама правимая деталь, вход перестал быть «ломает соседа» и тесты выше зелены "
            + "по другой причине");
    }

    /// <summary>Тот же вход на уровне самого шлюза, без панелей и жестов: снимок до, снимок
    /// после, и вопрос «внесено ли нарушение». Шлюз обязан назвать СОСЕДА — это и есть то,
    /// чего узкий радиус не видел.</summary>
    [Test]
    public void TheGate_NamesTheNeighbourItRefusedFor_NotTheEditedPart()
    {
        var mid = SpawnScene();
        var before = SceneViolations.OfScene();
        mid.DimensionsMM = new Vector3Int(600, 560, 18);
        var after = SceneViolations.OfScene();

        bool refused = EditGate.Introduced(before, after,
            new List<KitchenElement> { mid }, out var introduced);

        Assert.IsTrue(refused,
            "правка увела опору из-под соседа в пределах радиуса — шлюз обязан отказать");
        Assert.AreEqual(ShelfNeighbour, introduced.element.PartName,
            "названо обязано быть нарушение СОСЕДА: на самой правимой детали его нет");
        Assert.AreEqual(ViolationKind.Unsupported, introduced.kind,
            "и это COL-02 «не на что опереться», а не пересечение");
    }

    /// <summary>Обе формы входа в шлюз — одиночная деталь (панель) и набор (жест) — обязаны
    /// давать один ответ на один вход. Разъедься радиусы снова, разойдутся и эти два ответа.
    /// </summary>
    [Test]
    public void BothEntryShapes_JudgeTheSameEditTheSameWay()
    {
        var mid = SpawnScene();
        var before = SceneViolations.OfScene();
        mid.DimensionsMM = new Vector3Int(600, 560, 18);
        var after = SceneViolations.OfScene();

        bool single = EditGate.Refuses(before, after, mid, out string singleText);
        bool set = EditGate.Refuses(before, after,
            new List<KitchenElement> { mid }, out string setText);

        Assert.AreEqual(set, single,
            "деталь и набор из неё одной — один и тот же фокус; разный исход означает, что "
            + "радиусов снова два");
        Assert.AreEqual(setText, singleText,
            "и текст отказа обязан совпадать до символа, иначе пользователь получит разные "
            + "объяснения одной правке");
    }

    /// <summary>Сторож единственности: радиус нельзя ПЕРЕДАТЬ в шлюз. Пока ни один публичный
    /// метод не принимает его параметром, вызывающая сторона физически не может выбрать себе
    /// радиус — вопрос «каким радиусом судим» задаётся только внутри
    /// <c>EditGate.RadiusUnits</c>.</summary>
    [Test]
    public void NoCaller_CanChooseTheRadius()
    {
        var methods = typeof(EditGate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToList();

        Assert.IsTrue(methods.Any(m => m.Name == nameof(EditGate.Refuses)),
            "скан обязан видеть сам шлюз: без Refuses он проверяет пустоту");

        var withRadius = methods
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(float)))
            .Select(m => m.Name).ToList();

        Assert.IsEmpty(withRadius,
            "шлюз не имеет права брать радиус снаружи: " + string.Join(", ", withRadius)
            + ". Именно так панель и жесты и разъехались — каждый считал радиус у себя");

        var radius = typeof(EditGate).GetProperty(nameof(EditGate.RadiusUnits),
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(radius, "и ровно одно место, где радиус вычисляется, — RadiusUnits");
    }

    /// <summary>Второй сторож, со стороны исходников: выражение радиуса
    /// (<c>SnapThreshold * 2</c>) живёт в одном файле. Пока оно там одно, «каким радиусом
    /// судим» — один вопрос; появись второе, здесь покраснеет с именем файла.</summary>
    [Test]
    public void TheRadiusExpression_LivesInExactlyOneFile()
    {
        var dir = ProductionSourceDir();
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);

        Assert.Greater(files.Length, 200,
            "скан обязан видеть слой целиком: путь, по которому ничего не нашлось, "
            + "зеленеет сам собой и не проверяет ничего");
        Assert.IsTrue(files.Any(f => Path.GetFileName(f) == "EditGate.cs"),
            "и обязан видеть сам EditGate.cs — иначе он ищет не там");

        var pattern = new Regex(@"SnapThreshold\s*\*\s*2", RegexOptions.Compiled);
        var carriers = files.Where(f => pattern.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.AreEqual(new List<string?> { "EditGate.cs" }, carriers,
            "радиус шлюза считается в одном месте. Нашлось: " + string.Join(", ", carriers));
    }

    private static string ProductionSourceDir()
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(EditGateSingleRadiusTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Core");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Не найден Assets/Scripts/Core ни от одной из точек: " + string.Join(", ", roots));
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Страж правила UI-GUIDELINES §9 «Кнопка, нажатие которой в этом состоянии ничего не
/// сделает, выключена» для окон: «Этажи», «Загрузить», «Сцена», «Ошибки», «Спецификация» и панели
/// свойств элемента (по ВСЕМ типам элементов). Контракт тот же, что у тулбара
/// (<see cref="NoOpButtonContract"/>): каждая кнопка окна названа либо «бывает пустой» с условием —
/// тогда <c>interactable</c> обязан ему следовать в каждом из проверяемых состояний, — либо «всегда
/// исполнима» с причиной. Новая кнопка вне таблицы роняет тест. Окно «Настройки» и выпадающие списки
/// сюда не входят.</summary>
public class WindowNoOpButtonsGuardTests
{
    private GameObject? _canvasGo;
    private GameObject? _host;
    private string[]? _recentBackup;
    private string? _lastPathBackup;
    private readonly List<string> _tempFiles = new List<string>();

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        CommandStack.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        _lastPathBackup = SaveLoadManager.LastPath;
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        _host = new GameObject("WindowHost");
    }

    [TearDown]
    public void TearDown()
    {
        if (_recentBackup != null)
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = _recentBackup;
        SaveLoadManager.LastPath = _lastPathBackup!;
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _tempFiles.Clear();
        if (_host != null) Object.DestroyImmediate(_host);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        EveryElementType.ClearScene();
        GroupManager.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    private static string ConfirmedMessage(List<string> violations) =>
        "Правило UI-GUIDELINES §9: кнопка, нажатие которой в этом состоянии ничего не сделает, выключена. "
        + "Нарушения: " + string.Join(" | ", violations);

    private List<string> Check(NoOpButtonContract contract, Transform root, string state) =>
        contract.Violations(root.GetComponentsInChildren<Button>(true), state);

    // ── «Этажи» ─────────────────────────────────────────────

    private static readonly NoOpButtonContract LevelsContract = new NoOpButtonContract()
        .Always("CloseBtn", "закрыть окно возможно всегда")
        .Always("LvAdd", "новый этаж над верхним добавляется всегда")
        .NoOpWhen("LvDelete_.*", _ => LevelRegistry.Snapshot().Length <= 1);

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void LevelsWindow_ButtonsFollowTheContract(int levels)
    {
        var list = new List<Level>();
        for (int i = 0; i < levels; i++)
            list.Add(new Level((i + 1).ToString(), (i + 1) + " этаж", i * 3000, 3000));
        LevelRegistry.Set(list);
        var ui = _canvasGo!.AddComponent<LevelsWindowUI>();
        ui.Build(_canvasGo.transform);
        ui.SetVisible(true);

        var violations = Check(LevelsContract, _canvasGo.transform, levels + " этаж(ей)");

        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }

    // ── «Загрузить» ─────────────────────────────────────────

    private static readonly NoOpButtonContract LoadContract = new NoOpButtonContract()
        .Always("CloseBtn", "закрыть окно возможно всегда")
        .Always("LoadNewProject", "диалог нового проекта открывается всегда")
        .Always("LoadOpenFile", "диалог выбора файла открывается всегда")
        .NoOpWhen("Row", RowOfAMissingProject);

    private static bool RowOfAMissingProject(Button row)
    {
        var title = row.transform.Find("Title");
        return title != null && title.GetComponent<TMP_Text>().text.StartsWith(Loc.T("window.load.notFound"));
    }

    private string MakeProjectFile(string name)
    {
        string path = Path.Combine(Application.temporaryCachePath, name);
        File.WriteAllText(path, "{\"version\":1,\"appVersion\":\"" + BuildInfo.Version + "\"}");
        _tempFiles.Add(path);
        return path;
    }

    private LoadProjectWindowUI BuildLoadWindowWithRecent(params string[] paths)
    {
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = paths;
        var ui = _canvasGo!.AddComponent<LoadProjectWindowUI>();
        ui.Build(_canvasGo.transform);
        ui.SetVisible(true);
        return ui;
    }

    [Test]
    public void LoadWindow_NoRecentProjects_ButtonsFollowTheContract()
    {
        SaveLoadManager.LastPath = "";
        BuildLoadWindowWithRecent();

        var violations = Check(LoadContract, _canvasGo!.transform, "нет недавних");

        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }

    [Test]
    public void LoadWindow_ExistingAndMissingRecentProjects_ButtonsFollowTheContract()
    {
        string existing = MakeProjectFile("wnog_existing.kdproj");
        string missing = Path.Combine(Application.temporaryCachePath, "wnog_gone.kdproj");
        if (File.Exists(missing)) File.Delete(missing);
        BuildLoadWindowWithRecent(existing, missing);

        var rows = new List<Button>();
        foreach (var button in _canvasGo!.GetComponentsInChildren<Button>(true))
            if (button.name == "Row") rows.Add(button);
        Assume.That(rows.Count, Is.EqualTo(2), "предпосылка: по строке на каждый недавний проект");

        var violations = Check(LoadContract, _canvasGo.transform, "есть и пропавший проект");

        Assert.IsEmpty(violations, ConfirmedMessage(violations));
        Assert.AreEqual(1, rows.FindAll(r => !r.interactable).Count,
            "ровно строка пропавшего файла выключена: её нажатие раньше молча ничего не делало");
        var missingRow = rows.Find(r => !r.interactable)!;
        Assert.AreEqual(UIStyle.HighlightError, missingRow.transform.Find("Title")!.GetComponent<TMP_Text>().color,
            "строка пропавшего файла выключена, но остаётся КРАСНОЙ: выключена не значит серая");
    }

    // ── «Сцена» ─────────────────────────────────────────────

    private static readonly NoOpButtonContract HierarchyContract = new NoOpButtonContract()
        .Always("CloseBtn", "закрыть окно возможно всегда")
        .Always("HierAddGroup", "пустую группу можно создать всегда")
        .Always("Fold", "кнопка есть только у узла с детьми — сворачивать есть что")
        .Always("Main", "строка выделяет свои элементы; корень «Кухня» тоже выделяет сцену")
        .Always("GroupMenu", "у группы без участников «…» её распускает — действие есть");

    private KitchenElement MakeElement(string name)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(element);
        return element;
    }

    private HierarchyPanelUI BuildHierarchy()
    {
        var ui = _host!.AddComponent<HierarchyPanelUI>();
        ui.Build(_canvasGo!.transform);
        ui.SetVisible(true);
        return ui;
    }

    [Test]
    public void HierarchyWindow_EmptyScene_ButtonsFollowTheContract()
    {
        BuildHierarchy();

        var violations = Check(HierarchyContract, _canvasGo!.transform, "пустая сцена");

        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }

    [Test]
    public void HierarchyWindow_GroupsAndElements_ButtonsFollowTheContract()
    {
        var a = MakeElement("А");
        var b = MakeElement("Б");
        var group = GroupManager.Link(new List<KitchenElement> { a, b });
        GroupManager.Rename(group!, "Модуль");
        GroupManager.Create("Пустая");
        BuildHierarchy();

        var violations = Check(HierarchyContract, _canvasGo!.transform, "группы и элементы");

        Assert.IsEmpty(violations, ConfirmedMessage(violations));
        foreach (var go in new[] { a.gameObject, b.gameObject })
            Object.DestroyImmediate(go);
    }

    // ── «Ошибки» ────────────────────────────────────────────

    private static readonly NoOpButtonContract ErrorsContract = new NoOpButtonContract()
        .Always("Flt(Level|Code|Floor)", "выпадающий список фильтра: компонент вне этого контракта")
        .Always("CloseBtn", "закрыть окно возможно всегда")
        .Always("Row", "строка находки выделяет её и показывает в сцене")
        .Always("ErrRefresh", "повторный анализ сцены осмыслен всегда: сцена могла измениться");

    [Test]
    public void ErrorsWindow_ButtonsFollowTheContract_WithAndWithoutIssues()
    {
        var panel = _host!.AddComponent<ErrorPanelUI>();
        panel.Build(_canvasGo!.transform);
        panel.SetVisible(true);

        var empty = Check(ErrorsContract, _canvasGo.transform, "нет находок");

        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска А", Vector3.zero);
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска Б", Vector3.zero);
        panel.SetVisible(false);
        panel.SetVisible(true);
        var withIssues = Check(ErrorsContract, _canvasGo.transform, "есть находки");

        Assert.IsEmpty(empty, ConfirmedMessage(empty));
        Assert.IsEmpty(withIssues, ConfirmedMessage(withIssues));
    }

    // ── «Спецификация» ──────────────────────────────────────

    private static readonly NoOpButtonContract SpecContract = new NoOpButtonContract()
        .Always("CloseBtn", "закрыть окно возможно всегда")
        .Always("SpecClose", "закрыть окно возможно всегда")
        .NoOpWhen("SpecExport", _ => SpecificationManager.Build(PartRegistry.All).lines.Count == 0);

    [Test]
    public void SpecificationWindow_EmptyAndFilledScene_ButtonsFollowTheContract()
    {
        var ui = _host!.AddComponent<SpecificationPanelUI>();
        ui.Build(_canvasGo!.transform);
        ui.SetVisible(true);
        var empty = Check(SpecContract, _canvasGo.transform, "пустая сцена");

        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска", Vector3.zero);
        ui.SetVisible(false);
        ui.SetVisible(true);
        var filled = Check(SpecContract, _canvasGo.transform, "в сцене есть детали");

        Assert.IsEmpty(empty, ConfirmedMessage(empty));
        Assert.IsEmpty(filled, ConfirmedMessage(filled));
    }


    // ── Панель свойств элемента ─────────────────────────────

    private static KitchenElement? _ctxTarget;

    private static int SlotOf(Button button) => int.Parse(Regex.Match(button.name, @"\d+$").Value);

    private static bool NoLiveLights() => LightSwitchNetwork.LiveLightNames().Count == 0;

    private static readonly NoOpButtonContract ContextMenuContract = new NoOpButtonContract()
        .Always("CloseBtn", "закрыть панель возможно всегда")
        .Always("CtxRot[XYZ]|CtxRotY180", "кнопки, бессмысленные для детали только с рысканием, скрыты, а не выключены")
        .Always("CtxDup", "копия создаётся для любого элемента")
        .Always("CtxDel", "удалить можно любой элемент")
        .Always("Ctx(Door|OvenDoor|DishwasherDoor|WinDoor|DrawerAnim)", "переключатель открыто/закрыто, работает в обе стороны")
        .Always("CtxDrawerDouble|CtxDrawerRemoveUpper", "строка видна только когда действие применимо: пару можно создать или убрать")
        .Always("CtxEdge[LW][12]", "щелчок по кромке листает её состояние по кругу: результат всегда другой")
        .Always("CtxGaps|CtxGrooves|CtxTextures|CtxLightLinks|CtxLightAdv", "заголовок раздела: разворачивает и сворачивает")
        .Always("CtxTexEdit[0-9]+|CtxTexDel[0-9]+|CtxGrooveDel[0-9]+|CtxLightLinkDel[0-9]+", "кнопка строки, а строка есть только пока есть что править или удалять")
        .Always("CtxTexAdd|CtxGrooveAdd", "на пределе числа или при повторе строки кнопка показывает причину тостом, а не молчит")
        .NoOpWhen("CtxLightLinkAddBtn|CtxLightLinkPick", _ => NoLiveLights())
        .NoOpWhen("CtxTexUp[0-9]+", b => SlotOf(b) == 0)
        .NoOpWhen("CtxTexDown[0-9]+", b => SlotOf(b) >= _ctxTarget!.TextureOverlays.Count - 1);

    private static readonly string[] SectionHeaders =
        { "CtxTextures", "CtxGrooves", "CtxGaps", "CtxLightLinks", "CtxLightAdv" };

    private static void ExpandEverySection(Transform panel)
    {
        foreach (var button in panel.GetComponentsInChildren<Button>(false))
            if (System.Array.IndexOf(SectionHeaders, button.name) >= 0) button.onClick.Invoke();
    }

    private static void CheckPanelInBothStates(ContextMenuUI menu, Transform canvas, string state,
        HashSet<string> distinct)
    {
        var panel = canvas.Find("ContextMenu")!;
        foreach (var v in ContextMenuContract.Violations(panel.GetComponentsInChildren<Button>(true), state))
            distinct.Add(v);
        ExpandEverySection(panel);
        foreach (var v in ContextMenuContract.Violations(panel.GetComponentsInChildren<Button>(true), state + ", разделы развёрнуты"))
            distinct.Add(v);
    }

    [Test]
    public void ContextMenu_EveryElementType_ButtonsFollowTheContract()
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("TestCanvas");
        var menuGo = new GameObject("CtxMenu");
        var menu = menuGo.AddComponent<ContextMenuUI>();
        menu.Build(canvas.transform);
        var distinct = new HashSet<string>();

        try
        {
            foreach (var (type, _) in EveryElementType.Makers)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
                _ctxTarget = element;
                menu.Open(element);
                CheckPanelInBothStates(menu, canvas.transform, "панель свойств", distinct);
                menu.Close();
                EveryElementType.ClearScene();
            }
        }
        finally
        {
            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(canvas.gameObject);
        }

        var violations = new List<string>(distinct);
        violations.Sort();
        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContextMenu_TextureAndGrooveRows_ButtonsFollowTheContract(bool withTextures)
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("TestCanvas");
        var menuGo = new GameObject("CtxMenu");
        var menu = menuGo.AddComponent<ContextMenuUI>();
        menu.Build(canvas.transform);
        var distinct = new HashSet<string>();

        try
        {
            var part = EveryElementType.Spawn(withTextures ? typeof(FloorElement) : typeof(KitchenElement), "Проба_деталь");
            string material = MaterialCatalog.All[0].id;
            if (withTextures)
                part.SetTextureOverlays(new[]
                {
                    TextureOverlaySpec.FullFace((OverlaySide)0, material),
                    TextureOverlaySpec.FullFace((OverlaySide)1, material),
                    TextureOverlaySpec.FullFace((OverlaySide)2, material),
                });
            if (!withTextures)
                part.SetGrooves(new[] { new GrooveSpec(GrooveKind.Through, GrooveSide.Top) });
            _ctxTarget = part;
            menu.Open(part);
            CheckPanelInBothStates(menu, canvas.transform, withTextures ? "пол с тремя текстурами" : "деталь с пазом", distinct);

            var rowButtons = new List<string>();
            foreach (var b in canvas.transform.Find("ContextMenu")!.GetComponentsInChildren<Button>(false))
                rowButtons.Add(b.name);
            if (withTextures)
                CollectionAssert.Contains(rowButtons, "CtxTexUp1", "предпосылка: строки текстур построены и видны");
            if (!withTextures)
                CollectionAssert.Contains(rowButtons, "CtxGrooveDel0", "предпосылка: строка паза построена и видна");
        }
        finally
        {
            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(canvas.gameObject);
        }

        var violations = new List<string>(distinct);
        violations.Sort();
        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContextMenu_LightSwitch_LinkButtonsFollowTheContract(bool withLiveLight)
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("TestCanvas");
        var menuGo = new GameObject("CtxMenu");
        var menu = menuGo.AddComponent<ContextMenuUI>();
        menu.Build(canvas.transform);
        var distinct = new HashSet<string>();

        try
        {
            var sw = EveryElementType.Spawn(typeof(LightSwitchElement), "Проба_выключатель");
            if (withLiveLight)
            {
                var light = ElementFactory.CreateLightSource("Люстра", Vector3.zero);
                light.GetComponent<KitchenElement>().PartName = "Люстра";
            }
            _ctxTarget = sw;
            menu.Open(sw);
            CheckPanelInBothStates(menu, canvas.transform, withLiveLight ? "есть светильник" : "светильников нет", distinct);
            Assume.That(NoLiveLights(), Is.EqualTo(!withLiveLight), "предпосылка: сцена в нужном состоянии");
        }
        finally
        {
            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(canvas.gameObject);
        }

        var violations = new List<string>(distinct);
        violations.Sort();
        Assert.IsEmpty(violations, ConfirmedMessage(violations));
    }
}

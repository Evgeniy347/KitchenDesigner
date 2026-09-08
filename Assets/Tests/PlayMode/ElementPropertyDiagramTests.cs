using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

public class ElementPropertyDiagramTests
{
    private GameObject? _bootstrap;
    private GameObject? _mainCamera;
    private Canvas? _uiCanvas;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();
        _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
        _mainCamera.transform.LookAt(Vector3.zero);

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;

        _uiCanvas = UIManager.Instance!.Canvas;
        Assert.IsNotNull(_uiCanvas, "Canvas should be created by Bootstrap");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            if (es != null) Object.Destroy(es.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    /// <summary>
    /// Hide all sibling panels, center <paramref name="panelName"/>,
    /// switch canvas to ScreenSpaceCamera + ConstantPixelSize, render to PNG.
    /// Camera size is derived from the panel's actual RectTransform after setupPanel
    /// runs (Layout may resize it), so content is never clipped.
    /// </summary>
    private IEnumerator CapturePanel(string panelName, string fileName,
        System.Action setupPanel, System.Action? teardownPanel = null)
    {
        // Панели-окна лежат внутри WindowLayer, а не в корне канвы — ищем рекурсивно
        // и гасим соседей на каждом уровне вложенности (см. UiTestTree).
        var panelT = UiTestTree.FindDeep(_uiCanvas!.transform, panelName);
        Assert.IsNotNull(panelT, $"Panel '{panelName}' not found under canvas");
        var hidden = UiTestTree.HideAllExcept(_uiCanvas!.transform, panelT!);

        var panelRt = panelT!.GetComponent<RectTransform>();
        var origAnchorMin = panelRt.anchorMin;
        var origAnchorMax = panelRt.anchorMax;
        var origPivot = panelRt.pivot;
        var origPos = panelRt.anchoredPosition;
        var origSize = panelRt.sizeDelta;

        // Switch canvas to ScreenSpaceCamera + ConstantPixelSize before setupPanel
        // so that Layout() computes sizes in pixel units (not scaled).
        var scaler = _uiCanvas!.GetComponent<CanvasScaler>();
        var origScaleMode = scaler.uiScaleMode;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        var origRenderMode = _uiCanvas!.renderMode;
        _uiCanvas!.renderMode = RenderMode.ScreenSpaceCamera;
        _uiCanvas!.planeDistance = 1f;

        // Re-anchor to center so the panel sits in the middle of the canvas.
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;

        // Run setupPanel (opens the panel, triggers Layout which may resize it).
        setupPanel?.Invoke();
        yield return null; // let Layout settle

        // Use the panel's ACTUAL size after Layout — never the hardcoded estimate.
        int panelW = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.x));
        int panelH = Mathf.Max(1, Mathf.CeilToInt(panelRt.sizeDelta.y));

        // Re-center after Layout may have changed sizeDelta.
        panelRt.anchoredPosition = Vector2.zero;

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = panelH * 0.5f;
        cam.aspect = (float)panelW / panelH;
        cam.cullingMask = 1 << _uiCanvas!.gameObject.layer;
        _uiCanvas!.worldCamera = cam;

        var rt = new RenderTexture(panelW, panelH, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(panelW, panelH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, panelW, panelH), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), $"PNG was not created at {path}");
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG file is empty");
        Debug.Log($"[SCREENSHOT] Saved: {path} ({panelW}x{panelH})");

        var jsonPath = Path.ChangeExtension(path, ".json");
        UiSnapshotEngine.CaptureVerified(panelT!.gameObject, jsonPath);

        teardownPanel?.Invoke();

        _uiCanvas!.renderMode = origRenderMode;
        _uiCanvas!.worldCamera = null;
        scaler.uiScaleMode = origScaleMode;

        panelRt.anchorMin = origAnchorMin;
        panelRt.anchorMax = origAnchorMax;
        panelRt.pivot = origPivot;
        panelRt.anchoredPosition = origPos;
        panelRt.sizeDelta = origSize;

        UiTestTree.Restore(hidden);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }

    // ── Spawn helpers ────────────────────────────────────────

    private static KitchenElement SpawnPart(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), name,
            new Vector3(1f, 0.2f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnFacade(string name)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 400, 18), name,
            new Vector3(2f, 0.2f, 0f), 2, 2, 2, 2);
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnAssembled(string name)
    {
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(450, 700, 18), name,
            new Vector3(3f, 0.35f, 0f), AssembledFill.Blind);
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnRadial(string name)
    {
        var go = ElementFactory.CreateRadialShelf(600, 400, 18, 200, name,
            new Vector3(4f, 0.009f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnDrawer(string name)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name,
            new Vector3(5f, 0.043f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnTable(string name)
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 600), name,
            new Vector3(6f, 0.375f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    private static KitchenElement SpawnWall(string name)
    {
        var go = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), name,
            new Vector3(7f, 1.25f, 0f));
        return go.GetComponent<KitchenElement>();
    }

    // ── Tests ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ContextMenu_Part_SavesPng()
    {
        var el = SpawnPart("Деталь_800x400");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_part.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Кадр РАДИ РАЗЛИЧИМОСТИ: на обычной детали все четыре стороны
    /// зелёные, и по contextmenu_part.png нельзя проверить ни один из новых
    /// цветов. Здесь у одной детали разом стоят все четыре состояния:
    ///
    ///   L1 (верх)  — «принудительно есть»: жёлтая полоса, заливка сплошная;
    ///   L2 (низ)   — авто, торец открыт: зелёная полоса, заливка сплошная;
    ///   W1 (справа)— авто, торец закрыт стойкой: серая полоса С ПРОРЕЗЬЮ
    ///                («кромки тут нет» читается и без цвета);
    ///   W2 (слева) — «убрать»: красная полоса, тоже с прорезью.
    ///
    /// Расстановка не описана словами, а ПРОВЕРЕНА: если стойка перестанет
    /// закрывать W1 или низ упрётся в подложку, тест краснеет, а не отдаёт
    /// кадр, на котором нарисовано не то, что подписано.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_EdgeStates_AllFourAtOnce_SavesPng()
    {
        var el = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Деталь_кромки",
            new Vector3(1f, 0.5f, 0f)).GetComponent<KitchenElement>();
        ElementFactory.CreatePart(new Vector3Int(18, 720, 400), "Стойка_справа",
            new Vector3(1.409f, 0.5f, 0f));
        Assert.IsTrue(el.SupportsEdges, "лист 800x400x18 кромкуется");

        var coverage = EdgeBanding.Coverage(el, PartRegistry.GetAll());
        Assert.IsFalse(coverage.HasEdge(EdgeSide.W1),
            "правый торец обязан быть закрыт стойкой — иначе серого состояния на кадре нет");
        Assert.IsTrue(coverage.HasEdge(EdgeSide.L2),
            "нижний торец обязан быть открыт — иначе зелёного состояния на кадре нет");

        el.SetEdgeState(EdgeSide.L1, EdgeSideState.Forced);
        el.SetEdgeState(EdgeSide.W2, EdgeSideState.Suppressed);

        yield return CapturePanel("ContextMenu", "contextmenu_edge_states.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Facade_SavesPng()
    {
        var el = SpawnFacade("Фасад_600x400");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_facade.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_AssembledFacade_SavesPng()
    {
        var el = SpawnAssembled("Сборный_450x700");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_assembled.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_RadialShelf_SavesPng()
    {
        var el = SpawnRadial("Радиусная_300");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_radial.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Drawer_SavesPng()
    {
        var el = SpawnDrawer("Ящик_A_350");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_drawer.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Меню стены с раскрытым списком накладок: секция «Текстуры» есть
    /// только у стены и пола, и в свёрнутом виде на снимке видна одной строкой —
    /// раскрываем её, чтобы на картинке были и строки накладок, и строка
    /// добавления.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_Wall_Textures_SavesPng()
    {
        var el = SpawnWall("Стена_3000x2500");
        Assert.IsNotNull(el);
        el.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.A, "oak"),
            new TextureOverlaySpec(OverlaySide.B, "white", 100, 200, 1200, 900),
        });

        yield return CapturePanel("ContextMenu", "contextmenu_wall_textures.png",
            () =>
            {
                ContextMenuUI.Instance!.Open(el);
                ContextMenuUI.Instance!.Textures.Toggle();
            },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator DayNightPanel_SavesPng()
    {
        yield return CapturePanel("DayNightPanel", "daynightpanel.png",
            () => { DayNightPanelUI.Instance?.SetVisible(true); },
            () => { DayNightPanelUI.Instance?.SetVisible(false); });
    }

    [UnityTest]
    public IEnumerator Sidebar_SavesPng()
    {
        yield return CapturePanel("Sidebar", "sidebar.png",
            () => { },
            () => { });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Table_SavesPng()
    {
        var el = SpawnTable("Стол_2000x750x1000");
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_table.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_RadiusTable_SavesPng()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateRadiusTable(dims, "Радиусный стол", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_radius_table.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Stool_SavesPng()
    {
        var dims = new Vector3Int(StoolElement.DefaultWidthMM,
            StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateStool(dims, FurnitureLayout.MaxCornerRadiusMM(dims),
            "Табуретка", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_stool.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Chair_SavesPng()
    {
        var dims = new Vector3Int(ChairElement.DefaultWidthMM,
            ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateChair(dims, FurnitureLayout.MaxCornerRadiusMM(dims),
            AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT, "Стул", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_chair.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Sofa_SavesPng()
    {
        var dims = new Vector3Int(SofaElement.DefaultWidthMM,
            SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateSofa(dims, SofaElement.DefaultCornerRadiusMM,
            SofaElement.DefaultSeatHeightMM, "Диван", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_sofa.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Bed_SavesPng()
    {
        var dims = BedLayout.DefaultDimensions(true, true);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateBed(dims, true, true, "Кровать", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_bed.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Pouffe_SavesPng()
    {
        var dims = new Vector3Int(PouffeElement.DefaultWidthMM,
            PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM);
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePouffe(dims, PouffeElement.DefaultCornerRadiusMM,
            PouffeElement.DefaultSeatThicknessMM, "Пуфик", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_pouffe.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Toilet_SavesPng()
    {
        var dims = ToiletElement.ModelDimensionsMM;
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM, "Унитаз", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_toilet.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_WallHungToilet_SavesPng()
    {
        var dims = WallHungToiletElement.ModelDimensionsMM;
        Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateWallHungToilet(
            WallHungToiletElement.DefaultSeatHeightMM,
            WallHungToiletElement.DefaultFlushPlateHeightMM, "Инсталляция", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_wall_hung_toilet.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    [UnityTest]
    public IEnumerator ContextMenu_Pillar_SavesPng()
    {
        int totalH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Default + PillarElement.BottomHeightMM;
        int hDim = totalH;
        Vector3 pos = new Vector3(0f, hDim * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "Опора", pos);
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_pillar.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Винтовая опора, вкрученная в дно 600×18×500. Кадр нужен ради
    /// подменю «Корпус»: подпись, нередактируемый «Заход в корпус» и две строки
    /// по два поля — «Слева / справа, мм» и «Сверху / снизу, мм». Опора стоит
    /// посреди дна, поэтому в них обязаны стоять 300/300 и 250/250, а не
    /// прочерки: прочерк здесь означал бы, что хозяин не вывелся.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_ScrewLeg_SavesPng()
    {
        var bottom = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Дно",
            new Vector3(0f, 159f * AppConstants.MM_TO_UNITS, 0f))
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(bottom);
        var go = ElementFactory.CreateScrewLeg("Опора", new Vector3(0f, 0.1f, 0f));
        var leg = go.GetComponent<ScrewLegElement>();
        Assert.IsNotNull(leg);
        leg.SeatAfterMove(PartRegistry.GetAll());
        SceneChangeTracker.SettleDerivedLinks();
        Assert.AreEqual(bottom.PartName, leg.HostPartName,
            "без хозяина секция «Корпус» покажет прочерки, и кадр перестанет быть о ней");
        yield return CapturePanel("ContextMenu", "contextmenu_screwleg.png",
            () => { ContextMenuUI.Instance!.Open(leg); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Труба: условный проход, три погашенные строки (диаметры и стенка) и
    /// схема концов — статическая картинка трубы с двумя торцами и списком деталей
    /// под каждым. Один конец заглушен нарочно: кадр обязан показать ОБА состояния
    /// торца сразу — залитый (занят) и пустой контур (свободен), — иначе форма как
    /// носитель смысла (docs/UI-GUIDELINES.md §10) на снимке не проверяется.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_Pipe_SavesPng()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 600, "Труба",
            new Vector3(0f, 0.3f, 0f));
        var pipe = go.GetComponent<PipeElement>();
        Assert.IsNotNull(pipe);
        var cap = ElementFactory.CreatePipeCap("Zaglushka", pipe.EndBUnits)
            .GetComponent<PipeFittingElement>();
        Assert.IsNotNull(cap);
        cap.SeatOnPipeEnd(pipe, 1);
        yield return CapturePanel("ContextMenu", "contextmenu_pipe.png",
            () => { ContextMenuUI.Instance!.Open(pipe); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Варочная: ширина/глубина/высота описывают верхнюю плиту (высота —
    /// общая), плюс две строки выреза — короба, уходящего в столешницу.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_Cooktop_SavesPng()
    {
        var go = ElementFactory.CreateCooktop("Варочная", new Vector3(0f, 0.9f, 0f));
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_cooktop.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Духовка: габарит задан моделью, поэтому все три строки размеров
    /// нередактируемы. Кадр нужен именно ради ПОДПИСЕЙ: и «Ширина/Высота/Глубина»,
    /// и значения в полях обязаны быть погашены (docs/UI-GUIDELINES.md → §9
    /// «Нередактируемая строка гаснет ЦЕЛИКОМ»), а «Название» рядом — яркой.</summary>
    [UnityTest]
    public IEnumerator ContextMenu_Oven_SavesPng()
    {
        var go = ElementFactory.CreateOven("Духовка", new Vector3(0f, 0.9f, 0f));
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el);
        yield return CapturePanel("ContextMenu", "contextmenu_oven.png",
            () => { ContextMenuUI.Instance!.Open(el); },
            () => { ContextMenuUI.Instance?.Close(); });
    }

    /// <summary>Окно свойств замера: обе точки в мм, длина и красная «Удалить».
    /// Открывается выбором отрезка в MeasureStore, как это делает рулетка.</summary>
    [UnityTest]
    public IEnumerator MeasureProperties_SavesPng()
    {
        var a = new Vector3(0.1f, 0.72f, -0.3f);
        var b = new Vector3(1.3f, 0.72f, -0.3f);
        var segment = new KitchenDesigner.Core.Measure.MeasureSegment(a, b);

        yield return CapturePanel("MeasurePanel", "measure_properties.png",
            () =>
            {
                KitchenDesigner.Core.Measure.MeasureStore.Add(segment);
                KitchenDesigner.Core.Measure.MeasureStore.Select(segment);
            },
            () => { KitchenDesigner.Core.Measure.MeasureStore.Clear(); });
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuTextureSectionTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — 0,31 с,
    /// и восемь сборок это 2,5 с из прогона EditMode при бюджете 170 с. Почему это
    /// безопасно — в сводке <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть
    /// ОДНА панель, переоткрываемая через <c>Open</c>.
    ///
    /// Здесь у общей панели есть два переживающих тест состояния, и оба сбрасывает сам
    /// <c>Open</c>: свёрнутость секции текстур (<c>_textures.Collapse()</c> — поэтому
    /// <see cref="OpenWallWithThreeOverlays"/> разворачивает её сам, а не рассчитывает
    /// на свежесобранную панель) и список декоров в строках
    /// (<c>RebuildMaterialOptions</c>, который <c>Open</c> зовёт стене как
    /// <c>SupportsTextureOverlays</c>). Каждый тест этого класса идёт через
    /// <c>OpenWallWithThreeOverlays</c>, то есть через <c>Open</c>, без исключений.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего. Взвод «Удалить» живёт в статике и переживает
    /// не только тест (<see cref="DeletingAnyOverlay_DropsTheAreaHandles_BecauseIndexesShift"/>
    /// жмёт CtxTexDel0 дважды), фокус — причина, по которой <c>RefreshUnfocused</c>
    /// молча пропускает поле.</summary>
    [SetUp]
    public void Setup()
    {
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>DestroyImmediate</c> спавнов: он
    /// обнуляет <c>_target</c>, закрывает превью декора и гасит ручки области —
    /// иначе живая панель осталась бы с уничтоженной стеной в руках.</summary>
    [TearDown]
    public void Teardown()
    {
        TextureOverlayHandles.End();
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private Transform Panel()
    {
        var panel = _canvas!.transform.Find("ContextMenu");
        Assert.NotNull(panel, "панель контекстного меню должна существовать");
        return panel!;
    }

    private KitchenElement MakeWall(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(3000, 2500, 100);
        go.AddComponent<Wall>();
        _spawned.Add(go);
        return el;
    }

    private KitchenElement OpenWallWithThreeOverlays()
    {
        var wall = MakeWall("Стена");
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.A, "oak"),
            new TextureOverlaySpec(OverlaySide.A, "white", 100, 200, 1200, 900),
            new TextureOverlaySpec(OverlaySide.B, "oak", 50, 50, 600, 600),
        });
        _menu!.Open(wall);
        Panel().Find("CtxTextures").GetComponent<Button>().onClick.Invoke();
        return wall;
    }

    private static int MaterialIndexOf(string id)
    {
        var all = MaterialCatalog.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].id == id) return i;
        Assert.Fail($"декора «{id}» нет в каталоге");
        return -1;
    }

    [Test]
    public void EachRow_EditsItsOwnOverlay_NotTheLastOne()
    {
        var wall = OpenWallWithThreeOverlays();

        Panel().Find("CtxTexMat0").GetComponent<TMP_Dropdown>().value = MaterialIndexOf("white");

        Assert.AreEqual("white", wall.TextureOverlays[0].MaterialId,
            "строка 0 правит накладку 0: индекс в обработчике должен быть копией счётчика цикла");
        Assert.AreEqual("oak", wall.TextureOverlays[2].MaterialId,
            "последняя накладка не тронута — иначе все строки правили бы её одну");
    }

    [Test]
    public void EditingARow_ChangesSideAndDecor_ButKeepsTheArea()
    {
        var wall = OpenWallWithThreeOverlays();
        var before = wall.TextureOverlays[1];

        Panel().Find("CtxTexMat1").GetComponent<TMP_Dropdown>().value = MaterialIndexOf("oak");
        var after = wall.TextureOverlays[1];

        Assert.AreEqual("oak", after.MaterialId, "декор берётся из списка строки");
        Assert.AreEqual(before.u0MM, after.u0MM, "область правится ручками, а не строкой меню");
        Assert.AreEqual(before.v0MM, after.v0MM, "область правится ручками, а не строкой меню");
        Assert.AreEqual(before.widthMM, after.widthMM, "область правится ручками, а не строкой меню");
        Assert.AreEqual(before.heightMM, after.heightMM, "область правится ручками, а не строкой меню");
    }

    [Test]
    public void DeletingAnyOverlay_DropsTheAreaHandles_BecauseIndexesShift()
    {
        var wall = OpenWallWithThreeOverlays();
        TextureOverlayHandles.Begin(wall, 2);
        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 2), "ручки включены на последней накладке");

        var del = Panel().Find("CtxTexDel0").GetComponent<Button>();
        del.onClick.Invoke();
        del.onClick.Invoke();

        Assert.IsFalse(TextureOverlayHandles.Active,
            "после удаления любой накладки индексы ниже сдвигаются — ручки правили бы чужую область");
    }

    [Test]
    public void MovingAnOverlay_CarriesItsAreaHandlesAlong()
    {
        var wall = OpenWallWithThreeOverlays();
        TextureOverlayHandles.Begin(wall, 0);

        Panel().Find("CtxTexOrder0/CtxTexDown0").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 1),
            "порядок меняют ровно тогда, когда подгоняют перекрытие: ручки едут за своей накладкой");
    }

    [Test]
    public void MovingAnOverlay_AlsoShiftsTheHandlesOfTheDisplacedNeighbour()
    {
        var wall = OpenWallWithThreeOverlays();
        TextureOverlayHandles.Begin(wall, 1);

        Panel().Find("CtxTexOrder0/CtxTexDown0").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 0),
            "если правилась соседка, её индекс тоже сдвинулся — ручки должны уехать вместе с ней");
    }

    [Test]
    public void SideHover_HighlightsThatFace_AndAllSidesHighlightsNothing()
    {
        var wall = OpenWallWithThreeOverlays();
        var section = _menu!.Textures;

        section.HoverSide(0);
        Assert.IsTrue(SideHighlighter.IsFaceShown(wall, 0),
            "буква «C» не говорит, какая это сторона — под курсором грань подсвечивается в сцене");

        section.HoverSide((int)OverlaySide.All);
        Assert.AreEqual(0, SideHighlighter.QuadCount,
            "у пункта «(все)» грань не одна — подсвечивать нечего, молча ничего не красим");
    }

    [Test]
    public void OverlaysChangedOutsideTheMenu_AreNoticedByTheFingerprint()
    {
        var wall = OpenWallWithThreeOverlays();
        var section = _menu!.Textures;
        Assert.IsFalse(section.ChangedOutsideTheMenu(), "сразу после открытия набор совпадает с показанным");

        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.A, "white") });

        Assert.IsTrue(section.ChangedOutsideTheMenu(),
            "накладки правят и undo, и MCP, и ручки области — счётчик и строки должны это догонять");
    }

    [Test]
    public void FingerprintIsQuiet_WhileTheMenuItselfKeepsTheUiInSync()
    {
        var wall = OpenWallWithThreeOverlays();
        var section = _menu!.Textures;

        Panel().Find("CtxTexMat1").GetComponent<TMP_Dropdown>().value = MaterialIndexOf("oak");

        Assert.IsFalse(section.ChangedOutsideTheMenu(),
            "правка из самого меню уже обновила строки — сторож не должен срабатывать второй раз");
        Assert.AreEqual(3, wall.TextureOverlays.Count);
    }
}

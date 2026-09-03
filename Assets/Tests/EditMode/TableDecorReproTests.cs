using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Декор стола: слоты «столешница»/«ножки» против базового
/// KitchenElement.MaterialId и замощение крышки по физическому размеру плитки.</summary>
public class TableDecorReproTests
{
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private SelectionManager? _selection;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _selection = new GameObject("SelMgr").AddComponent<SelectionManager>();
        SetSelectionInstance(_selection);
    }

    [TearDown]
    public void TearDown()
    {
        if (_selection != null) Object.DestroyImmediate(_selection.gameObject);
        SetSelectionInstance(null);
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private static void SetSelectionInstance(SelectionManager? sm)
    {
        SelectionManager.Instance = sm;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Один корень трёх симптомов: базовый MaterialId расходился со слотом
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void RadiusTable_TabletopSlotChoice_IsWhatTheElementCallsItsOwnDecor()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;

        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, MaterialCatalog.Get("oak"));

        Assert.AreEqual("oak", ((KitchenElement)table).MaterialId,
            "выбор в списке «Столешница» — и есть СОБСТВЕННЫЙ декор стола; пока базовый "
            + "MaterialId жил отдельным полем, подсветка считала стол недекорированным "
            + "и перекрашивала его при каждом обновлении");
        Assert.IsTrue(MaterialManager.HasCustomDecor(table),
            "HasCustomDecor спрашивает базовый MaterialId — на нём держится ветка "
            + "«показать декор, а не валидационный тон» в ElementHighlighter");
    }

    [Test]
    public void RadiusTable_TabletopSlotChoice_SurvivesDeselect()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;
        var renderer = table.GetComponent<MeshRenderer>()!;
        var oakMat = MaterialManager.GetSharedMaterial(MaterialCatalog.Get("oak"));

        _selection!.Select(table);
        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, MaterialCatalog.Get("oak"));
        _selection.DeselectAll();

        Assert.AreEqual(oakMat, renderer.sharedMaterial,
            "снятие выделения вернуло материал, запомненный ДО выбора декора: "
            + "симптом «выделил-снял — текстура пропала»");
    }

    [Test]
    public void Table_OwnDecorRepaint_DoesNotResetTheTabletopChosenBySlot()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;

        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, MaterialCatalog.Get("oak"));
        MaterialManager.ApplySlot(table, MaterialSlot.Legs, MaterialCatalog.Get("wenge"));

        MaterialManager.ApplyOwnDecor(table);

        Assert.AreEqual("oak", table.PrimaryMaterialId,
            "ApplyOwnDecor зовётся подсветкой после КАЖДОЙ правки, ножек в том числе; "
            + "читая базовый MaterialId, он возвращал столешнице устаревший декор — "
            + "симптом «меняешь ножки, меняется столешница»");
        Assert.AreEqual("wenge", table.SecondaryMaterialId,
            "перекраска столешницы не имеет права трогать ножки");
    }

    [Test]
    public void Table_SlotsStayApart_WhenOnlyTheLegsChange()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;

        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, MaterialCatalog.Get("oak"));
        MaterialManager.ApplySlot(table, MaterialSlot.Legs, MaterialCatalog.Get("wenge"));

        Assert.AreEqual("oak", table.PrimaryMaterialId);
        Assert.AreEqual("wenge", table.SecondaryMaterialId);
        Assert.AreEqual("oak", MaterialManager.MaterialIdOf(table, MaterialSlot.Tabletop));
        Assert.AreEqual("wenge", MaterialManager.MaterialIdOf(table, MaterialSlot.Legs));
    }

    [Test]
    public void Table_SavedMaterialId_IsTheTabletopDecor()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;
        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, MaterialCatalog.Get("oak"));
        MaterialManager.ApplySlot(table, MaterialSlot.Legs, MaterialCatalog.Get("wenge"));

        var data = ElementCapture.FromElement(table);

        Assert.AreEqual("oak", data.materialId,
            "в сейв уходил базовый MaterialId, у стола не менявшийся никогда: проект "
            + "запоминал «default» и после загрузки подсветка снова стирала столешницу");
        Assert.AreEqual("oak", data.tabletopMaterialId);
        Assert.AreEqual("wenge", data.legsMaterialId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Декор замощается, а не растягивается
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_DecorRenderer_IsTheTabletop_NotALeg()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(2000, 750, 1000), "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;

        var decor = ((KitchenElement)table).DecorRenderer;

        Assert.IsNotNull(decor, "у стола есть поверхность под декор — крышка");
        Assert.AreEqual("Tabletop", decor!.gameObject.name,
            "GetComponentInChildren отдавал ПЕРВОГО ребёнка — ножку, и «вырез» декора "
            + "уезжал на неё, а крышка оставалась с UV 0..1, то есть растянутой");
    }

    [Test]
    public void Table_DecorSurface_IsWidthByDepth_NotWidthByHeight()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(2000, 750, 1000), "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;

        Assert.AreEqual(new Vector2Int(2000, 1000), ((KitchenElement)table).DecorSurfaceMM,
            "рисунок лежит на крышке: её размер — ширина на глубину, высота стола "
            + "(750) к нему отношения не имеет");
    }

    [Test]
    public void Table_Tiling_RepeatsTheDecorByItsPhysicalSize()
    {
        var def = MaterialCatalog.Get("oak");
        var tile = MaterialManager.TileMM(def);
        var dims = new Vector3Int(2000, 750, 1000);
        Assume.That(dims.x, Is.Not.EqualTo(tile.x),
            "щит обязан отличаться от плитки, иначе тест зелен и на растянутом декоре");

        var go = ElementFactory.CreateTable(dims, "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;
        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, def);

        var mpb = new MaterialPropertyBlock();
        ((KitchenElement)table).DecorRenderer!.GetPropertyBlock(mpb);
        var st = mpb.GetVector(BaseMapST);

        Assert.AreEqual(dims.x / (float)tile.x, st.x, 1e-3f,
            "по ширине декор обязан повториться dims.x / tileWidth раз");
        Assert.AreEqual(dims.z / (float)tile.y, st.y, 1e-3f,
            "по глубине — dims.z / tileHeight; раньше сюда шла ВЫСОТА стола");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Слот пишет в СВОЙ рендерер — проверка материалом, а не строкой
    // ═══════════════════════════════════════════════════════════════

    private static readonly string[] KnownDecorIds = { "oak", "wenge", MaterialCatalog.DefaultId };

    /// <summary>Материал на рендерере, названный по декору, из которого он получен.
    ///
    /// Сравнивать сами Material нельзя: они создаются как new Material(shader) и
    /// имени им никто не даёт, поэтому ToString() у всех печатает имя ШЕЙДЕРА, и
    /// упавший тест сообщает «Expected: Universal Render Pipeline/Lit, But was:
    /// Universal Render Pipeline/Lit». Читатель такого сообщения решит, что сломан
    /// тест, а не код. Отображение обратно в идентификатор идёт ПО ССЫЛКЕ на
    /// фактический материал рендерера — поля слота здесь не участвуют, иначе
    /// проверка вернулась бы к той самой дыре, ради которой она написана.</summary>
    private static string DecorOn(MeshRenderer renderer)
    {
        var mat = renderer.sharedMaterial;
        if (mat == null) return "<материала нет>";
        foreach (var id in KnownDecorIds)
            if (ReferenceEquals(mat, MaterialManager.GetSharedMaterial(MaterialCatalog.Get(id))))
                return id;
        return "<декор вне списка теста>";
    }

    private static (MeshRenderer top, MeshRenderer leg) DecorAndLegRenderers(KitchenElement element)
    {
        var top = element.DecorRenderer;
        Assert.IsNotNull(top, "у стола обязана быть поверхность под декор");
        var legTransform = element.transform.Find("Leg1");
        Assert.IsNotNull(legTransform,
            "LegSet называет ножки Leg1..LegN; без ножки на сцене проверять нечего");
        var leg = legTransform!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(leg, "ножка обязана быть видимой, иначе декор на ней не проверить");
        return (top!, leg!);
    }

    private static void AssertLegsSlotLeavesTheTabletopAlone(KitchenElement table, string what)
    {
        var oak = MaterialCatalog.Get("oak");
        var wenge = MaterialCatalog.Get("wenge");
        Assume.That(MaterialManager.GetSharedMaterial(oak),
            Is.Not.EqualTo(MaterialManager.GetSharedMaterial(wenge)),
            "два декора обязаны давать РАЗНЫЕ материалы, иначе тест зелен на любом коде");

        MaterialManager.ApplySlot(table, MaterialSlot.Tabletop, oak);
        MaterialManager.ApplySlot(table, MaterialSlot.Legs, wenge);

        var (top, leg) = DecorAndLegRenderers(table);

        Assert.AreEqual("wenge", DecorOn(leg),
            "положительный контроль (" + what + "): ножки обязаны носить выбранный "
            + "для них декор. Без него проверка крышки ничего не стоит — она была бы "
            + "зелёной и на коде, который ножки не красит вообще");
        Assert.AreEqual("oak", DecorOn(top),
            "правка ТОЛЬКО ножек не имеет права перекрасить крышку (" + what + "). "
            + "Соседние тесты этого не ловят: они сравнивают ИДЕНТИФИКАТОРЫ слотов, а "
            + "те при такой поломке остаются верными — _tabletopMaterialId по-прежнему "
            + "«oak», тогда как на рендерере крышки уже лежит материал ножек. Видно "
            + "это только по фактическому материалу");
    }

    [Test]
    public void Table_LegsSlotChange_PaintsTheLegs_AndLeavesTheTabletopRendererAlone()
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "T", Vector3.zero);
        AssertLegsSlotLeavesTheTabletopAlone(go.GetComponent<TableElement>()!,
            "крышка — ребёнок Tabletop");
    }

    [Test]
    public void RadiusTable_LegsSlotChange_PaintsTheLegs_AndLeavesTheTabletopRendererAlone()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700), "RT", Vector3.zero);
        AssertLegsSlotLeavesTheTabletopAlone(go.GetComponent<RadiusTableElement>()!,
            "крышка — рендерер КОРНЯ, а не ребёнок, поэтому перепутанные слоты "
            + "выглядят иначе, чем у прямоугольного стола");
    }
}

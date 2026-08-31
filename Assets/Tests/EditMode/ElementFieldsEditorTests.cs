using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ElementFieldsEditorTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockOnViolation;

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
        _blockOnViolation = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
    }

    [TearDown]
    public void Teardown()
    {
        KitchenSettings.Instance.BlockOnViolation = _blockOnViolation;
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private T Spawn<T>(GameObject go) where T : KitchenElement
    {
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField Field(string label) =>
        Panel().Find($"F_{label}")!.GetComponent<TMP_InputField>();

    private static string Text(TMP_InputField f) => f.text.Replace("​", "");

    private void Type(string label, string value)
    {
        Field(label).text = value;
        Field(label).onEndEdit.Invoke(value);
    }

    private KitchenElement Board() =>
        Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(600, 300, 18), "Полка", Vector3.zero));

    private RadialShelfElement Radial() =>
        Spawn<RadialShelfElement>(ElementFactory.CreateRadialShelf(600, 600, 18, 200, "Радиусная", Vector3.zero));

    private PillarElement Pillar() =>
        Spawn<PillarElement>(ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "Опора", Vector3.zero));

    private DrawerElement Drawer() =>
        Spawn<DrawerElement>(ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero));

    private StoolElement Stool() =>
        Spawn<StoolElement>(ElementFactory.CreateStool(
            new Vector3Int(StoolElement.DefaultWidthMM, StoolElement.DefaultHeightMM,
                StoolElement.DefaultDepthMM), 0, "Табуретка", Vector3.zero));

    private TableElement Table() =>
        Spawn<TableElement>(ElementFactory.CreateTable(
            new Vector3Int(1200, 750, 700), "Стол", Vector3.zero));

    [Test]
    public void Radial_CornerRadiusRow_AppliesAndIsUndoable()
    {
        var shelf = Radial();
        _menu!.Open(shelf);
        Assume.That(Text(Field("Радиус угла")), Is.EqualTo("200"));

        Type("Радиус угла", "150");

        Assert.AreEqual(150, shelf.CornerRadius, "радиус применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(200, shelf.CornerRadius, "одна правка — один шаг отмены (правило 2)");
    }

    [Test]
    public void NonRadialElement_ShowsTheDefaultRadius()
    {
        _menu!.Open(Radial());
        Type("Радиус угла", "150");
        _menu!.Open(Board());

        Assert.AreEqual(AppConstants.RADIAL_CORNER_RADIUS_DEFAULT.ToString(),
            Text(Field("Радиус угла")),
            "у детали без радиуса поле показывает значение по умолчанию, а не чужое");
    }

    /// <summary>Регрессия, найденную снимком контекстного меню: редактор
    /// табуретки лежал в реестре _editors — Show/Apply/Refresh по нему ходили, —
    /// но его Build() никто не позвал, и строки в панели просто НЕ БЫЛО. Реестр
    /// из двух половин (список редакторов и список вызовов Build) молчит об этом
    /// так же, как молчит пропуск типа в реестре элементов.</summary>
    [Test]
    public void Stool_CornerRadiusRow_IsBuilt_AndAppliesAndIsUndoable()
    {
        var stool = Stool();
        _menu!.Open(stool);
        Assert.IsNotNull(Panel().Find("F_Скругление"),
            "строка «Скругление» обязана быть ПОСТРОЕНА: занести редактор в реестр _editors "
            + "и забыть позвать его Build() — значит получить свойство, которое нечем "
            + "править, и молча");
        Assume.That(Text(Field("Скругление")), Is.EqualTo("0"),
            "поле обязано открыться значением элемента");

        Type("Скругление", "150");

        Assert.AreEqual(150, stool.CornerRadiusMM, "скругление применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(0, stool.CornerRadiusMM, "одна правка — один шаг отмены (правило 2)");
    }

    [Test]
    public void Stool_CornerRadiusRow_ShowsTheClampedValueBack_NotWhatWasTyped()
    {
        var stool = Stool();
        _menu!.Open(stool);

        Type("Скругление", "1000");

        Assert.AreEqual(180, stool.CornerRadiusMM,
            "элемент зажал радиус половиной меньшей стороны: 360/2 = 180");
        Assert.AreEqual("180", Text(Field("Скругление")),
            "в поле обязан вернуться ПРИНЯТЫЙ элементом радиус, а не то, что напечатал "
            + "человек (CONVENTIONS.md → «Read a value back only AFTER EndCapture»)");
    }

    [Test]
    public void Stool_TabletopAndLegsMaterialRows_AreShown_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Stool());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у табуретки два декора — сиденье и ножки: она носитель ITabletop");
        Assert.IsTrue(Panel().Find("CtxTableLegs")!.gameObject.activeInHierarchy);
        Assert.IsFalse(Panel().Find("CtxMaterial")!.gameObject.activeInHierarchy,
            "общая строка «Текстура» у носителя столешницы скрыта — иначе один декор "
            + "спорит с двумя");
    }

    [Test]
    public void Stool_HasNoLegInsetRow()
    {
        _menu!.Open(Stool());

        var legInset = Panel().Find("F_Сдвиг опор");
        Assert.IsTrue(legInset == null || !legInset.gameObject.activeInHierarchy,
            "отступ ножек у табуретки — константа конструкции: показать поле, которое "
            + "редактор табуретки не обрабатывает, значит показать мёртвую строку");
    }

    [Test]
    public void Drawer_DimensionFieldsAreReadOnly()
    {
        _menu!.Open(Drawer());

        Assert.IsFalse(Field("Ширина").interactable,
            "габарит ящика вычисляется из типа, длины и ширины короба — прямое редактирование "
            + "недоступно, поле затемняется");
        Assert.IsFalse(Field("Высота").interactable);
        Assert.IsFalse(Field("Глубина").interactable);
    }

    [Test]
    public void Drawer_BoxWidthRow_DrivesTheInternalWidth()
    {
        var drawer = Drawer();
        _menu!.Open(drawer);

        Type("Ширина короба", "500");

        Assert.AreEqual(500, drawer.BoxWidth,
            "ширина короба — единственный размер ящика, который правит человек");
    }

    [Test]
    public void Drawer_TypedDimensions_AreIgnored()
    {
        var drawer = Drawer();
        _menu!.Open(drawer);
        var before = drawer.DimensionsMM;

        Type("Ширина", "9999");

        Assert.AreEqual(before, drawer.DimensionsMM,
            "поля Ш/В/Г у ящика вычисляемые: даже проставленное мимо UI значение не применяется");
    }

    [Test]
    public void Pillar_HeightRow_RecomputesTheMiddleSection()
    {
        var pillar = Pillar();
        _menu!.Open(pillar);
        int requestedTotal = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Max
            + PillarElement.BottomHeightMM;

        Type("Высота", requestedTotal.ToString());

        Assert.AreEqual(PillarElement.MidHeightMM_Max, pillar.MidHeightMM,
            "у опоры человек правит общую высоту, а тянется средняя секция: верх и низ заданы деталью");
    }

    [Test]
    public void Pillar_MiddleSectionRow_DrivesTheTotalHeight()
    {
        var pillar = Pillar();
        _menu!.Open(pillar);

        Type("Средняя секция", PillarElement.MidHeightMM_Max.ToString());

        Assert.AreEqual(PillarElement.MidHeightMM_Max, pillar.MidHeightMM);
        Assert.AreEqual(pillar.TotalHeightMM.ToString(), Text(Field("Высота")),
            "поле общей высоты обязано показать пересчитанную сумму секций");
    }

    [Test]
    public void Pillar_WidthAndDepthAreReadOnly()
    {
        _menu!.Open(Pillar());
        Assert.IsFalse(Field("Ширина").interactable, "ширина опоры фиксирована");
        Assert.IsFalse(Field("Глубина").interactable, "глубина опоры фиксирована");
        Assert.IsTrue(Field("Высота").interactable, "высоту опоры править можно");
    }

    [Test]
    public void Table_LegInsetRow_Applies()
    {
        var table = Table();
        _menu!.Open(table);

        Type("Сдвиг опор", "150");

        Assert.AreEqual(150, table.LegInsetMM);
    }

    [Test]
    public void Cooktop_CutoutRow_ShowsTheClampedValue_NotTheTypedOne()
    {
        var cooktop = Spawn<CooktopElement>(ElementFactory.CreateCooktop("Варочная", Vector3.zero));
        _menu!.Open(cooktop);

        Type("Ширина выреза", "5000");

        Assert.AreEqual(cooktop.CutoutWidthMM.ToString(), Text(Field("Ширина выреза")),
            "вырез клампится по ширине плиты — в поле обязано стоять применённое значение");
    }

    [Test]
    public void Window_DepthIsReadOnlyAndTypedDepthIsIgnored()
    {
        var wallGo = new GameObject("Стена");
        wallGo.AddComponent<MeshFilter>();
        wallGo.AddComponent<MeshRenderer>();
        var wall = wallGo.AddComponent<KitchenElement>();
        wall.PartName = "Стена";
        wall.DimensionsMM = new Vector3Int(3000, 2500, 100);
        wallGo.AddComponent<Wall>();
        _spawned.Add(wallGo);

        var window = Spawn<WindowElement>(ElementFactory.CreateWindow(
            new Vector3Int(1000, 1200, 100), "Окно", Vector3.zero));
        _menu!.Open(window);
        int depthBefore = window.DimensionsMM.z;

        Assert.IsFalse(Field("Глубина").interactable,
            "глубину окна диктует толщина стены — поле только для чтения");

        Type("Глубина", "999");
        Assert.AreEqual(depthBefore, window.DimensionsMM.z,
            "поле Г у окна игнорируется даже когда значение туда попало");
    }
}

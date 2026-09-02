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

    private ChairElement Chair() =>
        Spawn<ChairElement>(ElementFactory.CreateChair(
            new Vector3Int(ChairElement.DefaultWidthMM, ChairElement.DefaultHeightMM,
                ChairElement.DefaultDepthMM), 0, AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT,
            "Стул", Vector3.zero));

    /// <summary>Тот же сторож двух половин, что и у табуретки: строки стула
    /// заведены под СВОИМИ узлами (F_СкруглениеСтула, F_ВысотаСиденья), потому
    /// что подпись «Скругление» у них общая, а Find по имени вернул бы чужую
    /// строку табуретки — и тест зеленел бы, ничего не построив.</summary>
    [Test]
    public void Chair_BothRows_AreBuilt_AndOpenWithTheElementsValues()
    {
        _menu!.Open(Chair());

        Assert.IsNotNull(Panel().Find("F_" + ChairFieldsEditor.CornerRadiusNode),
            "строка «Скругление» стула обязана быть ПОСТРОЕНА: занести редактор в реестр "
            + "_editors и забыть позвать его Build() — значит получить свойство, которое "
            + "нечем править, и молча");
        Assert.IsNotNull(Panel().Find("F_" + ChairFieldsEditor.SeatHeightNode),
            "и строка «Высота сиденья» тоже");
        Assert.AreEqual("0", Text(Field(ChairFieldsEditor.CornerRadiusNode)));
        Assert.AreEqual("450", Text(Field(ChairFieldsEditor.SeatHeightNode)));
    }

    /// <summary>Одна правка на тест: <c>ApplyOncePerFrame</c> пропускает только
    /// один Apply за кадр, а в EditMode кадр не сменяется — вторая правка в том
    /// же тесте молча ничего не делает.</summary>
    [Test]
    public void Chair_CornerRadiusRow_Applies_AndIsUndoable()
    {
        var chair = Chair();
        _menu!.Open(chair);

        Type(ChairFieldsEditor.CornerRadiusNode, "150");

        Assert.AreEqual(150, chair.CornerRadiusMM, "скругление применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(0, chair.CornerRadiusMM, "одна правка — один шаг отмены (правило 2)");
    }

    [Test]
    public void Chair_SeatHeightRow_Applies_AndIsUndoable()
    {
        var chair = Chair();
        _menu!.Open(chair);

        Type(ChairFieldsEditor.SeatHeightNode, "500");

        Assert.AreEqual(500, chair.SeatHeightMM, "высота сиденья применяется");
        CommandStack.Undo();
        Assert.AreEqual(450, chair.SeatHeightMM, "и откатывается одним шагом");
    }

    [Test]
    public void Chair_CornerRadiusRow_ShowsTheClampedValueBack_NotWhatWasTyped()
    {
        var chair = Chair();
        _menu!.Open(chair);

        Type(ChairFieldsEditor.CornerRadiusNode, "1000");

        Assert.AreEqual(200, chair.CornerRadiusMM, "радиус зажат половиной меньшей стороны");
        Assert.AreEqual("200", Text(Field(ChairFieldsEditor.CornerRadiusNode)),
            "в поле обязан вернуться ПРИНЯТЫЙ элементом радиус, а не то, что напечатал "
            + "человек (CONVENTIONS.md → «Read a value back only AFTER EndCapture»)");
    }

    [Test]
    public void Chair_SeatHeightRow_ShowsTheClampedValueBack_NotWhatWasTyped()
    {
        var chair = Chair();
        _menu!.Open(chair);

        Type(ChairFieldsEditor.SeatHeightNode, "5000");

        int expected = 900 - ChairElement.MinBackrestHeightMM;
        Assert.AreEqual(expected, chair.SeatHeightMM,
            "высота сиденья зажата так, чтобы спинке осталось место");
        Assert.AreEqual(expected.ToString(), Text(Field(ChairFieldsEditor.SeatHeightNode)),
            "и она тоже обязана вернуться принятой, а не напечатанной");
    }

    [Test]
    public void Chair_TabletopAndLegsMaterialRows_AreShown_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Chair());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у стула два декора — сиденье со спинкой и ножки: он носитель ITabletop");
        Assert.IsTrue(Panel().Find("CtxTableLegs")!.gameObject.activeInHierarchy);
        Assert.IsFalse(Panel().Find("CtxMaterial")!.gameObject.activeInHierarchy,
            "общая строка «Текстура» у носителя столешницы скрыта");
    }

    [Test]
    public void Chair_DoesNotShowTheStoolRow_AndTheStoolDoesNotShowTheChairRows()
    {
        _menu!.Open(Chair());
        var stoolRow = Panel().Find("F_Скругление");
        Assert.IsTrue(stoolRow == null || !stoolRow.gameObject.activeInHierarchy,
            "строка табуретки у стула — мёртвая: её Apply не трогает стул, и человек "
            + "правил бы поле, которое ничего не делает");

        _menu!.Open(Stool());
        var chairSeat = Panel().Find("F_" + ChairFieldsEditor.SeatHeightNode);
        Assert.IsTrue(chairSeat == null || !chairSeat.gameObject.activeInHierarchy,
            "и наоборот: у табуретки высоты сиденья нет — это её общая высота");
    }

    private BedElement Bed() =>
        Spawn<BedElement>(ElementFactory.CreateBed(
            BedLayout.DefaultDimensions(true, true), true, true, "Кровать", Vector3.zero));

    private TMP_Dropdown Dropdown(string node) =>
        Panel().Find(node)!.GetComponent<TMP_Dropdown>();

    /// <summary>Та же регрессия, что у табуретки: реестр редакторов состоит из
    /// ДВУХ половин — массива _editors и списка вызовов Build(), — и строка,
    /// заведённая только в одной, молча не появляется в панели. Тест открывает
    /// меню и ищет узлы по имени, как их видит человек.</summary>
    [Test]
    public void Bed_TypeRows_AreBuilt_AndShowTheCurrentType()
    {
        var bed = Bed();
        _menu!.Open(bed);

        Assert.IsTrue(Panel().Find(BedFieldsEditor.SizeNode)!.gameObject.activeInHierarchy,
            "строка «Тип кровати» обязана быть видна у кровати");
        Assert.IsTrue(Panel().Find(BedFieldsEditor.HeadboardNode)!.gameObject.activeInHierarchy,
            "и строка «Изголовье» тоже");
        Assert.AreEqual(1, Dropdown(BedFieldsEditor.SizeNode).value,
            "двуспальная — второй вариант списка; индекс, разошедшийся со смыслом, "
            + "переключал бы кровать наоборот");
        Assert.AreEqual(1, Dropdown(BedFieldsEditor.HeadboardNode).value,
            "и «Со спинкой» — тоже второй");
    }

    [Test]
    public void Bed_SizeDropdown_SwitchesTheTypeAndResetsTheSize_Undoably()
    {
        var bed = Bed();
        bed.DimensionsMM = new Vector3Int(1600, 950, 2100);
        _menu!.Open(bed);

        Dropdown(BedFieldsEditor.SizeNode).value = 0;

        Assert.IsFalse(bed.IsDouble, "выбор «Односпальная» обязан дойти до элемента");
        Assert.AreEqual(BedLayout.DefaultDimensions(false, true), bed.DimensionsMM,
            "смена типа сбрасывает габарит на дефолтный — прямое указание пользователя");
        Assert.AreEqual("900", Text(Field("Ширина")),
            "и поле ширины обязано показать НОВЫЙ размер: человек, который не увидит "
            + "сброса в панели, решит, что переключатель не сработал");

        CommandStack.Undo();
        Assert.IsTrue(bed.IsDouble, "переключатель обязан откатываться");
        Assert.AreEqual(new Vector3Int(1600, 950, 2100), bed.DimensionsMM,
            "и отмена обязана вернуть РУЧНОЙ размер, а не дефолт двуспальной: флаг типа "
            + "восстанавливается раньше габарита (Undoable.Order), иначе он сбросил бы "
            + "только что восстановленные миллиметры");
    }

    [Test]
    public void Bed_HeadboardDropdown_SwitchesTheHeadboard_Undoably()
    {
        var bed = Bed();
        _menu!.Open(bed);

        Dropdown(BedFieldsEditor.HeadboardNode).value = 0;

        Assert.IsFalse(bed.HasHeadboard, "выбор «Без спинки» обязан дойти до элемента");
        Assert.AreEqual(BedLayout.HeightFor(false), bed.DimensionsMM.y,
            "и высота обязана сброситься до верха подушек");

        CommandStack.Undo();
        Assert.IsTrue(bed.HasHeadboard, "и это тоже один шаг отмены");
        Assert.AreEqual(BedLayout.HeightFor(true), bed.DimensionsMM.y,
            "вместе с высотой");
    }

    [Test]
    public void Bed_ShowsCarcassAndMattressRows_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Bed());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у кровати два декора — каркас и постель: она носитель ITabletop");
        Assert.IsTrue(Panel().Find("CtxTableLegs")!.gameObject.activeInHierarchy,
            "второй слот тоже обязан быть виден");
        Assert.IsFalse(Panel().Find("CtxMaterial")!.gameObject.activeInHierarchy,
            "общая строка «Текстура» у носителя двух слотов скрыта");
    }

    [Test]
    public void BedRows_AreHiddenForOtherFurniture()
    {
        _menu!.Open(Bed());
        _menu!.Open(Stool());

        Assert.IsFalse(Panel().Find(BedFieldsEditor.SizeNode)!.gameObject.activeInHierarchy,
            "строки кровати у табуретки мертвы: человек правил бы переключатель, который "
            + "ничего не делает");
        Assert.IsFalse(Panel().Find(BedFieldsEditor.HeadboardNode)!.gameObject.activeInHierarchy,
            "и вторая строка тоже");
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
    public void Pillar_WidthAndDepthRows_AreHidden()
    {
        _menu!.Open(Pillar());
        Assert.IsFalse(Field("Ширина").gameObject.activeInHierarchy,
            "у опоры сечение задаётся диаметром: строке ширины в панели делать нечего");
        Assert.IsFalse(Field("Глубина").gameObject.activeInHierarchy, "и глубине тоже");
        Assert.IsTrue(Field("Высота").interactable, "высоту опоры править можно");
        Assert.IsTrue(Field("Диаметр").gameObject.activeInHierarchy, "а диаметр — вот он");
    }

    [Test]
    public void Pillar_DiameterRow_SetsWidthAndDepth()
    {
        var pillar = Pillar();
        _menu!.Open(pillar);

        Type("Диаметр", "120");

        Assert.AreEqual(120, pillar.DiameterMM);
        Assert.AreEqual(120, pillar.DimensionsMM.x, "ширина = диаметр");
        Assert.AreEqual(120, pillar.DimensionsMM.z, "глубина = диаметр");
    }

    [Test]
    public void Pillar_DiameterRow_ShowsTheClampedValue_NotTheTypedOne()
    {
        var pillar = Pillar();
        _menu!.Open(pillar);

        Type("Диаметр", "5");

        Assert.AreEqual(PillarElement.DiameterMM_Min, pillar.DiameterMM);
        Assert.AreEqual(PillarElement.DiameterMM_Min.ToString(), Text(Field("Диаметр")),
            "поле обязано показать принятое значение, а не набранное");
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

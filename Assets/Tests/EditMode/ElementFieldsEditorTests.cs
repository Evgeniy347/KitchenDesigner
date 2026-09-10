using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;

public class ElementFieldsEditorTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _blockOnViolation;

    /// <summary>Панель строится ОДИН раз на класс, а не в каждом из пятидесяти семи
    /// тестов: сборка контекстного меню — 0,32 с, и пятьдесят семь сборок это 18,4 с
    /// из 193-секундного прогона EditMode при бюджете 170. Причина, по которой это
    /// безопасно, разобрана в сводке <see cref="ContextMenuLayoutTests"/>: боевой
    /// сценарий — ОДНА панель, переоткрываемая через <c>Open</c>, и через <c>Open</c>
    /// проходит каждый тест этого класса без исключений (единственная протечка
    /// сброса — условная перестройка списков «Прикрепить к» и «Фасад» — закрыта в
    /// самом <c>ContextMenuUI.Open</c>).
    ///
    /// Что панель переживает тест, а состояние вокруг неё — нет, разобрано
    /// в <see cref="Setup"/>: там сбрасываются оба потестовых состояния.</summary>
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

    /// <summary>Панель переживает тест — значит всё ПОТЕСТОВОЕ состояние обязано
    /// возвращаться на место здесь, и таких состояний два.
    ///
    /// <c>BlockOnViolation</c> — глобальная настройка приложения.
    ///
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: <c>ApplyOncePerFrame</c>
    /// пропускает один Apply за кадр, а в EditMode <c>Time.frameCount</c> стоит на
    /// месте, поэтому окно, взведённое ПРЕДЫДУЩИМ тестом, молча съедало первую же
    /// правку следующего. Со своей панелью на каждый тест счётчик рождался заново и
    /// прятал это: восемь тестов падали не своими значениями, а значениями по
    /// умолчанию — «Expected 850, But was 450» и есть «Apply не позвался вовсе».
    /// В приложении кадр сменяется, поэтому лечится это здесь, а не в продукте;
    /// тот же сброс по той же причине стоит в <c>McpUiPropertyParityTests</c> и
    /// <c>ScrewLegHostSectionTests</c>.</summary>
    [SetUp]
    public void Setup()
    {
        _blockOnViolation = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
    }

    [TearDown]
    public void Teardown()
    {
        KitchenSettings.Instance.BlockOnViolation = _blockOnViolation;
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
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

    private List<string> VisibleFieldValues()
    {
        var values = new List<string>();
        foreach (var field in Panel().GetComponentsInChildren<TMP_InputField>(false))
            values.Add(field.name + "=" + Text(field));
        values.Sort(System.StringComparer.Ordinal);
        return values;
    }

    private KitchenElement Board() =>
        Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(600, 300, 18), "Полка", Vector3.zero));

    private RadialShelfElement Radial() =>
        Spawn<RadialShelfElement>(ElementFactory.CreateRadialShelf(600, 600, 18, 200, "Радиусная", Vector3.zero));

    private PillarElement Pillar() =>
        Spawn<PillarElement>(ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "Опора", Vector3.zero));

    private PipeElement Pipe() =>
        Spawn<PipeElement>(ElementFactory.CreatePipe(
            KitchenDesigner.Core.Plumbing.PipeSpec.DEFAULT_SIZE,
            PipeElementSpec.DEFAULT_LENGTH_MM, "Труба", Vector3.zero));

    private DrawerElement Drawer() =>
        Spawn<DrawerElement>(ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero));

    private StoolElement Stool() =>
        Spawn<StoolElement>(ElementFactory.CreateStool(
            new Vector3Int(StoolElement.DefaultWidthMM, StoolElement.DefaultHeightMM,
                StoolElement.DefaultDepthMM), 0, "Табуретка", Vector3.zero));

    private PouffeElement Pouffe() =>
        Spawn<PouffeElement>(ElementFactory.CreatePouffe(
            new Vector3Int(PouffeElement.DefaultWidthMM, PouffeElement.DefaultHeightMM,
                PouffeElement.DefaultDepthMM), PouffeElement.DefaultCornerRadiusMM,
            PouffeElement.DefaultSeatThicknessMM, "Пуфик", Vector3.zero));

    private ToiletElement Toilet() =>
        Spawn<ToiletElement>(ElementFactory.CreateToilet(
            ToiletElement.DefaultSeatHeightMM, "Унитаз", Vector3.zero));

    private WallHungToiletElement WallHungToilet() =>
        Spawn<WallHungToiletElement>(ElementFactory.CreateWallHungToilet(
            WallHungToiletElement.DefaultSeatHeightMM,
            WallHungToiletElement.DefaultFlushPlateHeightMM, "Инсталляция", Vector3.zero));

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
    public void Stool_BothDecorSlotRows_AreShown_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Stool());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у табуретки два декора — сиденье и ножки: она носитель IHasTwoDecorSlots");
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
    public void Chair_BothDecorSlotRows_AreShown_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Chair());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у стула два декора — сиденье со спинкой и ножки: он носитель IHasTwoDecorSlots");
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

    private SofaElement Sofa() =>
        Spawn<SofaElement>(ElementFactory.CreateSofa(
            new Vector3Int(SofaElement.DefaultWidthMM, SofaElement.DefaultHeightMM,
                SofaElement.DefaultDepthMM), SofaElement.DefaultCornerRadiusMM,
            SofaElement.DefaultSeatHeightMM, "Диван", Vector3.zero));

    /// <summary>Тот же сторож двух половин реестра, что у табуретки и стула, —
    /// у дивана его не было вовсе: обе строки жили без единого теста, и потеря
    /// вызова Build() прошла бы молча. Узлы у дивана свои
    /// (F_СкруглениеДивана, F_ВысотаОснованияДивана), потому что подписи
    /// «Скругление» и «Высота ...» он делит с другой мебелью.</summary>
    [Test]
    public void Sofa_BothRows_AreBuilt_AndOpenWithTheElementsValues()
    {
        _menu!.Open(Sofa());

        Assert.IsNotNull(Panel().Find("F_" + SofaFieldsEditor.CornerRadiusNode),
            "строка «Скругление» дивана обязана быть ПОСТРОЕНА: занести редактор в "
            + "реестр _editors и забыть позвать его Build() — значит получить свойство, "
            + "которое нечем править, и молча");
        Assert.IsNotNull(Panel().Find("F_" + SofaFieldsEditor.SeatHeightNode),
            "и строка «Высота основания» тоже");
        Assert.AreEqual(SofaElement.DefaultCornerRadiusMM.ToString(),
            Text(Field(SofaFieldsEditor.CornerRadiusNode)),
            "поле открывается значением элемента, а не пустым и не чужим");
        Assert.AreEqual(SofaElement.DefaultSeatHeightMM.ToString(),
            Text(Field(SofaFieldsEditor.SeatHeightNode)),
            "и второе поле тоже");
    }

    [Test]
    public void Sofa_CornerRadiusRow_Applies_AndIsUndoable()
    {
        var sofa = Sofa();
        _menu!.Open(sofa);

        Type(SofaFieldsEditor.CornerRadiusNode, "200");

        Assert.AreEqual(200, sofa.CornerRadiusMM, "скругление применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(SofaElement.DefaultCornerRadiusMM, sofa.CornerRadiusMM,
            "одна правка — один шаг отмены");
    }

    [Test]
    public void Sofa_SeatHeightRow_ShowsTheClampedValueBack_NotWhatWasTyped()
    {
        var sofa = Sofa();
        _menu!.Open(sofa);

        Type(SofaFieldsEditor.SeatHeightNode, "5000");

        int expected = SofaElement.MaxSeatHeightMM(SofaElement.DefaultHeightMM);
        Assert.AreEqual(expected, sofa.SeatHeightMM,
            "высота основания зажата так, чтобы спинке осталось место");
        Assert.AreEqual(expected.ToString(), Text(Field(SofaFieldsEditor.SeatHeightNode)),
            "и в поле обязана вернуться ПРИНЯТАЯ высота, а не напечатанная "
            + "(CONVENTIONS.md → «Read a value back only AFTER EndCapture»)");
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
            "у кровати два декора — каркас и постель: она носитель IHasTwoDecorSlots");
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

    /// <summary>Узел строки пуфика назван явно, а не подписью: подпись
    /// «Скругление» уже занята табуреткой, и два редактора, взявшие узел из
    /// подписи, склеили бы свои строки в одну. Тест ищет строку по имени узла —
    /// то есть проверяет ровно то соглашение, на котором это держится.</summary>
    [Test]
    public void Pouffe_CornerRadiusRow_IsBuilt_AndAppliesAndIsUndoable()
    {
        var pouffe = Pouffe();
        _menu!.Open(pouffe);
        Assume.That(Text(Field(PouffeFieldsEditor.CornerRadiusNode)), Is.EqualTo("120"));

        Type(PouffeFieldsEditor.CornerRadiusNode, "60");

        Assert.AreEqual(60, pouffe.CornerRadiusMM, "скругление применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(PouffeElement.DefaultCornerRadiusMM, pouffe.CornerRadiusMM,
            "одна правка — один шаг отмены");
    }

    [Test]
    public void Pouffe_SeatThicknessRow_AppliesAndShowsTheCLAMPEDValueBack()
    {
        var pouffe = Pouffe();
        _menu!.Open(pouffe);
        Assume.That(Text(Field(PouffeFieldsEditor.SeatThicknessNode)), Is.EqualTo("50"));

        Type(PouffeFieldsEditor.SeatThicknessNode, "10000");

        int max = PouffeElement.MaxSeatThicknessMM(PouffeElement.DefaultHeightMM);
        Assert.AreEqual(max, pouffe.SeatThicknessMM,
            "сидушка не вправе занимать больше трети высоты пуфика");
        Assert.AreEqual(max.ToString(), Text(Field(PouffeFieldsEditor.SeatThicknessNode)),
            "и поле обязано показать ПРИНЯТОЕ значение, а не набранное: значение, "
            + "записанное обратно изнутри BeginCapture, показывало бы человеку его "
            + "собственный ввод вместо того, что элемент взял");
    }

    /// <summary>Оба поля пуфика уезжают в элемент за ОДИН Apply. Тест закрывает
    /// дыру, из-за которой обе строки проверялись только поодиночке: потеря
    /// одной из них при общем обходе полей осталась бы незамеченной.
    ///
    /// Он же отвечает на вопрос о ПОРЯДКЕ записи. Порядка здесь нет: скругление
    /// зажимается по DimensionsMM.x/z, толщина сидушки — по DimensionsMM.y, а
    /// размеры к этому моменту уже применены и внутри Apply не меняются, так что
    /// ни одно из свойств не видит другого. Если такая связь когда-нибудь
    /// появится, красным станет именно этот тест — и порядок записи придётся
    /// назначать осознанно, а не наследовать от порядка строк в панели.</summary>
    [Test]
    public void Pouffe_SeatThicknessAndCornerRadius_ApplyTogether_EachClampedByTheDimensionsAlone()
    {
        var pouffe = Pouffe();
        _menu!.Open(pouffe);

        Field(PouffeFieldsEditor.SeatThicknessNode).text = "10000";
        Type(PouffeFieldsEditor.CornerRadiusNode, "1000");

        int maxRadius = PouffeElement.MaxCornerRadiusMM(pouffe.DimensionsMM);
        int maxThickness = PouffeElement.MaxSeatThicknessMM(PouffeElement.DefaultHeightMM);
        Assert.AreEqual(maxRadius, pouffe.CornerRadiusMM,
            "скругление зажато половиной меньшей стороны — и только ею");
        Assert.AreEqual(maxThickness, pouffe.SeatThicknessMM,
            "сидушка зажата третью высоты — и только ею; вторая строка обязана "
            + "доехать в том же Apply, что и первая");
        Assert.AreEqual(maxRadius.ToString(), Text(Field(PouffeFieldsEditor.CornerRadiusNode)),
            "оба поля показывают ПРИНЯТОЕ значение, а не набранное");
        Assert.AreEqual(maxThickness.ToString(),
            Text(Field(PouffeFieldsEditor.SeatThicknessNode)),
            "и второе — тоже принятое: 10000 мм сидушки в 400-мм пуфик не помещается");
    }

    [Test]
    public void Pouffe_ShowsUpholsteryAndSeatRows_InsteadOfThePlainTextureRow()
    {
        _menu!.Open(Pouffe());

        Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
            "у пуфика два декора — обивка и сидушка: он носитель IHasTwoDecorSlots");
        Assert.IsTrue(Panel().Find("CtxTableLegs")!.gameObject.activeInHierarchy,
            "второй слот тоже обязан быть виден");
        Assert.IsFalse(Panel().Find("CtxMaterial")!.gameObject.activeInHierarchy,
            "общая строка «Текстура» у носителя двух слотов скрыта");
    }

    [Test]
    public void PouffeRows_AreHiddenForOtherFurniture()
    {
        _menu!.Open(Pouffe());
        _menu!.Open(Stool());

        Assert.IsFalse(
            Panel().Find("F_" + PouffeFieldsEditor.SeatThicknessNode)!.gameObject
                .activeInHierarchy,
            "строка «Толщина сидушки» у табуретки мертва: сидушки у неё нет, и человек "
            + "правил бы поле, которое ничего не делает");
        Assert.IsFalse(
            Panel().Find("F_" + PouffeFieldsEditor.CornerRadiusNode)!.gameObject
                .activeInHierarchy,
            "и своя строка скругления тоже: у табуретки есть СВОЁ поле с той же "
            + "подписью, и показать оба разом значило бы дать человеку два поля с "
            + "одним смыслом, из которых работает одно");
    }

    /// <summary>Один редактор обслуживает оба варианта унитаза, и строка
    /// «Высота чаши» у них ОБЩАЯ — это и есть то, что тут стережётся: узел один,
    /// а элементов два, и запись обязана попадать в тот, что открыт сейчас.</summary>
    [Test]
    public void Toilet_SeatHeightRow_IsBuilt_AndAppliesAndIsUndoable()
    {
        var toilet = Toilet();
        _menu!.Open(toilet);
        Assume.That(Text(Field(ToiletFieldsEditor.SeatHeightNode)), Is.EqualTo("400"));

        Type(ToiletFieldsEditor.SeatHeightNode, "460");

        Assert.AreEqual(460, toilet.SeatHeightMM, "высота чаши применяется вместе с размерами");
        CommandStack.Undo();
        Assert.AreEqual(ToiletElement.DefaultSeatHeightMM, toilet.SeatHeightMM,
            "одна правка — один шаг отмены");
    }

    [Test]
    public void Toilet_SeatHeightRow_ShowsTheCLAMPEDValueBack()
    {
        var toilet = Toilet();
        _menu!.Open(toilet);

        Type(ToiletFieldsEditor.SeatHeightNode, "10000");

        Assert.AreEqual(ToiletElement.MaxSeatHeightMM, toilet.SeatHeightMM,
            "чаша не вправе подняться настолько, чтобы бачку осталось меньше минимума");
        Assert.AreEqual(ToiletElement.MaxSeatHeightMM.ToString(),
            Text(Field(ToiletFieldsEditor.SeatHeightNode)),
            "поле обязано показать ПРИНЯТОЕ значение, а не набранное: иначе человек "
            + "видит собственный ввод вместо того, что элемент взял");
    }

    [Test]
    public void WallHungToilet_ShowsBothRows_WhileTheCompactShowsOnlyTheSeat()
    {
        _menu!.Open(WallHungToilet());
        Assert.IsTrue(
            Panel().Find("F_" + ToiletFieldsEditor.FlushPlateHeightNode)!.gameObject
                .activeInHierarchy,
            "у подвесного панель смыва есть, и её высота обязана быть видна");

        _menu!.Open(Toilet());
        Assert.IsTrue(
            Panel().Find("F_" + ToiletFieldsEditor.SeatHeightNode)!.gameObject
                .activeInHierarchy,
            "общая строка обязана пережить смену выделения между двумя вариантами");
        Assert.IsFalse(
            Panel().Find("F_" + ToiletFieldsEditor.FlushPlateHeightNode)!.gameObject
                .activeInHierarchy,
            "у напольного панели смыва нет: видимая строка правила бы ничто");
    }

    [Test]
    public void WallHungToilet_FlushPlateRow_AppliesAndIsPushedUpByTheBowl()
    {
        var toilet = WallHungToilet();
        _menu!.Open(toilet);
        Assume.That(Text(Field(ToiletFieldsEditor.FlushPlateHeightNode)), Is.EqualTo("600"));

        Type(ToiletFieldsEditor.SeatHeightNode, "600");

        Assert.AreEqual(600, toilet.SeatHeightMM, "чаша поднялась на предел");
        Assert.AreEqual(WallHungToiletElement.MinFlushPlateHeightMM(600),
            toilet.FlushPlateHeightMM,
            "поднятая чаша ТОЛКАЕТ панель вверх: связь односторонняя, и без неё панель "
            + "оказалась бы под крышкой унитаза");
        Assert.AreEqual(toilet.FlushPlateHeightMM.ToString(),
            Text(Field(ToiletFieldsEditor.FlushPlateHeightNode)),
            "и вторая строка обязана показать новое значение, хотя правили первую");
    }

    [Test]
    public void ToiletRows_AreHiddenForOtherFurniture()
    {
        _menu!.Open(Toilet());
        _menu!.Open(Stool());

        Assert.IsFalse(
            Panel().Find("F_" + ToiletFieldsEditor.SeatHeightNode)!.gameObject
                .activeInHierarchy,
            "строка «Высота чаши» у табуретки мертва: правка поля, которое ничего "
            + "не делает, — худший вид молчаливого отказа");
    }

    [Test]
    public void Toilets_ShowCeramicAndButtonRows_InsteadOfThePlainTextureRow()
    {
        foreach (var toilet in new KitchenElement[] { Toilet(), WallHungToilet() })
        {
            _menu!.Open(toilet);

            Assert.IsTrue(Panel().Find("CtxTableTop")!.gameObject.activeInHierarchy,
                "у унитаза два декора — керамика и хром: он носитель IHasTwoDecorSlots");
            Assert.IsTrue(Panel().Find("CtxTableLegs")!.gameObject.activeInHierarchy,
                "второй слот тоже обязан быть виден");
            Assert.IsFalse(Panel().Find("CtxMaterial")!.gameObject.activeInHierarchy,
                "общая строка «Текстура» у носителя двух слотов скрыта");
        }
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
    public void Pillar_WidthAndDepthRows_StayOnScreen_AndShowTheDiameter()
    {
        var pillar = Pillar();
        _menu!.Open(pillar);

        Assert.IsTrue(Field("Ширина").gameObject.activeInHierarchy,
            "габарит колонны идёт в спецификацию: строку ширины прячут — прочитать её негде");
        Assert.IsTrue(Field("Глубина").gameObject.activeInHierarchy, "и глубину тоже");
        Assert.AreEqual(pillar.DiameterMM.ToString(), Text(Field("Ширина")),
            "ширина колонны = диаметр: строка обязана показывать ту же величину, что и геометрия");
        Assert.AreEqual(pillar.DiameterMM.ToString(), Text(Field("Глубина")), "и глубина тоже");
        Assert.IsTrue(Field("Высота").interactable, "высоту опоры править можно");
        Assert.IsTrue(Field("Диаметр").gameObject.activeInHierarchy, "а диаметр — вот он");
    }

    [Test]
    public void Pillar_WidthAndDepthRows_AreLocked_BecauseTheDiameterOwnsThem()
    {
        _menu!.Open(Pillar());

        foreach (var row in new[] { "Ширина", "Глубина" })
        {
            Assert.IsFalse(Field(row).interactable,
                $"сечение колонны задаёт диаметр: поле «{row}» — второе описание той же величины, "
                + "править его нельзя");
            Assert.IsTrue(Field(row).readOnly, $"и печатать в «{row}» тоже нельзя");
        }
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

    private TMP_Text Label(string label) =>
        Panel().Find($"L_{label}")!.GetComponent<TMP_Text>();

    private static readonly string[] PipeDerivedRows =
        { "Наружный Ø", "Внутренний Ø", "Толщина стенки" };

    /// <summary>Строки трубы, которые ЧИТАЮТ: диаметры и толщина стенки. Прятать
    /// их нельзя — они идут в заказ, а править нельзя — их считает ГОСТ по
    /// условному проходу (docs/UI-GUIDELINES.md §9).</summary>
    [Test]
    public void Pipe_DerivedRows_StayOnScreen_AndShowTheTableValues()
    {
        _menu!.Open(Pipe());

        foreach (var row in PipeDerivedRows)
            Assert.IsTrue(Field(row).gameObject.activeInHierarchy,
                $"строка «{row}» обязана остаться на экране: её читают, а не печатают в неё");

        Assert.AreEqual("26.8", Text(Field("Наружный Ø")), "ДУ 20 по ГОСТ 3262-75");
        Assert.AreEqual("21.2", Text(Field("Внутренний Ø")));
        Assert.AreEqual("2.8", Text(Field("Толщина стенки")));
    }

    [Test]
    public void Pipe_DerivedRows_AreLocked_AndTheirLabelsAreDimmedWithThem()
    {
        _menu!.Open(Pipe());

        foreach (var row in PipeDerivedRows)
        {
            Assert.IsFalse(Field(row).interactable,
                $"«{row}» считает геометрия: поле, в которое человек правит ту же величину, — "
                + "это второе её описание, и они разойдутся");
            Assert.IsTrue(Field(row).readOnly, $"и печатать в «{row}» тоже нельзя");
            Assert.AreEqual(UIStyle.TextDisabled, Label(row).color,
                $"яркая подпись у мёртвого поля читается как «сюда можно печатать» — «{row}»");
        }
    }

    [Test]
    public void Pipe_WidthAndDepthRows_AreLocked_BecauseTheBoreOwnsThem()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        foreach (var row in new[] { "Ширина", "Глубина" })
        {
            Assert.IsFalse(Field(row).interactable,
                $"сечение трубы задаёт условный проход, а не «{row}»");
            Assert.IsTrue(Field(row).readOnly);
            Assert.AreEqual(pipe.SectionMM.ToString(), Text(Field(row)),
                "габаритная строка обязана показывать то же сечение, что и геометрия");
        }

        Assert.IsTrue(Field("Высота").interactable,
            "длина трубы — единственный её размер, и править его можно");
    }

    [Test]
    public void Pipe_HeightRow_IsTheLengthOfTheRun()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        Type("Высота", "1450");

        Assert.AreEqual(1450, pipe.LengthMM, "у трубы «высота» — это длина трассы");
        Assert.AreEqual(1450, pipe.DimensionsMM.y);
    }

    /// <summary>Выпадающий список — ЕДИНСТВЕННЫЙ писатель сечения. Одна правка
    /// обязана прокатиться по всем трём производным строкам И по габариту: строка,
    /// оставшаяся со старым числом, — это ровно тот случай, ради которого §9
    /// запрещает второе поле для одной величины.</summary>
    [Test]
    public void Pipe_BoreDropdown_MovesEveryDerivedRowAndTheSection_AtOnce()
    {
        var pipe = Pipe();
        _menu!.Open(pipe);

        Dropdown("CtxPipeSize").value =
            PipeElementSpec.IndexOf(KitchenDesigner.Core.Plumbing.PipeSpec.Dn40);

        Assert.AreEqual(KitchenDesigner.Core.Plumbing.PipeSpec.Dn40, pipe.SizeId);
        Assert.AreEqual(48, pipe.DimensionsMM.x, "ДУ 40 — наружный 48,0");
        Assert.AreEqual(48, pipe.DimensionsMM.z);
        Assert.AreEqual("48", Text(Field("Наружный Ø")));
        Assert.AreEqual("41", Text(Field("Внутренний Ø")), "48,0 − 2 × 3,5");
        Assert.AreEqual("3.5", Text(Field("Толщина стенки")));
        Assert.AreEqual(PipeElementSpec.DEFAULT_LENGTH_MM, pipe.LengthMM,
            "смена диаметра не имеет права трогать длину: это разные величины");
    }

    /// <summary>PipeElement хранит длину трассы в DimensionsMM.y — том же поле,
    /// что у обычной детали держит высоту. Подпись строки обязана меняться
    /// вместе со смыслом поля, а не только у трубы: у любой другой детали
    /// (и после закрытия трубы) она обязана вернуться к «Высота».</summary>
    [Test]
    public void Pipe_HeightRowLabel_ReadsLength_NotHeight()
    {
        _menu!.Open(Pipe());
        Assert.AreEqual("Длина", Label("Высота").text,
            "у трубы Y-размер DimensionsMM — это длина трассы, а не высота");
    }

    [Test]
    public void OrdinaryElement_HeightRowLabel_StaysHeight_EvenAfterAPipeWasShown()
    {
        _menu!.Open(Pipe());
        _menu!.Open(Board());

        Assert.AreEqual("Высота", Label("Высота").text,
            "переключение с трубы на обычную деталь обязано вернуть подпись «Высота» — "
            + "иначе ярлык одной детали протекает в панель другой");
    }

    /// <summary>«Прикрепить к» — общий механизм «деталь едет за родителем». У трубы и
    /// фитингов связи держатся на устьях портов (SnapPortAt), а не на этом поле — оно
    /// показывало бы список соседей и путало.</summary>
    [Test]
    public void Pipe_AndFitting_HaveNoAttachToRow()
    {
        _menu!.Open(Pipe());
        Assert.IsFalse(Panel().Find("CtxAttachTo")!.gameObject.activeInHierarchy,
            "у трубы связи держатся на устьях портов, а не на поле «Прикрепить к»");

        _menu!.Open(Elbow());
        Assert.IsFalse(Panel().Find("CtxAttachTo")!.gameObject.activeInHierarchy,
            "у отвода — тоже: он стыкуется портами, как и труба");
    }

    [Test]
    public void OrdinaryElement_HasAnAttachToRow()
    {
        _menu!.Open(Board());
        Assert.IsTrue(Panel().Find("CtxAttachTo")!.gameObject.activeInHierarchy,
            "обычная деталь по-прежнему может ехать за родителем через «Прикрепить к» — "
            + "это не отключено везде, а только у сантехники");
    }

    private PipeFittingElement Elbow() =>
        Spawn<PipeFittingElement>(ElementFactory.CreatePipeElbow("Otvod", Vector3.zero));

    private PipeFittingElement Tee() =>
        Spawn<PipeFittingElement>(ElementFactory.CreatePipeTee("Troynik", Vector3.zero));

    private PipeFittingElement Cap() =>
        Spawn<PipeFittingElement>(ElementFactory.CreatePipeCap("Zaglushka", Vector3.zero));

    /// <summary>Фитинг, к которому ничего не подведено. Диаметр у него не
    /// «пока не задан», а НЕИЗВЕСТЕН: его неоткуда взять, пока в порт не пришла
    /// труба. Показать здесь ДУ 20 по умолчанию — худшее из возможного: человек
    /// прочитает число, которого сцена не утверждала, и закажет по нему.
    /// Поэтому прочерк, и поэтому строка остаётся на месте (§9): исчезнувшая
    /// строка не отличима от «у этого фитинга диаметров не бывает».</summary>
    [Test]
    public void Fitting_WithNothingConnected_ShowsADashInsteadOfADefaultBore()
    {
        _menu!.Open(Elbow());

        Assert.IsTrue(Field("Диаметр 1").gameObject.activeInHierarchy,
            "строка диаметра обязана остаться на экране даже пустой");
        Assert.AreEqual(PipeSpec.NoValue, Text(Field("Диаметр 1")),
            "к порту ничего не подведено — значение брать неоткуда");
        Assert.AreEqual(PipeSpec.NoValue, Text(Field("Диаметр 2")));
    }

    [Test]
    public void Fitting_BoreRows_AreLocked_AndTheirLabelsAreDimmedWithThem()
    {
        _menu!.Open(Tee());

        foreach (var row in new[] { "Диаметр 1", "Диаметр 2", "Диаметр 3" })
        {
            Assert.IsFalse(Field(row).interactable,
                $"«{row}» читается с трассы: поле, в которое человек впишет свой диаметр, — "
                + "это второй писатель той же величины, и он разойдётся с PipeSurvey");
            Assert.IsTrue(Field(row).readOnly, $"и печатать в «{row}» тоже нельзя");
            Assert.AreEqual(UIStyle.TextDisabled, Label(row).color,
                $"яркая подпись у мёртвого поля читается как «сюда можно печатать» — «{row}»");
        }
    }

    /// <summary>Число строк равно числу портов, и это НЕ то же самое, что
    /// «строку прячут, когда в ней пусто». У заглушки второго диаметра не
    /// существует вовсе, а у тройника третий существует и просто пуст — первый
    /// случай строку убирает, второй обязан её оставить с прочерком.</summary>
    [Test]
    public void Fitting_ShowsOneBoreRowPerPort_NotOnePerFilledValue()
    {
        _menu!.Open(Cap());
        Assert.IsTrue(Field("Диаметр 1").gameObject.activeInHierarchy,
            "у заглушки один порт — и ровно одна строка диаметра");
        Assert.IsFalse(Field("Диаметр 2").gameObject.activeInHierarchy,
            "второго диаметра у заглушки не бывает: пустая строка соврала бы, что бывает");

        _menu!.Open(Tee());
        Assert.IsTrue(Field("Диаметр 3").gameObject.activeInHierarchy,
            "у тройника три порта, и третья строка обязана появиться — пустой, но появиться");
    }

    [Test]
    public void Fitting_SizeRows_AreAllLocked_BecauseNothingAboutItIsTyped()
    {
        var elbow = Elbow();
        _menu!.Open(elbow);

        foreach (var row in new[] { "Ширина", "Высота", "Глубина" })
            Assert.IsFalse(Field(row).interactable,
                $"габарит фитинга выводится из диаметра трассы, а не печатается в «{row}»");

        Assert.AreEqual(elbow.DerivedDimensionsMM, elbow.DimensionsMM,
            "и он обязан совпадать с вычисленным, а не жить своей жизнью");
    }

    /// <summary>Что видно в панели ПОСЛЕ переключения выделения. В приложении
    /// панель одна: её не пересобирают, а переоткрывают на другой детали, — и
    /// вопрос «не остались ли в полях числа от предыдущей» не должен жить только
    /// внутри чужих тестов про клампы. Проверяются оба направления: сначала
    /// открыта только опора, потом стул с правкой, потом снова опора — все её
    /// ВИДИМЫЕ поля обязаны совпасть с первым разом, — и обратно на стул, где
    /// стоит его собственная, только что применённая высота сиденья.
    ///
    /// Сравнение по «видимым полям», а не по всем: строку, которой у детали нет,
    /// прячет <c>RowVisibility</c>, и невидимый текст пользователю не показывают.
    /// Если строка когда-нибудь станет видна шире, чем её редактор отвечает
    /// <c>Handles</c>, чужое значение станет видимым — и упадёт этот тест.</summary>
    [Test]
    public void SelectionSwitch_ReReadsTheVisibleFields_FromTheNewlySelectedElement()
    {
        var chair = Chair();
        var pillar = Pillar();
        pillar.transform.position += new Vector3(2f, 0f, 0f);

        _menu!.Open(pillar);
        var pillarAlone = VisibleFieldValues();
        Assume.That(pillarAlone, Has.Member("F_Диаметр=" + pillar.DiameterMM),
            "диаметр опоры обязан быть среди видимых полей — иначе сравнивать нечего");

        _menu!.Open(chair);
        Assume.That(Text(Field(ChairFieldsEditor.SeatHeightNode)),
            Is.EqualTo(AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT.ToString()));
        Type(ChairFieldsEditor.SeatHeightNode, "500");
        Assume.That(chair.SeatHeightMM, Is.EqualTo(500), "правка обязана примениться");

        _menu!.Open(pillar);
        Assert.AreEqual(pillar.DiameterMM.ToString(), Text(Field("Диаметр")),
            "у опоры в поле диаметра — её диаметр, а не то, что осталось от стула");
        CollectionAssert.AreEqual(pillarAlone, VisibleFieldValues(),
            "все видимые поля опоры обязаны читаться заново: панель ОДНА, и переключение "
            + "выделения — единственный боевой способ её сбросить");

        _menu!.Open(chair);
        Assert.AreEqual("500", Text(Field(ChairFieldsEditor.SeatHeightNode)),
            "и обратно на стул — его собственная высота сиденья, применённая до переключения");
    }

    /// <summary>Спрятанная строка тоже обязана расстаться с числом предыдущей
    /// детали. Видно её сейчас или нет — решает <c>RowVisibility</c> (набор фасетов),
    /// а текст пишет <c>Show</c> у редактора, и он спрашивает <c>Handles</c> (тип):
    /// два разных вопроса об одном и том же. Пока ответы совпадают, чужое число
    /// прячется — расширь фасет строки на один тип, и оно станет видимым, причём
    /// зелёными останутся все тесты. Поэтому <c>NumberFieldsEditor</c> пишет поля
    /// БЕЗУСЛОВНО, отдавая «холостой» текст той детали, которой это свойство не
    /// принадлежит (CONVENTIONS.md → conventions/CORRECTNESS.md, «Not only the
    /// guard — every READER of a state must ask through one function»).</summary>
    [Test]
    public void SelectionSwitch_EvenAHiddenRow_DropsThePreviousElementsNumber()
    {
        var chair = Chair();
        _menu!.Open(chair);
        Type(ChairFieldsEditor.SeatHeightNode, "500");
        Assume.That(Text(Field(ChairFieldsEditor.SeatHeightNode)), Is.EqualTo("500"));

        _menu!.Open(Board());

        var row = Field(ChairFieldsEditor.SeatHeightNode);
        Assume.That(row.gameObject.activeInHierarchy, Is.False,
            "у полки нет высоты сиденья — строка спрятана");
        Assert.AreEqual("0", Text(row),
            "в спрятанной строке — холостое значение, а не 500 мм от стула");
    }
}

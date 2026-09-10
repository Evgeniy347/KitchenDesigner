using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Подписи двух строк декора в контекстном меню принадлежат САМОМУ элементу:
/// «Столешница» и «Ножки» верны для стола и врут для всего остального — у
/// табуретки и стула это сиденье, у дивана вообще нет ни столешницы, ни ножек,
/// а есть обивка и подушки.
///
/// Ответ даёт элемент (<c>IHasTwoDecorSlots.PrimarySlotLabel</c> /
/// <c>SecondarySlotLabel</c>), а не лестница по типу в UI — этого требует
/// CONVENTIONS.md → «Element type checks live in ONE place per layer», и за
/// слоем UI следит <c>UiElementTypeLadderTests</c>.
///
/// Правка КОСМЕТИЧЕСКАЯ: <c>MaterialSlot</c> и сериализуемые id декоров не
/// менялись — это пришпилено круговым тестом в конце файла, иначе «просто
/// переименовали подпись» однажды окажется сменой формата сохранения.
/// </summary>
public class MaterialSlotLabelTests
{
    private const string LabelNotWritten = "‹подпись не перезаписана›";

    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _globals;

    /// <summary>Панель строится ОДИН раз на класс: Build стоит ~0,3 с, а Open —
    /// ~5 мс, и тринадцать сборок были дороже всей остальной работы набора вместе.
    /// Всё, что панель помнит между тестами, приводится в порядок ниже: защёлка
    /// кадра правок, фокус поля, глобальные настройки — и сами подписи декора,
    /// которые при общей панели пережили бы тест и позеленили бы следующий на
    /// чужом тексте (см. <see cref="PoisonSlotLabels"/>).</summary>
    [OneTimeSetUp]
    public void BuildPanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DestroyPanelOnce()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    [SetUp]
    public void Setup()
    {
        _globals = ProjectLoadStateGuard.Capture();
        KitchenSettings.Instance.BlockOnViolation = false;
        if (_menu != null) ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        Unfocus();
        PoisonSlotLabels();
    }

    [TearDown]
    public void Teardown()
    {
        if (_menu != null) _menu!.Close();
        CommandStack.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        if (_globals != null) _globals!.Restore();
    }

    /// <summary>Подписи — единственное, что этот набор читает из панели, и общая
    /// панель отдала бы их следующему тесту как есть. Заведомо неверный текст
    /// перед каждым тестом превращает каждое сравнение в доказательство того,
    /// что ShowFor написал подпись ИМЕННО этого элемента, а не донёс её от
    /// предыдущего.</summary>
    private void PoisonSlotLabels()
    {
        if (_canvas == null) return;
        SlotLabel(ContextMenuMaterialSection.PrimarySlotLabelNode).text = LabelNotWritten;
        SlotLabel(ContextMenuMaterialSection.SecondarySlotLabelNode).text = LabelNotWritten;
    }

    private static void Unfocus()
    {
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) es.SetSelectedGameObject(null);
    }

    private KitchenElement Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement Table() => Spawn(ElementFactory.CreateTable(
        new Vector3Int(1200, 750, 600), "Стол", Vector3.zero));

    private KitchenElement RadiusTable() => Spawn(ElementFactory.CreateRadiusTable(
        new Vector3Int(1200, 750, 600), "Радиусный стол", Vector3.zero));

    private KitchenElement Stool() => Spawn(ElementFactory.CreateStool(
        new Vector3Int(StoolElement.DefaultWidthMM, StoolElement.DefaultHeightMM,
            StoolElement.DefaultDepthMM), 0, "Табуретка", Vector3.zero));

    private KitchenElement Chair() => Spawn(ElementFactory.CreateChair(
        new Vector3Int(ChairElement.DefaultWidthMM, ChairElement.DefaultHeightMM,
            ChairElement.DefaultDepthMM), 0, AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT,
        "Стул", Vector3.zero));

    private KitchenElement Sofa() => Spawn(ElementFactory.CreateSofa(
        new Vector3Int(SofaElement.DefaultWidthMM, SofaElement.DefaultHeightMM,
            SofaElement.DefaultDepthMM), SofaElement.DefaultCornerRadiusMM,
        SofaElement.DefaultSeatHeightMM, "Диван", Vector3.zero));

    private KitchenElement Bed() => Spawn(ElementFactory.CreateBed(
        BedLayout.DefaultDimensions(true, true), true, true, "Кровать", Vector3.zero));

    private KitchenElement Pouffe() => Spawn(ElementFactory.CreatePouffe(
        new Vector3Int(PouffeElement.DefaultWidthMM, PouffeElement.DefaultHeightMM,
            PouffeElement.DefaultDepthMM), PouffeElement.DefaultCornerRadiusMM,
        PouffeElement.DefaultSeatThicknessMM, "Пуфик", Vector3.zero));

    private KitchenElement Toilet() => Spawn(ElementFactory.CreateToilet(
        ToiletElement.DefaultSeatHeightMM, "Унитаз", Vector3.zero));

    private KitchenElement WallHungToilet() => Spawn(ElementFactory.CreateWallHungToilet(
        WallHungToiletElement.DefaultSeatHeightMM,
        WallHungToiletElement.DefaultFlushPlateHeightMM, "Инсталляция", Vector3.zero));

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_Text SlotLabel(string node) =>
        Panel().Find(node)!.GetComponent<TMP_Text>();

    private string LabelText(string node) => SlotLabel(node).text;

    private (string top, string legs) LabelsFor(KitchenElement element)
    {
        _menu!.Open(element);
        return (LabelText(ContextMenuMaterialSection.PrimarySlotLabelNode),
            LabelText(ContextMenuMaterialSection.SecondarySlotLabelNode));
    }

    [Test]
    public void MaterialRows_OfATable_AreCalledTabletopAndLegs()
    {
        Assert.AreEqual(("Столешница", "Ножки"), LabelsFor(Table()),
            "у стола подписи и были правильными — правка не должна их шевелить");
    }

    [Test]
    public void MaterialRows_OfARadiusTable_AreCalledTabletopAndLegs()
    {
        Assert.AreEqual(("Столешница", "Ножки"), LabelsFor(RadiusTable()),
            "радиусный стол — тот же стол: крышка и ножки");
    }

    [Test]
    public void MaterialRows_OfAStool_AreCalledSeatAndLegs()
    {
        Assert.AreEqual(("Сиденье", "Ножки"), LabelsFor(Stool()),
            "у табуретки верхняя плоскость — сиденье, а не столешница");
    }

    [Test]
    public void MaterialRows_OfAChair_AreCalledSeatAndLegs()
    {
        Assert.AreEqual(("Сиденье", "Ножки"), LabelsFor(Chair()),
            "у стула этот слот красит сиденье ВМЕСТЕ со спинкой, и «Сиденье» — "
            + "осознанный выбор, а не недосмотр: подпись строки шириной 126 px, "
            + "«Сиденье и спинка» туда не влезает. Не «чините» это на длинную "
            + "формулировку — она обрежется");
    }

    [Test]
    public void MaterialRows_OfASofa_AreCalledUpholsteryAndCushions()
    {
        Assert.AreEqual(("Обивка", "Подушки"), LabelsFor(Sofa()),
            "у дивана нет ни столешницы, ни ножек: слоты красят корпус и подушки "
            + "(SofaElement.SetPrimaryMaterial → Body, SetSecondaryMaterial → Cushions)");
    }

    [Test]
    public void MaterialRows_OfABed_AreCalledCarcassAndMattress()
    {
        Assert.AreEqual(("Каркас", "Матрас"), LabelsFor(Bed()),
            "у кровати нет ни столешницы, ни сиденья: первый слот красит деревянную часть "
            + "целиком — царгу, спинку и ножки (BedElement.SetPrimaryMaterial), второй — "
            + "постель, матрас вместе с подушками (SetSecondaryMaterial). Разделение проходит "
            + "там, где его видит человек: дерево против ткани");
    }

    [Test]
    public void MaterialRows_OfAPouffe_AreCalledUpholsteryAndSeat()
    {
        Assert.AreEqual(("Обивка", "Сиденье"), LabelsFor(Pouffe()),
            "у пуфика нет ни столешницы, ни ножек: первый слот красит обитую тумбу "
            + "(PouffeElement.SetPrimaryMaterial → Body), второй — мягкую сидушку "
            + "(SetSecondaryMaterial → Seat). «Обивка» совпадает с диваном намеренно — это "
            + "одна и та же вещь, — а вторая подпись расходится: у дивана подушки "
            + "лежат НА сиденье, у пуфика сидушка сиденьем и является");
    }

    [Test]
    public void MaterialRows_OfACompactToilet_AreCalledCeramicAndButton()
    {
        Assert.AreEqual(("Керамика", "Кнопка"), LabelsFor(Toilet()),
            "у унитаза нет ни столешницы, ни сиденья в смысле мебели: первый слот красит "
            + "всю керамику разом — пьедестал, чашу, сиденье, крышку и бачок, — второй "
            + "только кнопку смыва. Разделение проходит там, где его видит человек: "
            + "фарфор против металла");
    }

    [Test]
    public void MaterialRows_OfAWallHungToilet_AreCalledCeramicAndPlate()
    {
        Assert.AreEqual(("Керамика", "Панель"), LabelsFor(WallHungToilet()),
            "первая подпись совпадает с напольным намеренно — это та же керамика, — а "
            + "вторая расходится: у напольного во втором слоте одна кнопка на бачке, у "
            + "подвесного вся панель смыва вместе с двумя кнопками");
    }

    [Test]
    public void MaterialRow_OfAStool_NeverSaysTabletop()
    {
        var (top, legs) = LabelsFor(Stool());

        Assert.AreNotEqual("Столешница", top,
            "«Столешница» у табуретки — исходный дефект этой правки: слово стола, "
            + "показанное всей мебели подряд");
        Assert.AreNotEqual("Столешница", legs);
    }

    [Test]
    public void MaterialRowLabels_FollowTheSelection_WhenTheMenuIsReopened()
    {
        LabelsFor(Sofa());

        Assert.AreEqual(("Сиденье", "Ножки"), LabelsFor(Stool()),
            "строки в панели одни на все типы: подпись обязана переключаться при "
            + "смене выделения, иначе табуретка донашивает подписи дивана");
    }

    [Test]
    public void MaterialSlotLabels_LeaveTheSerializedDecorIdsAlone()
    {
        foreach (var element in new[] { Table(), RadiusTable(), Stool(), Chair(), Sofa(), Bed(),
            Pouffe(), Toilet(), WallHungToilet() })
        {
            var slots = (IHasTwoDecorSlots)element;
            slots.PrimaryMaterialId = "oak";
            slots.SecondaryMaterialId = "concrete";

            var json = JsonUtility.ToJson(ElementCapture.FromElement(element));
            var restored = JsonUtility.FromJson<ElementData>(json);

            var what = element.DisplayTypeName;
            StringAssert.Contains("tabletopMaterialId", json,
                what + ": имя поля — часть формата сохранения, подпись в меню его не трогает");
            StringAssert.Contains("legsMaterialId", json,
                what + ": то же самое для второго слота");
            Assert.AreEqual("oak", restored.tabletopMaterialId,
                what + ": id декора обязан пережить круг — файл, записанный до "
                + "переименования подписей, обязан читаться так же");
            Assert.AreEqual("concrete", restored.legsMaterialId,
                what + ": и второй слот тоже; слоты не поменялись местами");
        }
    }

    [Test]
    public void MaterialSlotEnum_KeepsItsThreeNamesAndOrder()
    {
        Assert.AreEqual(new[] { "Base", "Tabletop", "Legs" },
            System.Enum.GetNames(typeof(MaterialSlot)),
            "MaterialSlot едет в командах и в пипетке; переименование подписей в "
            + "меню не имеет права его касаться");
    }
}

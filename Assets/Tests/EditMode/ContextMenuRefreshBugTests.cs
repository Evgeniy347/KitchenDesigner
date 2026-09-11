using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Тесты на баг: при внешнем изменении элемента (ресайз через ручки, переименование)
/// поля в ContextMenuUI не обновляются — нужна пере-выборка элемента.
/// </summary>
public class ContextMenuRefreshBugTests
{
    private GameObject? _canvasGo;
    private GameObject? _ctxGo;
    private GameObject? _eventSystemGo;
    private ContextMenuUI? _ctx;
    private KitchenElement? _element;

    /// <summary>Панель строится ОДИН раз на класс: <c>Build</c> стоит ~0,31 с против
    /// ~5 мс у <c>Open</c>, и двенадцать сборок — это 3,7 с прогона EditMode при
    /// бюджете 170 с. Боевой сценарий и есть ОДНА панель, переоткрываемая через
    /// <c>Open</c>: каждый тест этого класса через <c>Open</c> проходит.
    ///
    /// Класс при этом ровно про то, чем общая панель опасна — «панель показала
    /// СТАРОЕ». Поэтому у каждого теста здесь свои неповторяющиеся числа и имена, а
    /// <see cref="OpeningASecondElement_ShowsTheSecondElementsValues"/> открывает A,
    /// потом B и требует значений B: совпадение с предыдущим тестом больше не может
    /// выдать невыполненную перерисовку за выполненную.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        _canvasGo = new GameObject("Canvas");
        var canvas = _canvasGo!.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo!.AddComponent<CanvasScaler>();
        _canvasGo!.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            _eventSystemGo = new GameObject("EventSystem");
            _eventSystemGo!.AddComponent<UnityEngine.EventSystems.EventSystem>();
            _eventSystemGo!.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _ctxGo = new GameObject("CtxMenu");
        _ctxGo!.transform.SetParent(_canvasGo!.transform);
        _ctx = _ctxGo!.AddComponent<ContextMenuUI>();
        _ctx!.Build(_canvasGo!.transform);
    }

    /// <summary>Уничтожается только та <c>EventSystem</c>, которую завёл этот класс:
    /// чужую, оставленную соседним набором, потестовый <c>DestroyImmediate</c> сносил
    /// вместе со своей.</summary>
    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_ctx != null) Object.DestroyImmediate(_ctx!.gameObject);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        if (_eventSystemGo != null) Object.DestroyImmediate(_eventSystemGo);
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается
    /// здесь. <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего. Фокус снимается потому, что
    /// <c>RefreshUnfocused</c> МОЛЧА пропускает сфокусированное поле — а этот класс
    /// целиком про то, обновилось поле или нет.</summary>
    [SetUp]
    public void Setup()
    {
        ((IContextMenuHost)_ctx!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);

        _element = CreateBoard("Доска", new Vector3Int(400, 400, 18), Vector3.zero);
        _ctx!.Open(_element);
    }

    /// <summary><c>Close()</c> идёт ДО уничтожения элементов и БЕЗУСЛОВНО: он обнуляет
    /// <c>_target</c>. Прежняя оговорка <c>_element != null</c> молчала как раз в тех
    /// тестах, которые сносят элементы сами (<c>DestroyAllElements</c>) — со своей
    /// панелью на тест это сходило с рук, с общей панель осталась бы держать
    /// уничтоженный элемент.</summary>
    [TearDown]
    public void Teardown()
    {
        if (_ctx != null) _ctx!.Close();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    // ── БАГ: поля размеров не обновляются при внешнем ресайзе ───────────

    [Test]
    public void Dimensions_DoNotUpdate_WhenElementResizedExternally()
    {
        _element!.DimensionsMM = new Vector3Int(803, 607, 21);
        CallRefreshTransformFields();

        Assert.AreEqual("803", FieldText("F_Ширина"),
            $"BUG: width stays '{FieldText("F_Ширина")}' instead of '803' after external resize");
        Assert.AreEqual("607", FieldText("F_Высота"),
            $"BUG: height stays '{FieldText("F_Высота")}' instead of '607' after external resize");
    }

    /// <summary>Своя доска со своими числами, а не общая из <c>[SetUp]</c>: панель
    /// одна на класс, и «после Open поле равно 400» прошло бы вхолостую на четырёхстах,
    /// оставшихся от предыдущего теста.</summary>
    [Test]
    public void Dimensions_InitiallyCorrectAfterOpen()
    {
        var board = CreateBoard("Свежая", new Vector3Int(419, 433, 23), new Vector3(0.9f, 0f, 0f));
        _ctx!.Open(board);

        Assert.AreEqual("419", FieldText("F_Ширина"), "initial width");
        Assert.AreEqual("433", FieldText("F_Высота"), "initial height");
        Assert.AreEqual("23", FieldText("F_Глубина"), "initial depth");
    }

    /// <summary>Сторож против слепоты общей панели, и он же — единственный тест
    /// класса, который проверяет саму ПЕРЕОТКРЫВАЕМОСТЬ. Все прочие проверки вида
    /// «после Open поле равно X» проходят вхолостую, если X случайно совпал со
    /// значением, оставшимся от предыдущего теста; здесь панель открывают на A,
    /// потом на B, и требуют B по всем четырём полям — панель, показавшая A, краснеет
    /// независимо от порядка тестов и от того, что делали соседи.</summary>
    [Test]
    public void OpeningASecondElement_ShowsTheSecondElementsValues()
    {
        var a = CreateBoard("Первая", new Vector3Int(311, 317, 19), new Vector3(1.1f, 0f, 0f));
        _ctx!.Open(a);
        Assert.AreEqual("311", FieldText("F_Ширина"), "предусловие: панель показывает A");

        var b = CreateBoard("Вторая", new Vector3Int(523, 541, 27), new Vector3(2.3f, 0f, 0f));
        _ctx!.Open(b);

        Assert.AreEqual("523", FieldText("F_Ширина"), "ширина осталась от A");
        Assert.AreEqual("541", FieldText("F_Высота"), "высота осталась от A");
        Assert.AreEqual("27", FieldText("F_Глубина"), "глубина осталась от A");
        Assert.AreEqual("Вторая", FieldText("F_Название"), "имя осталось от A");
    }

    [Test]
    public void Position_Updates_WhenElementMovedExternally()
    {
        _element!.transform.position = new Vector3(1.507f, 2.503f, 3.511f);
        CallRefreshTransformFields();

        // Позиция показывается в мм (правило 1 UI-GUIDELINES): 1,507 м = 1507 мм.
        Assert.AreEqual(1507, int.Parse(FieldText("F_X, мм")),
            $"x field: expected 1507 мм, got '{FieldText("F_X, мм")}'");
        Assert.AreEqual(2503, int.Parse(FieldText("F_Y, мм")),
            $"y field: expected 2503 мм, got '{FieldText("F_Y, мм")}'");
        Assert.AreEqual(3511, int.Parse(FieldText("F_Z, мм")),
            $"z field: expected 3511 мм, got '{FieldText("F_Z, мм")}'");
    }

    [Test]
    public void Name_DoesNotUpdate_WhenElementRenamedExternally()
    {
        _element!.PartName = "Перекличка";
        CallRefreshTransformFields();

        Assert.AreEqual("Перекличка", FieldText("F_Название"),
            $"BUG: name stays '{FieldText("F_Название")}' instead of 'Перекличка' after rename");
    }

    [Test]
    public void Radius_DoesNotUpdate_WhenRadialShelfResizedExternally()
    {
        _ctx!.Close();
        DestroyAllElements();

        var shelfGo = ElementFactory.CreateRadialShelf(600, 400, 18, 137, "Полка", Vector3.zero);
        shelfGo.transform.SetParent(_canvasGo!.transform);
        var shelf = shelfGo.GetComponent<RadialShelfElement>();
        _ctx!.Open(shelf);

        Assert.AreEqual("137", FieldText("F_Радиус угла"), "initial corner radius");

        shelf.CornerRadius = 253;
        CallRefreshTransformFields();

        Assert.AreEqual("253", FieldText("F_Радиус угла"),
            $"BUG: corner radius stays '{FieldText("F_Радиус угла")}' instead of '253'");
    }

    [Test]
    public void Gaps_DoNotUpdate_WhenFacadeGapsChangedExternally()
    {
        _ctx!.Close();
        DestroyAllElements();

        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "Створка", Vector3.zero,
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        facadeGo.transform.SetParent(_canvasGo!.transform);
        var facade = facadeGo.GetComponent<FacadeElement>();
        _ctx!.Open(facade);

        facade.GapLeft = 11;
        facade.GapRight = 19;
        facade.GapTop = 27;
        facade.GapBottom = 37;
        CallRefreshTransformFields();

        Assert.AreEqual("11", FieldText("F_gapLeft"),
            $"BUG: gapLeft stays '{FieldText("F_gapLeft")}' instead of '11'");
        Assert.AreEqual("19", FieldText("F_gapRight"),
            $"BUG: gapRight stays '{FieldText("F_gapRight")}' instead of '19'");
        Assert.AreEqual("27", FieldText("F_gapTop"),
            $"BUG: gapTop stays '{FieldText("F_gapTop")}' instead of '27'");
        Assert.AreEqual("37", FieldText("F_gapBottom"),
            $"BUG: gapBottom stays '{FieldText("F_gapBottom")}' instead of '37'");
    }

    // ── БАГ: список фасадов ящика не обновляется при открытии дропдауна ──
    // RED: до фикса фасады в дропдауне не обновлялись после Open().
    // GREEN: хук на OnEnable template перестраивает список при каждом открытии.

    [Test]
    public void DrawerFacadeDropdown_Rebuilds_WhenNewFacadeAppears()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-появление", new Vector3(0f, 0.043f, 0f));
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(1, dd.options.Count, "только '(нет фасада)' до появления фасадов");

        // Создаём фасад в контакте с ящиком (позиция из DrawerFacadeContactTests).
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "Ф-появился", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        // Вызываем RebuildDrawerFacadeOptions напрямую — именно это делает хук.
        CallRebuildDrawerFacadeOptions();

        Assert.AreEqual(2, dd.options.Count,
            "BUG: новый фасад не появился в списке после обновления дропдауна");
        Assert.AreEqual("Ф-появился", dd.options[1].text);
    }

    [Test]
    public void OrphanedFacade_StaysInList_WhenFacadeDestroyed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-осиротел", new Vector3(0f, 0.043f, 0f));
        CreateFacade("Ф-осиротел", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "Ф-осиротел";
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(2, dd.options.Count, "опции: '(нет фасада)' + 'Ф-осиротел'");
        Assert.AreEqual("Ф-осиротел", dd.options[1].text);

        // Удаляем фасад из сцены.
        var facadeEl = FindElementByName("Ф-осиротел");
        Assert.IsNotNull(facadeEl, "фасад Ф-осиротел должен существовать");
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear(); // гарантия, что в реестре чисто

        // Перестраиваем список — осиротевший фасад должен остаться.
        CallRebuildDrawerFacadeOptions();

        Assert.AreEqual(2, dd.options.Count,
            "BUG: осиротевший фасад исчез из списка после удаления");
        Assert.AreEqual("Ф-осиротел", dd.options[1].text,
            "BUG: имя осиротевшего фасада не сохранилось в списке");
    }

    [Test]
    public void OrphanedFacade_CaptionTurnsRed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-красный", new Vector3(0f, 0.043f, 0f));
        CreateFacade("Ф-красный", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "Ф-красный";
        _ctx!.Open(drawer);

        // Удаляем фасад — он становится осиротевшим.
        var facadeEl = FindElementByName("Ф-красный");
        Assert.IsNotNull(facadeEl);
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear();

        var dd = GetDrawerFacadeDropdown();
        Assert.IsNotNull(dd.captionText, "captionText should exist");
        // Заведомо НЕ красный до опыта: панель одна на класс, и красный, оставшийся от
        // предыдущего теста, выдал бы непокрашенный caption за покрашенный.
        dd.captionText.color = UIStyle.Text;

        // Перестраиваем и устанавливаем значение — caption должен стать красным.
        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("Ф-красный");

        Assert.AreEqual(Color.red, dd.captionText.color,
            "BUG: caption осиротевшего фасада не покраснел");
    }

    [Test]
    public void ValidFacadeInContact_CaptionUsesNormalColor()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-живой", new Vector3(0f, 0.043f, 0f));
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "Ф-живой", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        drawer.AttachedFacadeName = "Ф-живой";
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        // Заведомо красный до опыта — иначе «нормальный цвет» мог бы просто остаться
        // от соседнего теста на общей панели.
        dd.captionText.color = Color.red;

        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("Ф-живой");

        Assert.AreEqual(UIStyle.Text, dd.captionText.color,
            "BUG: caption валидного фасада не использует нормальный цвет UIStyle.Text");
    }

    [Test]
    public void DrawerFacadeDropdown_ClearingResetsColor()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-сброс", new Vector3(0f, 0.043f, 0f));
        CreateFacade("Ф-сброс", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "Ф-сброс";
        _ctx!.Open(drawer);

        // Удаляем фасад — осиротел.
        var facadeEl = FindElementByName("Ф-сброс");
        Assert.IsNotNull(facadeEl);
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear();

        var dd = GetDrawerFacadeDropdown();
        dd.captionText.color = UIStyle.Text;

        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("Ф-сброс");

        Assert.AreEqual(Color.red, dd.captionText.color, "caption должен быть красным");

        // Сбрасываем выбор на «(нет фасада)».
        CallOnDrawerFacadeSelected(0);

        Assert.AreEqual(UIStyle.Text, dd.captionText.color,
            "BUG: после сброса на '(нет фасада)' caption не вернулся к нормальному цвету");
    }

    [Test]
    public void FacadeMovedAway_CaptionTurnsRed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Ящик-отодвинут", new Vector3(0f, 0.043f, 0f));
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "Ф-отодвинут", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        drawer.AttachedFacadeName = "Ф-отодвинут";
        _ctx!.Open(drawer);

        // Отодвигаем фасад далеко — он больше не в контакте, но в реестре есть.
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.transform.position = new Vector3(10f, 0.043f, 0.184f);

        var dd = GetDrawerFacadeDropdown();
        dd.captionText.color = UIStyle.Text;

        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("Ф-отодвинут");

        Assert.AreEqual(Color.red, dd.captionText.color,
            "BUG: фасад отодвинут (не в контакте), но caption не красный");
    }

    // ── helpers (existing) ──────────────────────────────────────────────

    private void CallRefreshTransformFields() => _ctx!.RefreshTransformFields();

    private string FieldText(string nodeName)
    {
        var node = _canvasGo!.transform.Find("ContextMenu")!.Find(nodeName);
        Assert.IsNotNull(node, $"виджет {nodeName} должен существовать в панели");
        var inputField = node!.GetComponent<TMP_InputField>();
        Assert.IsNotNull(inputField, $"{nodeName} должен быть полем ввода");
        return inputField!.text.Replace("200b", "");
    }

    private KitchenElement CreateBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<KitchenElement>();
    }

    private void DestroyAllElements()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
    }

    // ── drawer facade helpers ───────────────────────────────────────────

    private DrawerElement CreateDrawer(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, pos);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<DrawerElement>();
    }

    private FacadeElement CreateFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), name, pos, 2, 2, 2, 2);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<FacadeElement>();
    }

    private KitchenElement? FindElementByName(string name)
    {
        foreach (var el in PartRegistry.GetAll())
            if (el.PartName == name) return el;
        return null;
    }

    private TMP_Dropdown GetDrawerFacadeDropdown()
    {
        var node = _canvasGo!.transform.Find("ContextMenu")!.Find("CtxDrawerFacade");
        Assert.IsNotNull(node, "дропдаун фасада ищется по имени CtxDrawerFacade");
        return node!.GetComponent<TMP_Dropdown>();
    }

    private void CallRebuildDrawerFacadeOptions() => _ctx!.AttachedFacade.Rebuild();

    private void CallSetDrawerFacadeValue(string name) => _ctx!.AttachedFacade.SetValue(name);

    private void CallOnDrawerFacadeSelected(int index) => _ctx!.AttachedFacade.Select(index);
}

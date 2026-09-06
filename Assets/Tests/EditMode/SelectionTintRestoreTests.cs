using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Снятие выделения возвращает КАЖДОМУ рендереру ровно тот материал,
/// который выделение с него сняло — и не трогает тот, что за это время перекрасил
/// кто-то другой.
///
/// Раньше правило было сформулировано по элементу целиком: «если у элемента есть
/// свой декор — не возвращать ничего». Куплено это было настоящим симптомом
/// (выбрал текстуру у выделенного стола, снял выделение — текстура откатилась),
/// но заплачено чужой монетой. Ящик приходит из фабрики с декором «gtv_white»,
/// то есть подпадает под «есть свой декор» с рождения, ничего за время выделения
/// не менял — и жёлтый оставался на нём навсегда. То же ждало ЛЮБОЙ элемент, на
/// котором пользователь однажды выбрал текстуру: перекрасить обратно
/// ElementHighlighter успевал только сам декоративный меш, а стекло, хром и
/// ножки оставались жёлтыми.
///
/// Поэтому вопрос задаётся не элементу, а рендереру, и не про декор, а про факт:
/// на нём всё ещё лежит наша тонировка? Лежит — снимаем; лежит чужое — не наше
/// дело. Тесты ниже держат обе стороны этого «или», потому что каждая по
/// отдельности лечится однострочником, который ломает вторую.</summary>
public class SelectionTintRestoreTests
{
    private GameObject? _selectionGo;
    private SelectionManager? _selection;
    private GameObject? _element;
    private ElementHighlighter? _highlighter;

    /// <summary>Тесты сверяют материалы ПО ССЫЛКЕ, а живой
    /// <c>ElementHighlighter</c>, утёкший из соседнего класса, перекрашивает
    /// элемент сразу после возврата — поэтому на время теста его нет.</summary>
    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _highlighter = ElementHighlighter.Instance;
        ElementHighlighter.Instance = null;
        _selectionGo = new GameObject("SelectionManager сторожа");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = true;
        if (_selection != null) _selection.DeselectAll();
        SelectionManager.Instance = null;
        if (_selectionGo != null) Object.DestroyImmediate(_selectionGo);
        if (_element != null) Object.DestroyImmediate(_element);
        _selectionGo = null;
        _selection = null;
        _element = null;
        ElementHighlighter.Instance = _highlighter;
        _highlighter = null;
        MaterialManager.ClearCache();
        LogAssert.ignoreFailingMessages = false;
    }

    private (KitchenElement element, MeshRenderer root, MeshRenderer child) MakeComposite()
    {
        var go = new GameObject("Составной элемент");
        _element = go;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = go.name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);

        var root = go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshFilter>();

        var childGo = new GameObject("Стекло");
        childGo.transform.SetParent(go.transform, worldPositionStays: false);
        var child = childGo.AddComponent<MeshRenderer>();
        childGo.AddComponent<MeshFilter>();

        var start = MaterialManager.GetSharedMaterial(MaterialCatalog.Default);
        Assert.IsNotNull(start, "без материала на рендерере запоминать нечего");
        root.sharedMaterial = start;
        child.sharedMaterial = start;

        Assume.That(ElementRenderers.BodyOf(element).Count, Is.EqualTo(2),
            "тело элемента — оба меша; на одном сторож ничего не проверит");
        return (element, root, child);
    }

    /// <summary>Декор выбран ДО выделения и за время выделения не менялся:
    /// возвращать обязаны, сколько бы «своего декора» на элементе ни было.</summary>
    [Test]
    public void Deselect_GivesBackTheMaterial_EvenWhenTheElementWearsItsOwnDecor()
    {
        var (element, root, child) = MakeComposite();

        MaterialManager.Apply(element, MaterialCatalog.Get("oak"));
        Assume.That(MaterialManager.HasCustomDecor(element), Is.True,
            "элемент обязан носить НЕ дефолтный декор, иначе тест зелен и на старом коде");

        var rootBefore = root.sharedMaterial;
        var childBefore = child.sharedMaterial;

        _selection!.Select(element);
        Assert.AreNotSame(rootBefore, root.sharedMaterial, "выделение обязано покрасить корень");
        Assert.AreNotSame(childBefore, child.sharedMaterial, "и дочерний меш тоже");

        _selection.DeselectAll();

        Assert.AreSame(rootBefore, root.sharedMaterial,
            "жёлтый остался на корне: правило «у элемента есть декор — ничего не возвращаем» "
            + "держит тонировку на элементе, который свой декор и не менял");
        Assert.AreSame(childBefore, child.sharedMaterial,
            "и на дочернем меше — до него перекраска декором вообще не доходит");
    }

    /// <summary>Обратный вход к предыдущему тесту: декор выбран ВО ВРЕМЯ
    /// выделения. Возврат «как было» стёр бы только что выбранную текстуру —
    /// ровно тот симптом, ради которого появилось старое правило.</summary>
    [Test]
    public void Deselect_KeepsTheDecorChosenWhileSelected_AndStillClearsTheRest()
    {
        var (element, root, child) = MakeComposite();

        var childBefore = child.sharedMaterial;

        _selection!.Select(element);

        var oak = MaterialManager.GetSharedMaterial(MaterialCatalog.Get("oak"));
        root.sharedMaterial = oak;

        _selection.DeselectAll();

        Assert.AreSame(oak, root.sharedMaterial,
            "снятие выделения вернуло материал, запомненный ДО выбора декора: "
            + "симптом «выделил, выбрал текстуру, снял выделение — текстура пропала»");
        Assert.AreSame(childBefore, child.sharedMaterial,
            "меш, которого перекраска не касалась, обязан отдать жёлтый: старое правило "
            + "судило по элементу целиком и оставляло тонировку на всём, кроме декора");
    }

    /// <summary>Чтение <c>renderer.material</c> — не наблюдение, а мутация: Unity
    /// подменяет материал рендерера собственной КОПИЕЙ и возвращает её. Так делает
    /// <c>ElementMover.SaveDragMaterial</c> в начале каждого перетаскивания, и так
    /// делает любой тест, который смотрит цвет через <c>.material</c>.
    ///
    /// Сторож «на рендерере всё ещё наша тонировка?» сравнивал ССЫЛКУ и читал копию
    /// собственной краски как «перекрасил кто-то чужой»: подтащил выделенную деталь,
    /// снял выделение — жёлтый навсегда. Копия нашей тонировки — это наша тонировка,
    /// поэтому вопрос задаётся материалу (его имени), а не адресу объекта.
    ///
    /// Копирование здесь провоцируется НАРОЧНО, и в EditMode Unity пишет об этом
    /// ошибку про утечку материала в сцену — фреймворк валит тест на любом
    /// необработанном сообщении. Глушить весь тест флагом
    /// <c>ignoreFailingMessages</c> значило бы заодно проглотить настоящую ошибку,
    /// если она случится; поэтому ожидается ровно одно сообщение и ровно то.
    /// Смысл теста держат два <c>Assume</c> ниже: они, а не молчание лога,
    /// доказывают, что копия сделана и что тонировка подписана.</summary>
    [Test]
    public void Deselect_GivesBackTheMaterial_AfterSomeoneInstantiatedTheTint()
    {
        var (element, root, child) = MakeComposite();

        var rootBefore = root.sharedMaterial;
        var childBefore = child.sharedMaterial;

        _selection!.Select(element);
        var tinted = root.sharedMaterial;

        LogAssert.Expect(LogType.Error,
            new Regex("Instantiating material due to calling renderer\\.material"));
        var instantiated = root.material;

        Assume.That(instantiated, Is.Not.SameAs(tinted),
            "Unity не сделала копию — тогда тест зелен и на старом коде, судить нечего");
        Assume.That(tinted.name, Is.EqualTo(SelectionManager.TintMaterialName),
            "тонировка обязана быть подписана, иначе копию не отличить от чужого материала");

        _selection.DeselectAll();

        Assert.AreSame(rootBefore, root.sharedMaterial,
            "жёлтый остался: сторож сравнивал ссылку и принял копию нашей же тонировки "
            + "за чужую краску");
        Assert.AreSame(childBefore, child.sharedMaterial,
            "на дочернем меше копию никто не делал — он обязан вернуться в любом случае");
    }

    /// <summary>Повторная тонировка берётся от СОХРАНЁННОГО материала, а не от
    /// того, что сейчас на рендерере. Иначе жёлтый становится «своим» цветом
    /// элемента и возвращать после снятия будет уже нечего.</summary>
    [Test]
    public void RetintingASelectedElement_DoesNotTintOnTopOfItsOwnTint()
    {
        var (element, root, child) = MakeComposite();

        var rootBefore = root.sharedMaterial;
        var childBefore = child.sharedMaterial;

        _selection!.Select(element);
        _selection.RefreshHighlight(element);
        _selection.RefreshHighlight(element);
        _selection.DeselectAll();

        Assert.AreSame(rootBefore, root.sharedMaterial,
            "перезапомнив жёлтый как собственный материал корня, обновление подсветки "
            + "делает тонировку вечной");
        Assert.AreSame(childBefore, child.sharedMaterial,
            "то же самое на дочернем меше");
    }
}

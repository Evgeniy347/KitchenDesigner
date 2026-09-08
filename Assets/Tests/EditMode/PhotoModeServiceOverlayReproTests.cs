using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class PhotoModeServiceOverlayReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _tintBefore;
    private ElementHighlighter? _highlighterBefore;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _tintBefore = ElementHighlighter.TintEnabled;
        // SelectionTintRestoreTests уже документирует этот риск: живой
        // ElementHighlighter.Instance, оставшийся от СОСЕДНЕГО класса в общем
        // прогоне, репэйнтит элемент сразу после того, как DeselectAll вернул
        // его собственный материал (RestoreMaterial безусловно зовёт
        // ApplyForElement на актуальном Instance). Этот файл не создаёт
        // Highlighter в тестах на Select/DeselectAll и поэтому ничем не защищён
        // от такого чужого Instance — нейтрализуем его на время теста тем же
        // приёмом, которым уже пользуется SelectionTintRestoreTests.
        _highlighterBefore = ElementHighlighter.Instance;
        ElementHighlighter.Instance = null;
        PartRegistry.Clear();
        EditModeManager.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        ElementHighlighter.TintEnabled = _tintBefore;
        ElementHighlighter.Instance = _highlighterBefore;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
        LogAssert.ignoreFailingMessages = false;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
        PartRegistry.Register(f);
        Spawn(go);
        return f;
    }

    private KitchenElement MakePart(Vector3Int dims, string name)
    {
        var go = Spawn(ElementFactory.CreatePart(dims, name, Vector3.zero));
        var e = go.GetComponent<KitchenElement>()!;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    // ── D11: тонировка нарушения (ConstraintValidator) не должна попадать в кадр ──

    [Test]
    public void ApplyMaterial_InvalidFacade_NormalMode_PaintsTheInvalidTint()
    {
        var host = Spawn(new GameObject("Highlighter"));
        var highlighter = host.AddComponent<ElementHighlighter>();
        highlighter.CreateMaterials();

        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f));
        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(a), Is.True,
            "тест построен на перекрытии эффективных границ фасадов — без нарушения "
            + "непонятно, что именно должен гасить фоторежим");
        var ownDecor = a.GetComponent<MeshRenderer>().sharedMaterial;

        ElementHighlighter.TintEnabled = true;
        highlighter.ApplyForElement(a);

        Assert.AreNotEqual(ownDecor, a.GetComponent<MeshRenderer>().sharedMaterials[0],
            "вне фоторежима нарушение обязано красить деталь красным — иначе противоположный "
            + "вход не доказывает, что фоторежим что-то гасит");
    }

    [Test]
    public void ApplyMaterial_InvalidFacade_PhotoModeActive_ShowsOwnDecor_NotTheValidityTint()
    {
        var host = Spawn(new GameObject("Highlighter"));
        var highlighter = host.AddComponent<ElementHighlighter>();
        highlighter.CreateMaterials();

        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f));
        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(a), Is.True,
            "нарушение обязано существовать — иначе непонятно, что именно проверяется");

        // Деталь без собственного декора ничего не доказывает: ApplyOwnDecor всё равно
        // резолвит дефолтный MaterialDef через MaterialManager и получает СВЕЖИЙ материал
        // из кэша — он не обязан оказаться тем же объектом, что примитив нёс до захвата.
        // Декор нужен настоящий, чтобы "свой материал" был не абстракцией, а конкретным
        // экземпляром, который можно узнать по ссылке до и после прохода фоторежима.
        MaterialManager.ApplyById(a, "oak");
        Assume.That(MaterialManager.HasCustomDecor(a), Is.True,
            "предпосылка: у детали должен быть НАЗНАЧЕННЫЙ декор — иначе ветка ApplyOwnDecor "
            + "работает с дефолтом, и сравнение по ссылке ничего не проверяет");
        var ownDecor = a.GetComponent<MeshRenderer>().sharedMaterial;

        EditModeManager.SetMode(EditMode.Photo);
        highlighter.ApplyForElement(a);

        Assert.AreEqual(ownDecor, a.GetComponent<MeshRenderer>().sharedMaterials[0],
            "D11: тонкая красная метка валидности не должна попадать в фотографию для "
            + "заказчика — TintEnabled=false в фоторежиме обязан гасить И невалидную ветку, "
            + "а не только зелёную подсветку валидной детали");
    }

    // ── D11: золотая подсветка выделения не должна попадать в кадр ──

    [Test]
    public void Select_NormalMode_PaintsTheSelectionHighlight()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();

        sm.Select(e);

        Assert.AreNotEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterial,
            "вне фоторежима выделение обязано красить деталь золотистым тоном — "
            + "противоположный вход, без которого следующий тест ничего не доказывает");
    }

    [Test]
    public void Select_WhilePhotoModeActive_DoesNotPaintTheSelectionHighlight()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();

        EditModeManager.SetMode(EditMode.Photo);
        sm.Select(e);

        Assert.AreEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterial,
            "D11: выбор детали ВО ВРЕМЯ фоторежима не должен зажигать золотистую "
            + "подсветку — до этого коммита DeselectAll на входе спасал только случай "
            + "«выделили ДО фоторежима», а клик уже внутри режима ничем не был перекрыт");
    }

    [Test]
    public void Select_BeforeEnteringPhotoMode_HighlightIsRemovedOnEntry_AndSelectionIsCleared()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var renderer = e.GetComponent<MeshRenderer>();
        var ownDecor = renderer.sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();
        SelectionManager.Instance = sm;
        try
        {
            // ── СЕНСОР: кто, в каком порядке и на какой объект ставит/снимает
            // материал по пути «объект выделен → вход в фоторежим». Каждая строка —
            // факт, а не гипотеза: имя материала на РЕНДЕРЕРЕ детали и то, жив ли
            // в этот момент ElementHighlighter.Instance (утёкший экземпляр —
            // задокументированный риск, см. SelectionTintRestoreTests), а также
            // на КАКОЙ SelectionManager указывает статический Instance — прямой
            // вызов на sm и вызов через Instance (как это делает EditModeManager)
            // не одно и то же, если Instance null или указывает на чужой объект.
            void Log(string step) => TestContext.WriteLine(
                $"[sensor] {step}: material='{renderer.sharedMaterial?.name}' "
                + $"refEqualsOwnDecor={ReferenceEquals(renderer.sharedMaterial, ownDecor)} "
                + $"selected={(sm.Selected != null ? sm.Selected.PartName : "null")} "
                + $"ElementHighlighter.Instance={(ElementHighlighter.Instance != null ? "alive" : "null")} "
                + $"SelectionManager.Instance={(SelectionManager.Instance == null ? "null" : ReferenceEquals(SelectionManager.Instance, sm) ? "sm" : "other")}");

            Log("00 до выделения");

            sm.Select(e);
            Assume.That(renderer.sharedMaterial, Is.Not.EqualTo(ownDecor),
                "предпосылка: выделение реально перекрашивает деталь");
            Log("01 после Select, до входа в фоторежим");

            // Проверка «очевидного, чего никто не проверил»: DeselectAll обязан
            // снять именно МАТЕРИАЛ с рендерера, а не только ссылку sm.Selected.
            // Зовём его тут напрямую (а не через SetMode), чтобы отделить работу
            // DeselectAll от всего остального, что делает вход в фоторежим.
            sm.DeselectAll();
            Log("02 сразу после прямого DeselectAll (ДО фоторежима)");
            Assert.AreSame(ownDecor, renderer.sharedMaterial,
                "DeselectAll сам по себе обязан вернуть материал на рендерер — если это уже "
                + "не так ДО входа в фоторежим, дефект живёт в SelectionManager, а не в PhotoMode");
            Assert.IsNull(sm.Selected, "и ссылку на выделенный объект тоже обязан снять");

            // Возвращаем репродукцию к заявленному сценарию: объект выделен СНОВА,
            // теперь входим в фоторежим уже выделенным.
            sm.Select(e);
            Assume.That(renderer.sharedMaterial, Is.Not.EqualTo(ownDecor),
                "предпосылка: повторное выделение снова красит деталь");
            Log("03 повторно выделили — сценарий репро восстановлен");

            void OnSelectionChanged(KitchenElement? _) => Log("04 SelectionManager.OnSelectionChanged (внутри DeselectAll, вызванного SetMode)");
            void OnPhotoChanged() => Log("05 PhotoMode.Changed (после Enter() и второго DeselectAll в SetMode)");
            sm.OnSelectionChanged += OnSelectionChanged;
            PhotoMode.Changed += OnPhotoChanged;
            try
            {
                EditModeManager.SetMode(EditMode.Photo);
            }
            finally
            {
                sm.OnSelectionChanged -= OnSelectionChanged;
                PhotoMode.Changed -= OnPhotoChanged;
            }

            Log("06 в конце, после SetMode(Photo)");

            Assert.AreEqual(ownDecor, renderer.sharedMaterial,
                "вход в фоторежим с уже выделенным объектом обязан снять подсветку до кадра");
            Assert.IsNull(sm.Selected,
                "выделение снимается целиком — это не «то же самое, но другим цветом»");
        }
        finally
        {
            SelectionManager.Instance = null;
        }
    }
}

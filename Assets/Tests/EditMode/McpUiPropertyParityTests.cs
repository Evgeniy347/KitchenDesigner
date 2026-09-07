using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using KitchenDesigner.Core.UI;

/// <summary>Расхождение между тем, что человек правит в панели свойств, и тем,
/// что агент правит через edit_elements. Это ДВЕ независимо написанные вручную
/// поверхности над одними и теми же свойствами класса элемента: строка панели
/// пишется в *FieldsEditor, проволочное поле — в ElementEditAppliers, и ни одна
/// из них не выводится из другой. Свойство, добавленное в одну, молча
/// отсутствует в другой; так у светильника набралось семнадцать параметров, ни
/// одного из которых агент не видит.
///
/// Сверять СПИСКИ здесь нельзя: списка нет ни там, ни там — обе поверхности
/// написаны императивным кодом, а половина правок идёт не присваиванием, а
/// через команду (SetMaterialCommand, SetEdgeBandingCommand,
/// SetCooktopCutoutCommand). Разбор исходников поэтому соврал бы в обе стороны.
///
/// Поэтому обе поверхности снимаются ОПЫТОМ, а не чтением: элемент создаётся,
/// снимается слепок всех его свойств, дёргается ОДИН виджет панели (или
/// посылается ОДНО проволочное поле), снимается второй слепок — и разница
/// слепков говорит, какие свойства эта ручка на самом деле меняет. Ни одного
/// имени свойства в коде теста нет, кроме контрольных образцов и списка
/// исключений: новый тип элемента и новое свойство попадают под проверку сами.</summary>
public class McpUiPropertyParityTests : McpTestFixture
{
    /// <summary>Имя соседа в сцене. Поля, которые ссылаются на другой элемент
    /// (прикрепить к, фасад ящика, пара ящиков), без него не с чем сравнивать:
    /// выпадающий список панели пуст, а проволочное поле получает отказ — обе
    /// поверхности выглядели бы одинаково немыми и сошлись бы на пустом.</summary>
    private const string NEIGHBOUR = "Сосед";

    private const string PROBE = "Проба";

    /// <summary>Виджеты, которые НЕ правят свойство, а заменяют, удаляют или
    /// размножают сам элемент: после нажатия слепок снимать уже не с чего.
    /// Списком имён, а не типом виджета, потому что рядом стоят кнопки, которые
    /// свойства править как раз должны — поворот на 90° и полоса кромок.</summary>
    private static readonly (string node, string why)[] WidgetsNotDriven =
    {
        ("CtxType",
         "выпадающий «Тип» не правит свойство, а конвертирует элемент в другой класс: "
         + "старый GameObject уничтожается, и второй слепок снимался бы с трупа"),
        ("CtxDel",
         "«Удалить» уничтожает элемент — снимать второй слепок не с чего"),
        ("CtxDup",
         "«Дублировать» заводит копию с другим именем; дальше опыт правил бы то один "
         + "элемент, то другой"),
    };

    /// <summary>Секции панели, свёрнутые при открытии. Их содержимое неактивно,
    /// а неактивный виджет опыт не видит — зазоры, пазы, накладки и тонкая
    /// настройка лампы выглядели бы «панель этого не правит». Разворачиваются
    /// перед тем, как перечислять виджеты.</summary>
    private static readonly string[] Expanders =
    {
        "CtxGaps", "CtxGrooves", "CtxTextures", "CtxLightAdv",
    };

    /// <summary>Свойства, которые ПРАВИТ панель, но не правит агент. Каждая
    /// запись — либо решение владельца проекта, либо долг с указанием, чем он
    /// закрывается. Список проверяется на гниль тестом
    /// <see cref="EveryExemption_StillNamesALiveDivergence"/>.</summary>
    private static readonly (string owner, string property, string why)[] UiOnly =
    {
        ("LightSourceElement", "*",
         "светильник целиком вне MCP — он и создаётся только из сайдбара, это решение "
         + "владельца проекта, записанное в McpSpawnerTypeCoverageTests.NotOfferedToAgents. "
         + "Пока оно в силе, семнадцать его параметров агенту не нужны; изменится решение — "
         + "исчезнет и эта запись"),
    };

    /// <summary>Обратная сторона: свойства, которые правит агент, а панель не
    /// правит.</summary>
    private static readonly (string owner, string property, string why)[] McpOnly =
    {
        ("DrawerElement", "IsUpperDrawer",
         "кнопка «Двойной ящик» в панели заводит вторую коробку и делает ВЕРХНЕЙ именно её, а "
         + "не ту, из которой нажали: у исходного ящика этот флаг так и остаётся снятым. "
         + "Агент же назначает роль прямо, полем is_upper — это разные операции, а не "
         + "забытая строка панели"),
        ("DrawerElement", "System",
         "систему ящика (GTV / Movento) панель переключает тем же выпадающим «Тип», которым "
         + "конвертирует классы элементов: элемент при этом пересоздаётся, поэтому опыт этот "
         + "виджет не трогает (см. WidgetsNotDriven). Расхождения тут нет — есть предел опыта, "
         + "и он назван вслух, чтобы никто не принял эту строку за настоящее решение"),
    };

    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private bool _blockOnViolation;

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("ParityCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu.Build(_canvas.transform);
        _blockOnViolation = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
    }

    [TearDown]
    public void Teardown()
    {
        KitchenSettings.Instance.BlockOnViolation = _blockOnViolation;
        CommandStack.Clear();
        if (_menu != null) UnityEngine.Object.DestroyImmediate(_menu.gameObject);
        if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas.gameObject);
        MaterialCatalog.Reset();
        ElementFactory.ClearPools();
    }

    /// <summary>Ручной сброс сцены МЕЖДУ образцами внутри одного теста — не
    /// только между тестами: [TearDown] тут не поможет, вызывается явно перед
    /// следующим слепком. Тело совпадает с McpTestFixture.McpFixtureTearDown,
    /// но переиспользовать его нельзя: тот вызывается NUnit-ом только один раз
    /// в конце теста, а этому нужно отрабатывать посреди метода произвольное
    /// число раз.</summary>
    private void DestroySpawned()
    {
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    // ---------- образцы ----------

    /// <summary>По одному живому экземпляру каждого класса элемента. Список
    /// строится не руками: он берётся из фабрики через
    /// <see cref="Specimen"/>, а полнота проверяется
    /// <see cref="EveryElementClassInTheProject_HasASpecimen"/> — забытый класс
    /// краснеет, а не выпадает из сверки молча.</summary>
    private static IEnumerable<(string label, Func<GameObject> make)> Specimens()
    {
        yield return ("KitchenElement", () => ElementFactory.CreatePart(
            new Vector3Int(600, 400, 18), PROBE, Vector3.zero));
        yield return ("Wall", () => ElementFactory.CreateWall(
            new Vector3Int(2000, 2500, 100), PROBE, Vector3.zero));
        yield return ("FacadeElement", () => ElementFactory.CreateFacade(
            new Vector3Int(600, 716, 18), PROBE, Vector3.zero));
        yield return ("AssembledFacadeElement", () => ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 716, 18), PROBE, Vector3.zero, AssembledFill.Blind));
        yield return ("PanelElement", () => ElementFactory.CreatePanel(
            new Vector3Int(600, 400, 3), PROBE, Vector3.zero));
        yield return ("RadialShelfElement", () => ElementFactory.CreateRadialShelf(
            600, 400, 18, 200, PROBE, Vector3.zero));
        yield return ("DrawerElement", () => ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, PROBE, Vector3.zero, DrawerSystem.Gtv));
        yield return ("TableElement", () => ElementFactory.CreateTable(
            new Vector3Int(1200, 750, 700), PROBE, Vector3.zero));
        yield return ("RadiusTableElement", () => ElementFactory.CreateRadiusTable(
            new Vector3Int(1200, 750, 700), PROBE, Vector3.zero));
        yield return ("StoolElement", () => ElementFactory.CreateStool(
            new Vector3Int(StoolElement.DefaultWidthMM, StoolElement.DefaultHeightMM,
                StoolElement.DefaultDepthMM), 0, PROBE, Vector3.zero));
        yield return ("ChairElement", () => ElementFactory.CreateChair(
            new Vector3Int(ChairElement.DefaultWidthMM, ChairElement.DefaultHeightMM,
                ChairElement.DefaultDepthMM), 0, AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT,
            PROBE, Vector3.zero));
        yield return ("SofaElement", () => ElementFactory.CreateSofa(
            new Vector3Int(SofaElement.DefaultWidthMM, SofaElement.DefaultHeightMM,
                SofaElement.DefaultDepthMM), SofaElement.DefaultCornerRadiusMM,
            SofaElement.DefaultSeatHeightMM, PROBE, Vector3.zero));
        yield return ("PouffeElement", () => ElementFactory.CreatePouffe(
            new Vector3Int(PouffeElement.DefaultWidthMM, PouffeElement.DefaultHeightMM,
                PouffeElement.DefaultDepthMM), PouffeElement.DefaultCornerRadiusMM,
            PouffeElement.DefaultSeatThicknessMM, PROBE, Vector3.zero));
        yield return ("ToiletElement", () => ElementFactory.CreateToilet(
            ToiletElement.DefaultSeatHeightMM, PROBE, Vector3.zero));
        yield return ("WallHungToiletElement", () => ElementFactory.CreateWallHungToilet(
            WallHungToiletElement.DefaultSeatHeightMM,
            WallHungToiletElement.DefaultFlushPlateHeightMM, PROBE, Vector3.zero));
        yield return ("BathtubElement", () => ElementFactory.CreateBathtub(
            BathtubLayout.DefaultDimensionsMM, BathtubElement.DefaultRimWidthMM,
            BathtubElement.DefaultBowlDepthMM, BathtubElement.DefaultBowlRadiusMM,
            BathtubElement.DefaultBowlFilletMM, PROBE, Vector3.zero));
        yield return ("BathMixerElement", () => ElementFactory.CreateBathMixer(
            BathMixerSpec.Default, PROBE, Vector3.zero));
        yield return ("ShowerColumnElement", () => ElementFactory.CreateShowerColumn(
            ShowerColumnSpec.Default, PROBE, Vector3.zero));
        yield return ("SocketElement", () => ElementFactory.CreateSocket(
            WallDeviceSpec.Default, PROBE, Vector3.zero));
        yield return ("LightSwitchElement", () => ElementFactory.CreateLightSwitch(
            WallDeviceSpec.Default, true, null, PROBE, Vector3.zero));
        yield return ("BedElement", () => ElementFactory.CreateBed(
            new Vector3Int(BedElement.DefaultWidthMM, BedElement.DefaultHeightMM,
                BedElement.DefaultDepthMM), true, true, PROBE, Vector3.zero));
        yield return ("PillarElement", () => ElementFactory.CreatePillar(
            PillarElement.MidHeightMM_Default, PROBE, Vector3.zero));
        yield return ("ScrewLegElement", () => ElementFactory.CreateScrewLeg(PROBE, Vector3.zero));
        yield return ("SinkElement", () => ElementFactory.CreateSink(PROBE, Vector3.zero));
        yield return ("CooktopElement", () => ElementFactory.CreateCooktop(PROBE, Vector3.zero));
        yield return ("OvenElement", () => ElementFactory.CreateOven(PROBE, Vector3.zero));
        yield return ("DishwasherElement", () => ElementFactory.CreateDishwasher(PROBE, Vector3.zero));
        yield return ("WindowElement", () => ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), PROBE, Vector3.zero));
        yield return ("DoorElement", () => ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), PROBE, Vector3.zero));
        yield return ("FloorElement", () => ElementFactory.CreateFloor(
            new Vector3Int(FloorElement.DEFAULT_SIZE_MM, FloorElement.DEFAULT_THICKNESS_MM,
                FloorElement.DEFAULT_SIZE_MM), PROBE, Vector3.zero));
        yield return ("LightSourceElement", () => ElementFactory.CreateLightSource(PROBE, Vector3.zero));
    }

    private KitchenElement Spawn(Func<GameObject> make)
    {
        var go = make();
        _spawned.Add(go);
        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element, "образец обязан быть элементом, а не голым объектом сцены");
        if (!PartRegistry.GetAll().Contains(element!)) PartRegistry.Register(element!);
        return element!;
    }

    /// <summary>Сосед-фасад ставится ВПЛОТНУЮ к передней грани образца, а не
    /// куда-нибудь в сторону: список «Фасад ящика» в панели показывает только
    /// фасады, которые физически прилегают к коробке
    /// (DrawerLinks.IsFacadeInContact). Отодвинутый сосед делал этот список
    /// пустым, и панель выглядела так, будто фасад к ящику не привязывают.
    ///
    /// «Вплотную» считается не формулой, а примеркой: у ДВП зазоры по 3 мм при
    /// толщине 3 мм, и любая формула для такой детали промахивается в ту или
    /// другую сторону. Поэтому сосед двигается по нескольким положениям, пока
    /// сама AttachLinks.InContact не скажет «касаются» — фикстуру ставит та же
    /// геометрия, которой потом пользуется панель.</summary>
    private void PlaceNeighbourInFrontOf(KitchenElement host)
    {
        const int THICKNESS_MM = 18;
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 716, THICKNESS_MM),
            NEIGHBOUR, Vector3.zero);
        _spawned.Add(go);
        var neighbour = go.GetComponent<KitchenElement>();
        if (!PartRegistry.GetAll().Contains(neighbour)) PartRegistry.Register(neighbour);
        _neighbourName = neighbour.PartName;

        var flush = host.DimensionsMM.z * 0.5f + THICKNESS_MM * 0.5f;
        foreach (var mm in new[]
                 {
                     flush,
                     flush - host.GapFront,
                     flush + host.GapFront,
                     flush + host.GapFront + neighbour.GapBack,
                 })
        {
            neighbour.transform.position = new Vector3(0f, 0f, -mm * AppConstants.MM_TO_UNITS);
            if (AttachLinks.InContact(host, neighbour)) return;
        }
    }

    /// <summary>Имя, под которым сосед на самом деле лежит в реестре. Фабрика
    /// ТРАНСЛИТЕРИРУЕТ имя («Сосед» становится «Sosed»), и запрос по исходному
    /// имени получал «element not found» — молчаливый отказ, неотличимый от
    /// «поверхность этого не умеет».</summary>
    private string _neighbourName = "";

    // ---------- слепок свойств ----------

    private static readonly Dictionary<Type, PropertyInfo[]> ReadableCache =
        new Dictionary<Type, PropertyInfo[]>();

    /// <summary>В слепок идут только свойства С ПУБЛИЧНЫМ СЕТТЕРОМ, потому что
    /// сверяются ПРАВКИ. Производные (IsOpen, BoxWidth, TotalHeightMM,
    /// DecorSurfaceMM и ещё десятки) меняются заодно с тем, что правят, и в
    /// слепке дали бы разницу там, где обе поверхности делают одно и то же
    /// разными путями: в панели дверь открывается кнопкой, у агента — полем
    /// is_open, а IsOpen у обоих только читается.</summary>
    private static PropertyInfo[] Readable(Type type)
    {
        if (ReadableCache.TryGetValue(type, out var cached)) return cached;
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite
                        && p.GetGetMethod() != null && p.GetSetMethod() != null
                        && p.GetIndexParameters().Length == 0
                        && p.DeclaringType != null
                        && typeof(KitchenElement).IsAssignableFrom(p.DeclaringType))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();
        ReadableCache[type] = props;
        return props;
    }

    private static string Format(object? value)
    {
        if (value == null) return "∅";
        if (value is string s) return s;
        if (value is UnityEngine.Object obj) return obj == null ? "∅" : obj.name;
        if (value is IEnumerable list)
        {
            var parts = new List<string>();
            foreach (var item in list) parts.Add(Format(item));
            return "[" + string.Join(",", parts) + "]";
        }
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static Dictionary<string, string> Snapshot(KitchenElement element)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in Readable(element.GetType()))
        {
            try { values[p.Name] = Format(p.GetValue(element)); }
            catch (Exception) { }
        }
        values["transform.position"] = element.transform.position.ToString("F4");
        values["transform.rotation"] = element.transform.eulerAngles.ToString("F2");
        return values;
    }

    private static IEnumerable<string> Changed(
        Dictionary<string, string> before, Dictionary<string, string> after)
    {
        foreach (var pair in after)
            if (!before.TryGetValue(pair.Key, out var was) || !string.Equals(was, pair.Value, StringComparison.Ordinal))
                yield return pair.Key;
    }

    // ---------- поверхность панели ----------

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private static bool Excluded(string node) =>
        WidgetsNotDriven.Any(w => w.node == node);

    private static bool Drivable(Transform t) =>
        t.GetComponent<TMP_InputField>() != null
        || t.GetComponent<TMP_Dropdown>() != null
        || t.GetComponent<Toggle>() != null
        || t.GetComponent<Button>() != null;

    private void Expand()
    {
        foreach (var node in Expanders)
        {
            var widget = WidgetNamed(node);
            var button = widget == null ? null : widget.GetComponent<Button>();
            if (button != null && widget!.gameObject.activeInHierarchy) button.onClick.Invoke();
        }
    }

    private List<string> VisibleWidgets()
    {
        var names = new List<string>();
        foreach (var t in Panel().GetComponentsInChildren<Transform>(false))
            if (!Excluded(t.name) && Drivable(t) && !names.Contains(t.name))
                names.Add(t.name);
        return names;
    }

    private Transform? WidgetNamed(string node) =>
        Panel().GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == node);

    /// <summary>Значения, которыми пробуется текстовое поле. Одного мало:
    /// поле поворота показывает «0,0», прибавка 1,5 даёт «1,5» — и панель
    /// принимает её, а слепок не видит разницы, потому что угол округляется до
    /// десятых и остаётся собой. Числа тут разного порядка нарочно: миллиметры,
    /// градусы и доли ведут себя по-разному, и что-то одно обязано пройти.</summary>
    private static IEnumerable<string> TextAttempts(string current)
    {
        var clean = current.Replace("​", "");
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        if (int.TryParse(clean, out var i))
        {
            yield return (i + 7).ToString();
            yield return (i + 137).ToString();
        }
        else if (float.TryParse(clean, System.Globalization.NumberStyles.Float, culture, out var f))
        {
            yield return (f + 45f).ToString("F1", culture);
            yield return (f + 90f).ToString("F1", culture);
        }

        yield return "90";
        yield return "137";
        yield return clean + "2";
    }

    /// <summary>Сколько раз подряд опыт жмёт одну и ту же кнопку. Кнопка бывает
    /// ЦИКЛИЧЕСКОЙ: полоса-торец в секции кромок перещёлкивает сторону по кругу
    /// «авто → есть → убрать», и первый клик правит одну маску, а второй —
    /// другую. Остановка на первой удавшейся попытке (она права для текстового
    /// поля, где кандидаты — разные способы сделать ОДНО и то же) объявила бы
    /// половину такой ручки несуществующей: guard увидел кликом только
    /// EdgeSuppressedMask и записал EdgeForcedMask в «умеет только MCP», хотя
    /// человек правит его тем же самым виджетом, вторым нажатием.
    ///
    /// Три — длина полного круга: третий клик возвращает сторону в исходное
    /// состояние, и продолжать нечего.</summary>
    private const int ButtonPresses = 3;

    /// <summary>Один виджет — несколько попыток. Возвращает действия, а не
    /// делает их: снаружи попытки идут до первой, которая что-то изменила —
    /// кроме помеченных <c>cycling</c>, где следующее нажатие не «другой способ
    /// сделать то же», а следующий шаг круга.</summary>
    private static IEnumerable<(Action act, bool cycling)> Attempts(Transform widget)
    {
        var input = widget.GetComponent<TMP_InputField>();
        if (input != null)
        {
            if (!input.interactable) yield break;
            foreach (var text in TextAttempts(input.text))
            {
                var value = text;
                yield return (() => { input.text = value; input.onEndEdit.Invoke(value); }, false);
            }
            yield break;
        }

        var dropdown = widget.GetComponent<TMP_Dropdown>();
        if (dropdown != null)
        {
            if (!dropdown.interactable) yield break;
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var index = i;
                yield return (() => { if (dropdown.value != index) dropdown.value = index; }, false);
            }
            yield break;
        }

        var toggle = widget.GetComponent<Toggle>();
        if (toggle != null)
        {
            if (toggle.interactable) yield return (() => toggle.isOn = !toggle.isOn, false);
            yield break;
        }

        var button = widget.GetComponent<Button>();
        if (button == null || !button.interactable) yield break;
        for (int i = 0; i < ButtonPresses; i++)
            yield return (() => button.onClick.Invoke(), true);
    }

    /// <summary>Панель применяет правку текстовых полей не чаще одного раза за
    /// кадр (ContextMenuFieldTracker.ApplyOncePerFrame), а в EditMode кадр не
    /// сменяется НИКОГДА: первое поле опыта проходит, второе и все следующие
    /// молча проглатываются, и панель выглядит немой. Именно так первый прогон
    /// и «доказал», что из панели правится одно только имя.</summary>
    private void ForgetPreviousApply() =>
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();

    /// <summary>Что панель на самом деле правит у этого класса: по одному
    /// виджету за раз, на свежем элементе, разницей слепков.</summary>
    private HashSet<string> UiSurface(Func<GameObject> make)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);

        var probe = Spawn(make);
        PlaceNeighbourInFrontOf(probe);
        _menu!.Open(probe);
        Expand();
        var widgets = VisibleWidgets();
        DestroySpawned();

        foreach (var node in widgets)
        {
            var element = Spawn(make);
            PlaceNeighbourInFrontOf(element);
            _menu!.Open(element);
            Expand();
            var widget = WidgetNamed(node);
            if (widget == null || !widget.gameObject.activeInHierarchy) { DestroySpawned(); continue; }

            var before = Snapshot(element);
            foreach (var (attempt, cycling) in Attempts(widget))
            {
                ForgetPreviousApply();
                attempt();
                if (element == null) break;
                var changed = Changed(before, Snapshot(element)).ToList();
                foreach (var property in changed) written.Add(property);
                if (changed.Count > 0 && !cycling) break;
            }

            DestroySpawned();
        }

        return written;
    }

    // ---------- поверхность MCP ----------

    private static IEnumerable<(FieldInfo field, McpParamAttribute attr, string wire)> EditFields()
    {
        foreach (var f in typeof(EditOp).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = f.GetCustomAttribute<McpParamAttribute>();
            if (attr == null) continue;
            if (f.Name == nameof(EditOp.name)) continue;
            yield return (f, attr, string.IsNullOrEmpty(attr.AgentName) ? f.Name : attr.AgentName!);
        }
    }

    /// <summary>Значения, которыми поле пробуется. Перебор идёт до первой
    /// правки: поле, которое ничего не изменило ни одним кандидатом, для этого
    /// класса недоступно — и это факт, а не догадка по имени.</summary>
    private IEnumerable<object> Candidates(FieldInfo field, McpParamAttribute attr)
    {
        if (attr.Enum != null && attr.Enum.Length > 0)
        {
            foreach (var value in attr.Enum) yield return value;
            yield break;
        }

        var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

        if (type == typeof(bool)) { yield return true; yield return false; yield break; }

        if (type == typeof(int))
        {
            foreach (var v in new[] { 137, 400, 700, 5, 1 }) yield return v;
            yield break;
        }

        if (type == typeof(float) || type == typeof(double))
        {
            foreach (var v in new[] { 1.5f, 3f, 0.25f }) yield return v;
            yield break;
        }

        foreach (var v in StringCandidates(field.Name)) yield return v;
    }

    private IEnumerable<string> StringCandidates(string fieldName)
    {
        if (fieldName == "material" || fieldName.EndsWith("_material", StringComparison.Ordinal))
        {
            foreach (var def in MaterialCatalog.All) yield return def.id;
            yield break;
        }

        switch (fieldName)
        {
            case "new_name": yield return "Probe2"; break;
            case "grooves": yield return "through:top"; break;
            case "edge_sides": yield return "L1:on; W1:off"; break;
            case "screw_thread":
                foreach (var thread in ScrewLegSpec.Threads) yield return thread;
                break;
            case "attached_to_name":
            case "attached_facade_name":
            case "paired_drawer_name":
                yield return _neighbourName;
                break;
            case "texture_overlays": yield return ""; break;
            default: yield break;
        }
    }

    private bool Edit(string elementName, string wire, object value)
    {
        var op = new JObject { ["name"] = elementName, [wire] = JToken.FromObject(value) };
        var request = new McpRequest
        {
            id = "parity",
            method = "edit_elements",
            Params = new JObject { ["ops"] = new JArray { op } }
        };
        return _handler!.Handle(request).type == "result";
    }

    /// <summary>Что edit_elements на самом деле правит у этого класса: по
    /// одному проволочному полю за раз, через настоящий обработчик — со всеми
    /// его отказами EditFieldRules, а не в обход них.
    ///
    /// Элемент пересоздаётся только ПОСЛЕ удавшейся правки: отказ и правка «в
    /// то же значение» его не пачкают, а вот удавшаяся — пачкает, и пачкает
    /// опасно (locked запрещает следующие правки, new_name уводит имя, по
    /// которому элемент ищут). Создание элемента с мешами — самая дорогая
    /// часть опыта, поэтому оно платится ровно там, где нужно.</summary>
    private HashSet<string> McpSurface(Func<GameObject> make)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);

        var element = Spawn(make);
        PlaceNeighbourInFrontOf(element);
        var name = element.PartName;

        foreach (var (field, attr, wire) in EditFields())
        {
            var touched = false;
            foreach (var candidate in Candidates(field, attr))
            {
                var before = Snapshot(element);
                if (!Edit(name, wire, candidate)) continue;
                var changed = Changed(before, Snapshot(element)).ToList();
                if (changed.Count == 0) continue;
                foreach (var property in changed) written.Add(property);
                touched = true;
                break;
            }

            if (!touched) continue;

            DestroySpawned();
            element = Spawn(make);
            PlaceNeighbourInFrontOf(element);
            name = element.PartName;
        }

        DestroySpawned();
        return written;
    }

    // ---------- сверка ----------

    private static bool Exempt((string owner, string property, string why)[] list,
        string owner, string property) =>
        list.Any(e => e.owner == owner && (e.property == "*" || e.property == property));

    /// <summary>Снятие обеих поверхностей — это сотни построений панели и
    /// запросов к обработчику, по два десятка секунд на класс. Результат от
    /// теста к тесту один и тот же (SetUp у всех одинаков), поэтому он
    /// считается один раз на класс, а не заново в каждом из четырёх тестов.</summary>
    private static readonly Dictionary<string, HashSet<string>> UiCache =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    private static readonly Dictionary<string, HashSet<string>> McpCache =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    private HashSet<string> Ui(string label, Func<GameObject> make)
    {
        if (!UiCache.TryGetValue(label, out var set)) UiCache[label] = set = UiSurface(make);
        return set;
    }

    private HashSet<string> Mcp(string label, Func<GameObject> make)
    {
        if (!McpCache.TryGetValue(label, out var set)) McpCache[label] = set = McpSurface(make);
        return set;
    }

    private List<string> Divergences(bool uiSide)
    {
        var report = new List<string>();
        foreach (var (label, make) in Specimens())
        {
            var ui = Ui(label, make);
            var mcp = Mcp(label, make);
            var missing = uiSide ? ui.Except(mcp) : mcp.Except(ui);
            var list = uiSide ? UiOnly : McpOnly;
            foreach (var property in missing.OrderBy(p => p, StringComparer.Ordinal))
                if (!Exempt(list, label, property))
                    report.Add(label + "." + property);
        }
        return report;
    }

    [Test]
    public void EveryPropertyThePanelCanEdit_IsAlsoEditableThroughMcp()
    {
        var missing = Divergences(uiSide: true);

        Assert.IsEmpty(missing,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: человек правит это свойство в панели, а агент через "
            + "edit_elements — нет. Проект, собранный агентом, будет отличаться от собранного "
            + "руками, и никакой ошибки при этом не покажут: MCP просто промолчит о свойстве. "
            + "ЧТО СДЕЛАТЬ: завести проволочное поле в EditOp, разрешить его этому типу в "
            + "EditFieldRules и написать правку в ElementEditAppliers — либо, если "
            + "недоступность агенту осознанна, добавить запись с причиной в UiOnly. "
            + McpUiParityRule.PropertyAddresses
            + "Только в панели: " + string.Join(", ", missing));
    }

    [Test]
    public void EveryPropertyMcpCanEdit_IsAlsoEditableInThePanel()
    {
        var extra = Divergences(uiSide: false);

        Assert.IsEmpty(extra,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: агент правит это свойство, а человек в панели — нет. Свойство, "
            + "которого нет в панели, человек не увидит и не отменит: он не поймёт, откуда "
            + "взялось состояние, которое он не задавал. "
            + "ЧТО СДЕЛАТЬ: завести строку в подходящем *FieldsEditor — либо, если "
            + "отсутствие в панели осознанно, добавить запись с причиной в McpOnly. "
            + McpUiParityRule.PropertyAddresses
            + "Только в MCP: " + string.Join(", ", extra));
    }

    /// <summary>Сторож самого сторожа. Обе поверхности снимаются опытом, и опыт
    /// может не удаться молча: панель не построилась, обработчик отказал на
    /// всём, слепок не увидел ни одного свойства — и тогда обе стороны пусты,
    /// разница пуста, тест зелен и не проверяет ничего.</summary>
    [Test]
    public void TheProbe_ActuallyDrivesBothSurfaces()
    {
        var stool = Specimens().First(s => s.label == "StoolElement").make;
        var ui = Ui("StoolElement", stool);
        var mcp = Mcp("StoolElement", stool);

        CollectionAssert.Contains(ui, nameof(StoolElement.CornerRadiusMM),
            "поле «Скругление» стоит в панели табуретки — если опыт его не увидел, "
            + "он не увидит и остальные, а тест позеленеет на пустом");
        CollectionAssert.Contains(mcp, nameof(StoolElement.CornerRadiusMM),
            "corner_radius объявлен для табуретки в EditFieldRules — если запрос ничего "
            + "не изменил, опыт не доехал до обработчика");
        CollectionAssert.Contains(ui, nameof(KitchenElement.DimensionsMM),
            "габариты правятся из панели у любого элемента");
        CollectionAssert.Contains(mcp, nameof(KitchenElement.DimensionsMM),
            "габариты правятся через width/height/depth у любого элемента");

        Assert.GreaterOrEqual(ui.Count, 5,
            "у табуретки панель правит имя, габариты, положение, материал и скругление — "
            + "меньше пяти свойств значит, что виджеты не нашлись");
        Assert.GreaterOrEqual(mcp.Count, 5,
            "столько же обязан уметь и агент");
    }

    /// <summary>Список исключений обязан гнить громко: запись, чьё расхождение
    /// уже закрыто, молча прикроет следующее забытое свойство того же типа.</summary>
    [Test]
    public void EveryExemption_StillNamesALiveDivergence()
    {
        var known = Specimens().Select(s => s.label).ToList();

        foreach (var (owner, property, why) in UiOnly.Concat(McpOnly))
        {
            Assert.IsNotEmpty(why,
                "исключение без причины через полгода не отличить от недосмотра: "
                + owner + "." + property);
            CollectionAssert.Contains(known, owner,
                "исключение числится за классом, которого больше нет среди образцов: " + owner);
        }

        foreach (var (owner, property, _) in McpOnly)
        {
            var make = Specimens().First(s => s.label == owner).make;
            var stillOnlyInMcp = Mcp(owner, make).Contains(property) && !Ui(owner, make).Contains(property);
            Assert.IsTrue(stillOnlyInMcp,
                "расхождение закрыто, а запись осталась и теперь прикрывает следующее "
                + "забытое свойство — убрать из McpOnly: " + owner + "." + property);
        }
    }

    /// <summary>Классы элементов берутся из исходников третьим источником,
    /// который не знает ни про панель, ни про контракт. Забытый в списке
    /// образцов класс иначе выпал бы из сверки молча — ровно так, как из MCP
    /// выпадали мойка и пуфик.</summary>
    [Test]
    public void EveryElementClassInTheProject_HasASpecimen()
    {
        var covered = Specimens().Select(s => s.label).ToList();
        var declared = KitchenDesigner.Tests.Geometry.ElementTypeCatalog.FromSources();

        var missing = declared.Where(t => !covered.Contains(t)).ToList();

        Assert.IsEmpty(missing,
            "класс элемента есть в исходниках, но образца для сверки поверхностей у него "
            + "нет — расхождение панели и MCP по нему никто не увидит. Добавить образец в "
            + "Specimens(). Без образца: " + string.Join(", ", missing));
    }
}

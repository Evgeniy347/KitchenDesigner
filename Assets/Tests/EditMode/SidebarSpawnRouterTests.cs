using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.UI;

/// <summary>Путь от кнопки сайдбара до фабрики. Раньше он не проверялся
/// ничем, и проверить его было нечем: ветвление сидело в приватном методе
/// MonoBehaviour, которому нужен живой UIManager.Instance, а в EditMode он не
/// оживает — тест молча уходил бы в `if (UIManager.Instance == null) return`
/// и зеленел на пустом.
///
/// С 2026-09-10 <see cref="IElementSpawns"/> сжался до ОДНОГО метода
/// (<c>Spawn(SidebarCatalog.Item)</c>) — раньше здесь стоял подставной
/// приёмник (Recorder), который реализовывал ~30 методов интерфейса и просто
/// запоминал, какой из них позвали. Это давало чёрный ящик ДО фабрики: видно
/// было, какой Spawn* метод выбрал маршрутизатор, но не то, что тот метод
/// реально построил. Одного метода Spawn(Item) для такого наблюдения уже не
/// хватает — ветвление теперь целиком внутри самой реализации, и подставной
/// приёмник его не видит вовсе.
///
/// Поэтому сенсор здесь сменился на РЕЗУЛЬТАТ: тесты гоняют настоящий
/// <see cref="SidebarThumbnailSpawns"/> (тот же путь, что миниатюра плитки —
/// без сцены, без камеры, без PlacementController, см. ElementFactorySandbox)
/// и разглядывают получившийся компонент — его конкретный тип и то, что можно
/// снять с него рефлексией. Это даже строже прежнего: раньше проверялось, что
/// маршрутизатор ПОЗВАЛ правильный метод с правильными аргументами; теперь —
/// что фабрика и правда ПОСТРОИЛА то, что обещано.
///
/// Ловушка, ради которой тест написан, была живой раньше: у сборного фасада
/// истинны были ОБА булевых признака каталога — и «сборный», и «фасад», —
/// потому что isFacade отвечал «да» на оба вида. Признаки is* с тех пор
/// удалены (см. SidebarCatalog.Item), но тест остаётся стражем формы:
/// сборный фасад обязан рождать другой КОМПОНЕНТ (AssembledFacadeElement), а
/// не читать флаг молча.</summary>
public class SidebarSpawnRouterTests
{
    [TearDown]
    public void TearDown() => PartRegistry.Clear();

    private static IEnumerable<SidebarCatalog.Item> CatalogItems() =>
        SidebarCatalog.Build().SelectMany(g => g.items);

    private static SidebarCatalog.Item ItemOfKind(SidebarItemKind kind) =>
        new SidebarCatalog.Item("Проба", new Vector3Int(600, 400, 18), kind);

    /// <summary>Заводит item настоящим спауном миниатюры (тот же switch, что
    /// у ElementSpawner, но без постановки на сцену) и возвращает получившийся
    /// KitchenElement. Вызывающий обязан уничтожить объект сам.</summary>
    private static KitchenElement SpawnReal(SidebarCatalog.Item item)
    {
        GameObject go;
        using (ElementFactorySandbox.Enter())
            go = SidebarThumbnailSpawns.For(item)!();

        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element,
            "спаун «" + item.name + "» (" + item.kind + ") вернул объект без KitchenElement");
        return element!;
    }

    private static void Destroy(KitchenElement element)
    {
        element.PrepareForDestruction();
        UnityEngine.Object.DestroyImmediate(element.gameObject);
    }

    /// <summary>Стена — единственный законный вход, у которого CLR-тип совпадает с обычной
    /// доской: `ElementFactoryInstance.CreateWall` строит plain `KitchenElement` и вешает
    /// маркерный компонент `Wall` поверх него (тот же приём, каким `IsFlatBoardElement` и
    /// `SupportsGrooves` отличают стену от доски в продакшне) — своего класса у стены нет и не
    /// предполагается. Голое сравнение CLR-типов не видит этого и объявляло бы стену
    /// «провалившейся в доску» при каждом прогоне. Проверяем и тип, И отсутствие маркера: вид,
    /// который действительно провалился в CreatePart, не несёт вообще никакого компонента
    /// сверх обычной доски — а стена несёт `Wall`.</summary>
    private static bool IsIndistinguishableFromPlainBoard(KitchenElement element, Type boardType) =>
        element.GetType() == boardType && element.GetComponent<Wall>() == null;

    /// <summary>Снимок наблюдаемого поведения спауна: конкретный тип
    /// компонента (различает вид И пресеты, которые меняют класс — сборный
    /// фасад, каждый фитинг трубы) плюс габариты плюс всё, что можно снять
    /// рефлексией с самого компонента без побочных эффектов — простые
    /// публичные свойства (строка/число/булево/перечисление). Это и есть
    /// сенсор «поле каталога дошло до готового объекта», а не только «дошло
    /// до вызова фабрики»: последнее уже проверяла Recorder-версия этого
    /// файла, и её обманул бы перепутанный порядок аргументов внутри самой
    /// реализации IElementSpawns — сюда он уже не спрячется.</summary>
    private static string Signature(SidebarCatalog.Item item)
    {
        var element = SpawnReal(item);
        try
        {
            var parts = new List<string> { element.GetType().Name, element.DimensionsMM.ToString() };

            foreach (var prop in element.GetType()
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!(prop.PropertyType.IsEnum || prop.PropertyType == typeof(string)
                      || prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(int)
                      || prop.PropertyType == typeof(float))) continue;
                object? value;
                try { value = prop.GetValue(element); }
                catch { continue; }
                parts.Add(prop.Name + "=" + (value?.ToString() ?? "null"));
            }
            return string.Join("|", parts);
        }
        finally
        {
            Destroy(element);
        }
    }

    /// <summary>Главная проверка. «Обычная деталь» — единственный вид, который
    /// маршрутизатор имеет право отдать в CreatePart; для всех прочих это
    /// признак того, что ветка не написана и вид провалился в общий случай.
    /// Именно так выглядит забытый объект: кнопка есть, нажатие работает, и
    /// вместо дивана появляется доска.</summary>
    [Test]
    public void EverySidebarItemKind_HasItsOwnBranch_AndDoesNotFallThroughToAPlainBoard()
    {
        var board = SpawnReal(ItemOfKind(SidebarItemKind.Board));
        var boardType = board.GetType();
        Destroy(board);

        var fallen = new List<string>();

        foreach (SidebarItemKind kind in Enum.GetValues(typeof(SidebarItemKind)))
        {
            if (kind == SidebarItemKind.Board) continue;
            var element = SpawnReal(ItemOfKind(kind));
            bool isBoard = IsIndistinguishableFromPlainBoard(element, boardType);
            Destroy(element);
            if (isBoard) fallen.Add(kind.ToString());
        }

        Assert.IsEmpty(fallen,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: вид объекта есть в SidebarItemKind, но ветки в ElementSpawner/"
            + "SidebarThumbnailSpawns у него нет — нажатие на кнопку сайдбара молча заводит "
            + "обычную доску вместо объекта. Молчаливый успех: ни ошибки, ни пустоты, просто "
            + "не то. "
            + "ЧТО СДЕЛАТЬ: дописать ветку в Spawn(Item) обеих реализаций IElementSpawns и "
            + "убедиться, что тот же объект заводится агентом через create_elements. "
            + McpUiParityRule.CreationAddresses
            + "Проваливаются в доску: " + string.Join(", ", fallen));
    }

    /// <summary>То же самое, но по НАСТОЯЩЕМУ каталогу: вид может иметь ветку и
    /// при этом не иметь кнопки, а может иметь кнопку с неверно проставленным
    /// видом. Перебор перечисления этого не видит — он не знает, что каталог
    /// вообще предлагает.</summary>
    [Test]
    public void EveryButtonTheCatalogOffers_ReachesASpawn()
    {
        var board = SpawnReal(ItemOfKind(SidebarItemKind.Board));
        var boardType = board.GetType();
        Destroy(board);

        var silent = new List<string>();

        foreach (var item in CatalogItems())
        {
            var element = SpawnReal(item);
            bool isBoard = IsIndistinguishableFromPlainBoard(element, boardType);
            Destroy(element);
            if (item.kind != SidebarItemKind.Board && isBoard)
                silent.Add(item.name + " (" + item.kind + ")");
        }

        Assert.IsEmpty(silent,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: кнопка сайдбара заводит обычную доску вместо объекта, который "
            + "обещает её название. "
            + "ЧТО СДЕЛАТЬ: дописать ветку в Spawn(Item) обеих реализаций IElementSpawns. "
            + McpUiParityRule.CreationAddresses
            + "Кнопки: " + string.Join(", ", silent));
    }

    /// <summary>Регрессия на ловушку, ради которой всё это писалось. У сборного
    /// фасада раньше были истинны оба флага каталога (`isFacade` и
    /// `isAssembled`), и до извлечения ветвления порядок двух строк решал,
    /// каким он окажется. Флаги `is*` были удалены вместе с самой возможностью
    /// завести такую ловушку — но `AssembledFacade` тогда ещё был ОТДЕЛЬНЫМ
    /// `SidebarItemKind`, а не пресетом внутри `Facade` (сведение — см.
    /// docs/todo_evolution.md §2.1, тот же приём, что уже применён к
    /// фитингам трубы и к системе ящика). Сведение вернуло РОВНО ту форму
    /// ветвления, от которой уходили: `kind == Facade` плюс булев признак
    /// `preset.facadeAssembled` поверх него.</summary>
    [Test]
    public void TheAssembledFacade_StaysAssembled_AndDoesNotDegradeToAPlainFacade()
    {
        var item = ItemOfKind(SidebarItemKind.Facade);
        item.preset.facadeAssembled = true;

        var element = SpawnReal(item);
        try
        {
            Assert.IsInstanceOf<AssembledFacadeElement>(element,
                "сборный фасад (preset.facadeAssembled = true) приехал щитовым — признак из "
                + "каталога не долетел до готового объекта");
        }
        finally { Destroy(element); }
    }

    /// <summary>Обратный вход к тесту выше: без признака та же самая запись
    /// обязана остаться щитовой. Один тест на «true» ничего не доказывает,
    /// если фабрика на самом деле всегда строит AssembledFacadeElement —
    /// нужны оба значения признака, дающие РАЗНЫЕ компоненты.</summary>
    [Test]
    public void ThePlainFacade_DoesNotBecomeAssembled_WhenTheFlagIsFalse()
    {
        var item = ItemOfKind(SidebarItemKind.Facade);
        item.preset.facadeAssembled = false;

        var element = SpawnReal(item);
        try
        {
            Assert.IsInstanceOf<FacadeElement>(element, "щитовой фасад обязан быть FacadeElement");
            Assert.IsNotInstanceOf<AssembledFacadeElement>(element,
                "щитовой фасад (preset.facadeAssembled = false) приехал сборным");
        }
        finally { Destroy(element); }
    }

    /// <summary>Аргументы теряются так же тихо, как ветки. У варочной
    /// поверхности несколько моделей, и модель — единственное, что отличает их
    /// друг от друга: потерянная по дороге, она даёт готовый объект без
    /// модели вместо модели выбранной.</summary>
    [Test]
    public void TheCooktopModel_TravelsFromTheCatalogToTheSpawn()
    {
        var withModel = CatalogItems()
            .Where(i => i.kind == SidebarItemKind.Cooktop && !string.IsNullOrEmpty(i.preset.applianceModel))
            .ToList();

        Assert.IsNotEmpty(withModel,
            "в каталоге нет ни одной варочной с моделью — проверка сторожила бы пустоту");

        foreach (var item in withModel)
        {
            var element = SpawnReal(item);
            try
            {
                Assert.IsInstanceOf<CooktopElement>(element, "варочная обязана быть CooktopElement");
                Assert.AreEqual(item.preset.applianceModel, ((CooktopElement)element).Model,
                    "модель варочной не доехала от каталога до готового объекта: кнопка «"
                    + item.name + "» заведёт поверхность без модели выбранной");
            }
            finally { Destroy(element); }
        }
    }

    /// <summary>Ящик отличается от прочих тем, что вид системы едет строкой, а
    /// не числом или признаком: строка сверяется с образцом, и ЛЮБАЯ опечатка в
    /// каталоге молча даёт GTV вместо Movento — отказа не будет, будет другой
    /// ящик.</summary>
    [Test]
    public void TheMoventoDrawer_ArrivesAsMovento_NotAsGtv()
    {
        var movento = CatalogItems()
            .Where(i => i.kind == SidebarItemKind.Drawer
                        && i.preset.drawerSystem == SidebarCatalog.MoventoDrawerSystem)
            .ToList();

        Assert.IsNotEmpty(movento,
            "в каталоге нет ни одного ящика Movento — проверка сторожила бы пустоту");

        foreach (var item in movento)
        {
            var element = SpawnReal(item);
            try
            {
                Assert.IsInstanceOf<DrawerElement>(element, "ящик обязан быть DrawerElement");
                Assert.AreEqual(DrawerSystem.Movento, ((DrawerElement)element).System,
                    "ящик Movento приехал системой GTV. Строка вида системы в каталоге сверяется "
                    + "с образцом, и опечатка в ней отказа не даёт — даёт другой ящик: "
                    + item.name);
            }
            finally { Destroy(element); }
        }
    }

    /// <summary>Мутация значения поля пресета: значение подменяется на
    /// заведомо другое того же типа. Новый тип поля обязан приехать сюда
    /// явно — иначе механизм молча перестал бы проверять новое поле, а это
    /// ровно тот вид молчания, против которого он написан.</summary>
    private static object? MutatedValue(object? value)
    {
        switch (value)
        {
            // Префикс, а не суффикс: спаун имени и большинства строковых полей проходит через
            // ElementNaming.Sanitize, а тот ЗАВЕРШАЕТ санитайзинг вызовом Trim('_') — символ
            // вроде "*" на конце строки не транслитерируется и не отбрасывается, а превращается
            // в "_", который тут же обрезается тем же Trim. "Имя*" и "Имя" сануются в ОДНУ и ту
            // же строку, мутация исчезает бесследно, и «имя доезжает до спауна» перестаёт быть
            // наблюдаемым — не потому что имя не доехало, а потому что сенсор ослеп на
            // собственной мутации. Префикс не встречает этой ловушки: ведущий "Z" не подпадает
            // ни под Trim (тот трогает лишь края уже собранной строки после свёртки), ни под
            // отбрасывание, и остаётся в сануированном результате.
            case string s: return "Z" + s;
            case int i: return i + 137;
            case Vector3Int v: return v + new Vector3Int(7, 11, 13);
            case SidebarItemKind k:
                return k == SidebarItemKind.Sofa ? SidebarItemKind.Board : SidebarItemKind.Sofa;
            case PipeNodeKind p:
                return p == PipeNodeKind.Coupling ? PipeNodeKind.Tee : PipeNodeKind.Coupling;
            case bool b: return !b;
            default: return null;
        }
    }

    /// <summary>Путь к одному полю: либо прямо на Item (name/dims/kind), либо
    /// на его единственном вложенном объекте-пресете (preset.*). Рефлексия
    /// идёт РОВНО на один уровень вглубь — ровно столько, на сколько
    /// SidebarCatalog.Item сегодня прячет тип-специфичные поля. Новое поле,
    /// добавленное в SidebarCatalog.Preset, попадает под перебор само, без
    /// правки этого файла.</summary>
    private readonly struct FieldPath
    {
        public readonly string Label;
        private readonly FieldInfo _outer;
        private readonly FieldInfo? _inner;

        public FieldPath(string label, FieldInfo outer, FieldInfo? inner)
        {
            Label = label; _outer = outer; _inner = inner;
        }

        public object? GetValue(SidebarCatalog.Item item) =>
            _inner == null ? _outer.GetValue(item) : _inner.GetValue(_outer.GetValue(item));

        public SidebarCatalog.Item With(SidebarCatalog.Item item, object value)
        {
            object boxed = item;
            if (_inner == null) { _outer.SetValue(boxed, value); return (SidebarCatalog.Item)boxed; }
            object nested = _outer.GetValue(boxed)!;
            _inner.SetValue(nested, value);
            _outer.SetValue(boxed, nested);
            return (SidebarCatalog.Item)boxed;
        }
    }

    private static bool IsLeafFieldType(Type t) =>
        t.IsEnum || t == typeof(string) || t == typeof(bool) || t == typeof(int)
        || t == typeof(Vector3Int);

    private static IEnumerable<FieldPath> ItemFieldPaths()
    {
        foreach (var outer in typeof(SidebarCatalog.Item).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsLeafFieldType(outer.FieldType))
            {
                yield return new FieldPath(outer.Name, outer, null);
                continue;
            }
            foreach (var inner in outer.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                yield return new FieldPath(outer.Name + "." + inner.Name, outer, inner);
        }
    }

    /// <summary>Механизм против мёртвого числа в каталоге. Поле Item (или поле
    /// его пресета), которое ни для одной записи не меняет наблюдаемый
    /// результат спауна, — это настройка, которая выглядит настройкой и ни на
    /// что не влияет: следующий человек поправит её и не поймёт, почему ничего
    /// не изменилось.
    ///
    /// Так и было: у сборного фасада запись каталога объявляла четыре зазора, а
    /// спаун их не принимал. Проверка одной записи тут не годится — щитовой
    /// фасад те же зазоры передавал, и «поле живое» было правдой ровно
    /// наполовину. Поэтому спрашиваем не про запись, а про ПОЛЕ, и спрашиваем
    /// подменой: значение меняется на заведомо другое, и если после этого
    /// готовый объект не меняется никак — поле не доезжает никуда.</summary>
    [Test]
    public void EveryFieldOfACatalogItem_ReachesTheSpawner()
    {
        var items = CatalogItems().ToList();
        var paths = ItemFieldPaths().ToList();
        var unknownType = new List<string>();
        var dead = new List<string>();

        foreach (var path in paths)
        {
            bool observed = false;
            foreach (var item in items)
            {
                object? mutated = MutatedValue(path.GetValue(item));
                if (mutated == null) { unknownType.Add(path.Label); break; }
                if (Signature(path.With(item, mutated)) != Signature(item))
                {
                    observed = true;
                    break;
                }
            }
            if (!observed && !unknownType.Contains(path.Label)) dead.Add(path.Label);
        }

        Assert.IsEmpty(unknownType,
            "механизм не умеет подменять значение поля такого типа, поэтому проверить его "
            + "не может и молча пропустил бы: допишите тип в MutatedValue. Поля: "
            + string.Join(", ", unknownType));

        Assert.IsEmpty(dead,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: поле каталога сайдбара (или поле его пресета) не доезжает до "
            + "готового объекта ни для одной записи. Оно выглядит настройкой и ею не является: "
            + "следующий человек поправит число и не поймёт, почему объект не изменился. Так "
            + "уже было с зазорами сборного фасада. "
            + "ЧТО СДЕЛАТЬ: либо протянуть значение через Spawn(Item) до фабрики, либо убрать "
            + "поле из SidebarCatalog.Item/Preset и оставить одно место, где величина задаётся — "
            + "константу на самом элементе (FacadeElement.DEFAULT_GAP_MM и подобные). "
            + "Мёртвые поля: " + string.Join(", ", dead));
    }

    /// <summary>Сторож механизма выше. Он весь стоит на сравнении двух подписей,
    /// и обе его половины могут быть сломаны молча: сравнение, которое ВСЕГДА
    /// показывает разницу, объявит живыми любые поля, а сравнение, которое
    /// НИКОГДА её не показывает, объявит мёртвыми все. Плюс перебор полей: пустой
    /// список полей — это зелёный ноль мёртвых полей.</summary>
    [Test]
    public void TheDeadFieldScan_SeesTheFields_AndItsComparisonWorksBothWays()
    {
        var paths = ItemFieldPaths().ToList();
        Assert.GreaterOrEqual(paths.Count, 8,
            "у записи каталога около десятка полей (с учётом пресета); меньше — значит "
            + "рефлексия читает не тот тип, и перебор по полям проверяет пустоту: "
            + string.Join(", ", paths.Select(p => p.Label)));

        var drawer = CatalogItems().First(i => i.kind == SidebarItemKind.Drawer);

        Assert.AreEqual(Signature(drawer), Signature(drawer),
            "одна и та же запись обязана давать одну и ту же подпись — иначе сравнение "
            + "показывает разницу всегда, и ни одно поле не может быть признано мёртвым");

        var name = paths.First(p => p.Label == "name");
        Assert.AreNotEqual(Signature(drawer),
            Signature(name.With(drawer, MutatedValue(name.GetValue(drawer))!)),
            "имя доезжает до спауна во ВСЕХ ветках, поэтому его подмена обязана менять "
            + "подпись — если не меняет, сравнение слепо и объявит мёртвыми все поля");
    }

    /// <summary>Критерий приёмки D16 (docs/todo_evolution.md §2.1), закреплённый
    /// механизмом, а не списком имён: добавление новой ПОЗИЦИИ существующего
    /// вида не имеет права требовать нового метода на интерфейсе спауна.
    /// Раньше у каждого вида был свой Spawn* метод — новый параметр пресета
    /// (скажем, ещё одна опция ящика) правил сигнатуру здесь, в
    /// SidebarSpawnRouter и в ОБЕИХ реализациях сразу. Если этот тест
    /// покраснел, значит рецепт снова растёкся: кто-то опять завёл метод на
    /// каждый вид вместо того, чтобы читать новое поле прямо из
    /// <see cref="SidebarCatalog.Item"/>/<see cref="SidebarCatalog.Preset"/>
    /// внутри уже существующей ветки Spawn(Item).</summary>
    [Test]
    public void IElementSpawns_NeverGrowsAMethodPerType_OnlyOneSpawnEntryPoint()
    {
        var methods = typeof(IElementSpawns).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.AreEqual(1, methods.Length,
            "IElementSpawns обзавёлся ещё одним методом — это ровно тот способ, каким "
            + "новая позиция каталога раньше стоила шести мест правки (SidebarItemKind, "
            + "SidebarCatalog.Item, case в SidebarSpawnRouter, метод в IElementSpawns и обе "
            + "его реализации). Новое поле пресета существующего вида обязано читаться прямо "
            + "внутри Spawn(SidebarCatalog.Item), а не приезжать отдельным параметром. Методы: "
            + string.Join(", ", methods.Select(m => m.Name)));

        Assert.AreEqual(nameof(IElementSpawns.Spawn), methods[0].Name,
            "единственный метод интерфейса обязан называться Spawn и принимать весь "
            + "SidebarCatalog.Item целиком");

        var parameters = methods[0].GetParameters();
        Assert.AreEqual(1, parameters.Length,
            "Spawn обязан принимать РОВНО один параметр — весь пресет целиком, а не набор "
            + "примитивов, который пришлось бы менять при каждом новом поле");
        Assert.AreEqual(typeof(SidebarCatalog.Item), parameters[0].ParameterType,
            "единственный параметр Spawn обязан быть SidebarCatalog.Item — иначе новое поле "
            + "пресета опять не сможет доехать без правки сигнатуры");
    }

    /// <summary>Сторож сторожа. Спаун — настоящий, и он может оказаться нем:
    /// пустой каталог, спаун, который всегда строит одно и то же. Тогда все
    /// проверки выше зеленеют на пустоте.</summary>
    [Test]
    public void TheRealSpawn_ActuallyBuildsDifferentObjects_AndTheCatalogIsNotEmpty()
    {
        var kinds = Enum.GetValues(typeof(SidebarItemKind)).Length;
        Assert.GreaterOrEqual(kinds, 20,
            "видов объектов в сайдбаре два десятка; меньше — значит перечисление читается "
            + "не то");

        Assert.GreaterOrEqual(CatalogItems().Count(), 20,
            "каталог сайдбара предлагает два десятка кнопок; меньше — значит Build() вернул "
            + "не то, и перебор по каталогу ничего не проверяет");

        var board = SpawnReal(ItemOfKind(SidebarItemKind.Board));
        var boardType = board.GetType();
        Destroy(board);

        var sofa = SpawnReal(ItemOfKind(SidebarItemKind.Sofa));
        var sofaType = sofa.GetType();
        Destroy(sofa);

        Assert.AreNotEqual(boardType, sofaType,
            "диван обязан заводить компонент, отличный от обычной доски — положительный "
            + "контроль на то, что разные виды приезжают в РАЗНЫЕ объекты, а не все в один");
    }
}

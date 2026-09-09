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
/// Дыра тут не теоретическая, и она НЕ ловится сверкой
/// <see cref="McpUiCreationParityTests"/>: та сверяет, какие методы фабрики
/// зовёт ElementSpawner, и остаётся зелёной, пока сам файл не изменился. Ветка,
/// зовущая не тот метод, потерянный вид объекта, перепутанный порядок
/// проверок — всё это происходит ВЫШЕ, между каталогом и ElementSpawner, и обе
/// сверки этого не видят.
///
/// Ловушка, ради которой тест написан, была живой раньше: у сборного фасада
/// истинны были ОБА булевых признака каталога — и «сборный», и «фасад», —
/// потому что isFacade отвечал «да» на оба вида. Старое ветвление работало
/// только потому, что ветка сборного стояла в тексте ВЫШЕ ветки щитового.
/// Перестановка двух соседних строк молча превращала все сборные фасады в
/// щитовые, и ни один тест не краснел. Признаки is* с тех пор удалены (см.
/// SidebarCatalog.Item), но тест остаётся стражем формы — маршрутизатор
/// обязан ветвиться по единственному kind, а не собирать заново булевы
/// флаги поверх него.
///
/// Поверхность снимается ЗАПИСЬЮ, а не созданием: подставной приёмник
/// запоминает, какой метод его позвали и с чем. Поэтому тест не строит мешей,
/// не нуждается ни в камере, ни в сцене и идёт мгновенно.</summary>
public class SidebarSpawnRouterTests
{
    /// <summary>Приёмник, который ничего не создаёт, а только записывает вызов.
    /// Реализует тот же интерфейс, что и настоящий ElementSpawner, поэтому
    /// новый метод спауна нельзя добавить, забыв про этот тест: файл перестанет
    /// компилироваться.</summary>
    private sealed class Recorder : IElementSpawns
    {
        public string Method = "";
        public readonly List<object> Args = new List<object>();

        private void Put(string method, params object[] args)
        {
            Assert.IsEmpty(Method,
                "маршрутизатор позвал спаун дважды за одно нажатие: кнопка сайдбара обязана "
                + "заводить ровно один объект. Было " + Method + ", стало " + method);
            Method = method;
            Args.AddRange(args);
        }

        public void SpawnBoard(Vector3Int dims, string name) => Put(nameof(SpawnBoard), dims, name);

        public void SpawnFacade(Vector3Int dims, string name) =>
            Put(nameof(SpawnFacade), dims, name);

        public void SpawnAssembledFacade(Vector3Int dims, string name, AssembledFill fill) =>
            Put(nameof(SpawnAssembledFacade), dims, name, fill);

        public void SpawnWall(Vector3Int dims, string name) => Put(nameof(SpawnWall), dims, name);

        public void SpawnDrawer(string drawerType, int length, string colorName, int width,
            string name, DrawerSystem system) =>
            Put(nameof(SpawnDrawer), drawerType, length, colorName, width, name, system);

        public void SpawnTable(Vector3Int dims, string name) => Put(nameof(SpawnTable), dims, name);

        public void SpawnRadiusTable(Vector3Int dims, string name) => Put(nameof(SpawnRadiusTable), dims, name);

        public void SpawnStool(Vector3Int dims, string name) => Put(nameof(SpawnStool), dims, name);

        public void SpawnChair(Vector3Int dims, string name) => Put(nameof(SpawnChair), dims, name);

        public void SpawnSofa(Vector3Int dims, string name) => Put(nameof(SpawnSofa), dims, name);

        public void SpawnBed(Vector3Int dims, string name) => Put(nameof(SpawnBed), dims, name);

        public void SpawnPouffe(Vector3Int dims, string name) => Put(nameof(SpawnPouffe), dims, name);

        public void SpawnToilet(string name) => Put(nameof(SpawnToilet), name);

        public void SpawnWallHungToilet(string name) => Put(nameof(SpawnWallHungToilet), name);

        public void SpawnBathtub(Vector3Int dims, string name) => Put(nameof(SpawnBathtub), dims, name);

        public void SpawnBathMixer(string name) => Put(nameof(SpawnBathMixer), name);

        public void SpawnShowerColumn(string name) => Put(nameof(SpawnShowerColumn), name);

        public void SpawnSocket(string name) => Put(nameof(SpawnSocket), name);

        public void SpawnLightSwitch(string name) => Put(nameof(SpawnLightSwitch), name);

        public void SpawnPanel(Vector3Int dims, string name) =>
            Put(nameof(SpawnPanel), dims, name);

        public void SpawnRadialShelf(Vector3Int dims, string name) => Put(nameof(SpawnRadialShelf), dims, name);

        public void SpawnWindow(Vector3Int dims, string name) => Put(nameof(SpawnWindow), dims, name);

        public void SpawnDoor(Vector3Int dims, string name) => Put(nameof(SpawnDoor), dims, name);

        public void SpawnScrewLeg(string name) => Put(nameof(SpawnScrewLeg), name);

        public void SpawnPillar(int midHeightMM, string name) => Put(nameof(SpawnPillar), midHeightMM, name);

        public void SpawnPipe(string name) => Put(nameof(SpawnPipe), name);

        public void SpawnPipeFitting(PipeNodeKind kind, string name) =>
            Put(nameof(SpawnPipeFitting), kind, name);

        public void SpawnFloor(Vector3Int dims, string name) => Put(nameof(SpawnFloor), dims, name);

        public void SpawnSink(string name) => Put(nameof(SpawnSink), name);

        public void SpawnCooktop(string name, string model) => Put(nameof(SpawnCooktop), name, model);

        public void SpawnOven(string name) => Put(nameof(SpawnOven), name);

        public void SpawnDishwasher(string name) => Put(nameof(SpawnDishwasher), name);

        public void SpawnLightSource(string name) => Put(nameof(SpawnLightSource), name);
    }

    private static Recorder Route(SidebarCatalog.Item item)
    {
        var recorder = new Recorder();
        SidebarSpawnRouter.Route(item, recorder);
        return recorder;
    }

    private static IEnumerable<SidebarCatalog.Item> CatalogItems() =>
        SidebarCatalog.Build().SelectMany(g => g.items);

    private static SidebarCatalog.Item ItemOfKind(SidebarItemKind kind) =>
        new SidebarCatalog.Item("Проба", new Vector3Int(600, 400, 18), kind);

    /// <summary>Главная проверка. «Обычная деталь» — единственный вид, который
    /// маршрутизатор имеет право отдать в SpawnBoard; для всех прочих это
    /// признак того, что ветка не написана и вид провалился в общий случай.
    /// Именно так выглядит забытый объект: кнопка есть, нажатие работает, и
    /// вместо дивана появляется доска.</summary>
    [Test]
    public void EverySidebarItemKind_HasItsOwnBranch_AndDoesNotFallThroughToAPlainBoard()
    {
        var fallen = new List<string>();

        foreach (SidebarItemKind kind in Enum.GetValues(typeof(SidebarItemKind)))
        {
            if (kind == SidebarItemKind.Board) continue;
            var recorder = Route(ItemOfKind(kind));
            if (recorder.Method == nameof(Recorder.SpawnBoard)) fallen.Add(kind.ToString());
        }

        Assert.IsEmpty(fallen,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: вид объекта есть в SidebarItemKind, но ветки в SidebarSpawnRouter "
            + "у него нет — нажатие на кнопку сайдбара молча заводит обычную доску вместо "
            + "объекта. Молчаливый успех: ни ошибки, ни пустоты, просто не то. "
            + "ЧТО СДЕЛАТЬ: дописать ветку в SidebarSpawnRouter.Route и убедиться, что тот же "
            + "объект заводится агентом через create_elements. "
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
        var silent = new List<string>();

        foreach (var item in CatalogItems())
        {
            var recorder = Route(item);
            Assert.IsNotEmpty(recorder.Method,
                "кнопка «" + item.name + "» не позвала ничего — нажатие на неё не делает ровно "
                + "ничего, и пользователь видит зависшую кнопку");
            if (item.kind != SidebarItemKind.Board && recorder.Method == nameof(Recorder.SpawnBoard))
                silent.Add(item.name + " (" + item.kind + ")");
        }

        Assert.IsEmpty(silent,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: кнопка сайдбара заводит обычную доску вместо объекта, который "
            + "обещает её название. "
            + "ЧТО СДЕЛАТЬ: дописать ветку в SidebarSpawnRouter.Route. "
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
    /// `facadeAssembled` поверх него. Разница с прежней ловушкой в том, что
    /// признак теперь ровно один, а не два конкурирующих — но проверить,
    /// что он всё ещё решает исход, а не молча теряется по дороге (как это
    /// было с зазорами сборного фасада), обязан именно этот тест.</summary>
    [Test]
    public void TheAssembledFacade_StaysAssembled_AndDoesNotDegradeToAPlainFacade()
    {
        var item = ItemOfKind(SidebarItemKind.Facade);
        item.facadeAssembled = true;

        Assert.AreEqual(nameof(Recorder.SpawnAssembledFacade), Route(item).Method,
            "сборный фасад (facadeAssembled = true) приехал щитовым — признак из каталога "
            + "не долетел до маршрутизатора");
    }

    /// <summary>Обратный вход к тесту выше: без признака та же самая запись
    /// обязана остаться щитовой. Один тест на «true» ничего не доказывает,
    /// если маршрутизатор на самом деле всегда зовёт SpawnAssembledFacade —
    /// нужны оба значения признака, дающие РАЗНЫЕ методы.</summary>
    [Test]
    public void ThePlainFacade_DoesNotBecomeAssembled_WhenTheFlagIsFalse()
    {
        var item = ItemOfKind(SidebarItemKind.Facade);
        item.facadeAssembled = false;

        Assert.AreEqual(nameof(Recorder.SpawnFacade), Route(item).Method,
            "щитовой фасад (facadeAssembled = false) приехал сборным");
    }

    /// <summary>Аргументы теряются так же тихо, как ветки. У варочной
    /// поверхности несколько моделей, и модель — единственное, что отличает их
    /// друг от друга: потерянная по дороге, она даёт габариты по умолчанию
    /// вместо габаритов выбранной модели.</summary>
    [Test]
    public void TheCooktopModel_TravelsFromTheCatalogToTheSpawn()
    {
        var withModel = CatalogItems()
            .Where(i => i.kind == SidebarItemKind.Cooktop && !string.IsNullOrEmpty(i.applianceModel))
            .ToList();

        Assert.IsNotEmpty(withModel,
            "в каталоге нет ни одной варочной с моделью — проверка сторожила бы пустоту");

        foreach (var item in withModel)
            CollectionAssert.Contains(Route(item).Args, item.applianceModel,
                "модель варочной не доехала от каталога до спауна: кнопка «" + item.name
                + "» заведёт поверхность с габаритами по умолчанию, а не выбранной модели");
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
                        && i.drawerSystem == SidebarCatalog.MoventoDrawerSystem)
            .ToList();

        Assert.IsNotEmpty(movento,
            "в каталоге нет ни одного ящика Movento — проверка сторожила бы пустоту");

        foreach (var item in movento)
            CollectionAssert.Contains(Route(item).Args, DrawerSystem.Movento,
                "ящик Movento приехал системой GTV. Строка вида системы в каталоге сверяется с "
                + "образцом, и опечатка в ней отказа не даёт — даёт другой ящик: " + item.name);
    }

    /// <summary>Мутация поля каталога: значение подменяется на заведомо другое
    /// того же типа. Новый тип поля обязан приехать сюда явно — иначе механизм
    /// молча перестал бы проверять новое поле, а это ровно тот вид молчания,
    /// против которого он написан.</summary>
    private static object? MutatedValue(object? value)
    {
        switch (value)
        {
            case string s: return s + "*";
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

    private static SidebarCatalog.Item WithField(SidebarCatalog.Item item, FieldInfo field,
        object value)
    {
        object boxed = item;
        field.SetValue(boxed, value);
        return (SidebarCatalog.Item)boxed;
    }

    /// <summary>Что именно маршрутизатор передал: метод и все аргументы. Сравнение
    /// двух таких строк отвечает на единственный вопрос механизма ниже — видно ли
    /// поле каталога с той стороны вообще.</summary>
    private static string Signature(SidebarCatalog.Item item)
    {
        var recorder = Route(item);
        return recorder.Method + "(" + string.Join(", ",
            recorder.Args.Select(a => a == null ? "null" : a.ToString() ?? "null")) + ")";
    }

    private static FieldInfo[] ItemFields() =>
        typeof(SidebarCatalog.Item).GetFields(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>Механизм против мёртвого числа в каталоге. Поле Item, которое
    /// маршрутизатор не передаёт НИ ДЛЯ ОДНОЙ записи, — это настройка, которая
    /// выглядит настройкой и ни на что не влияет: следующий человек поправит её
    /// и не поймёт, почему ничего не изменилось.
    ///
    /// Так и было: у сборного фасада запись каталога объявляла четыре зазора, а
    /// SpawnAssembledFacade их не принимал. Проверка одной записи тут не годится —
    /// щитовой фасад те же зазоры передавал, и «поле живое» было правдой ровно
    /// наполовину. Поэтому спрашиваем не про запись, а про ПОЛЕ, и спрашиваем
    /// подменой: значение меняется на заведомо другое, и если после этого
    /// маршрутизатор зовёт ровно то же самое — поле не доезжает никуда.
    ///
    /// Перебор идёт рефлексией, поэтому новое поле попадает под проверку само;
    /// добавить мёртвое число молча больше нельзя.</summary>
    [Test]
    public void EveryFieldOfACatalogItem_ReachesTheSpawner()
    {
        var items = CatalogItems().ToList();
        var fields = ItemFields();
        var unknownType = new List<string>();
        var dead = new List<string>();

        foreach (var field in fields)
        {
            bool observed = false;
            foreach (var item in items)
            {
                object? mutated = MutatedValue(field.GetValue(item));
                if (mutated == null) { unknownType.Add(field.Name); break; }
                if (Signature(WithField(item, field, mutated)) != Signature(item))
                {
                    observed = true;
                    break;
                }
            }
            if (!observed && !unknownType.Contains(field.Name)) dead.Add(field.Name);
        }

        Assert.IsEmpty(unknownType,
            "механизм не умеет подменять значение поля такого типа, поэтому проверить его "
            + "не может и молча пропустил бы: допишите тип в MutatedValue. Поля: "
            + string.Join(", ", unknownType));

        Assert.IsEmpty(dead,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: поле каталога сайдбара не доезжает до спауна ни для одной "
            + "записи. Оно выглядит настройкой и ею не является: следующий человек "
            + "поправит число и не поймёт, почему объект не изменился. Так уже было с "
            + "зазорами сборного фасада. "
            + "ЧТО СДЕЛАТЬ: либо протянуть значение через IElementSpawns/ElementSpawner/"
            + "ElementFactory и принять его в SidebarSpawnRouter.Route, либо убрать поле из "
            + "SidebarCatalog.Item и оставить одно место, где величина задаётся — константу "
            + "на самом элементе (FacadeElement.DEFAULT_GAP_MM и подобные). "
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
        var fields = ItemFields();
        Assert.GreaterOrEqual(fields.Length, 8,
            "у записи каталога около десятка полей; меньше — значит рефлексия читает не тот "
            + "тип, и перебор по полям проверяет пустоту: " + string.Join(", ",
                fields.Select(f => f.Name)));

        var drawer = CatalogItems().First(i => i.kind == SidebarItemKind.Drawer);

        Assert.AreEqual(Signature(drawer), Signature(drawer),
            "одна и та же запись обязана давать одну и ту же подпись — иначе сравнение "
            + "показывает разницу всегда, и ни одно поле не может быть признано мёртвым");

        var name = fields.First(f => f.Name == "name");
        Assert.AreNotEqual(Signature(drawer),
            Signature(WithField(drawer, name, MutatedValue(drawer.name)!)),
            "имя доезжает до спауна во ВСЕХ ветках, поэтому его подмена обязана менять "
            + "подпись — если не меняет, сравнение слепо и объявит мёртвыми все поля");
    }

    /// <summary>Сторож сторожа. Запись — это подставной приёмник, и он может
    /// оказаться нем: не тот интерфейс, пустой каталог, маршрутизатор, который
    /// ничего не зовёт. Тогда все проверки выше зеленеют на пустоте.</summary>
    [Test]
    public void TheRecorder_ActuallySeesTheCalls_AndTheCatalogIsNotEmpty()
    {
        var kinds = Enum.GetValues(typeof(SidebarItemKind)).Length;
        Assert.GreaterOrEqual(kinds, 20,
            "видов объектов в сайдбаре два десятка; меньше — значит перечисление читается "
            + "не то");

        Assert.GreaterOrEqual(CatalogItems().Count(), 20,
            "каталог сайдбара предлагает два десятка кнопок; меньше — значит Build() вернул "
            + "не то, и перебор по каталогу ничего не проверяет");

        var board = Route(ItemOfKind(SidebarItemKind.Board));
        Assert.AreEqual(nameof(Recorder.SpawnBoard), board.Method,
            "обычная деталь обязана доезжать до SpawnBoard — если и она молчит, приёмник "
            + "не подключён и все проверки выше зелены ни на чём");

        var sofa = Route(ItemOfKind(SidebarItemKind.Sofa));
        Assert.AreEqual(nameof(Recorder.SpawnSofa), sofa.Method,
            "диван обязан доезжать до своего спауна — положительный контроль на то, что "
            + "разные виды приезжают в РАЗНЫЕ методы, а не все в один");
    }
}

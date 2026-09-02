using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using KitchenDesigner.Tests.Geometry;

/// <summary>«Завели новый объект, а в MCP прописать забыли» — со стороны
/// СОЗДАНИЯ. У McpSpawnerTypeCoverageTests другой угол: он спрашивает, достижим
/// ли каждый КЛАСС элемента из контракта, и ловит тип, забытый везде. Здесь
/// вопрос иной и более ранний: то, что человек уже может завести из сайдбара,
/// обязан уметь завести и агент.
///
/// Сверяются не списки имён типов, а ВЫЗОВЫ ФАБРИКИ — единственное место, через
/// которое элемент вообще появляется на свет. У сайдбара ровно один шлюз
/// (Core/UI/ElementSpawner.cs, за ним UIManager и SidebarUI), у агента ровно
/// один (Core/MCP/ElementSpawners.cs). Новый метод фабрики, подключённый к
/// кнопке сайдбара и забытый в MCP, здесь виден сразу — и виден ДО того, как
/// у нового класса появятся собственные свойства.</summary>
public class McpUiCreationParityTests
{
    private static readonly Regex FactoryCall =
        new Regex(@"ElementFactory(?:\.Instance)?\.(Create\w+)", RegexOptions.Compiled);

    /// <summary>Методы фабрики, которые сайдбар зовёт, а MCP не зовёт — и это
    /// правильно. У каждой записи причина; список проверяется на гниль
    /// <see cref="EveryExemption_StillNamesALiveFactoryMethod"/>, а по трём
    /// первым записям есть ОПЫТ (<see cref="ThePlainCubeTypes_ReallyDoArrive_ThroughCreateElements"/>),
    /// потому что «MCP делает это иначе» без доказательства неотличимо от
    /// «MCP этого не делает».</summary>
    private static readonly (string method, string why)[] NotCalledByMcp =
    {
        ("CreatePart",
         "обычную деталь агент заводит не фабрикой, а ElementSpawners.SpawnPlainCube — "
         + "type:\"board\". Что она действительно приезжает, проверяет опыт ниже"),
        ("CreateWall",
         "стена приезжает тем же SpawnPlainCube с добавленным Wall — type:\"wall\""),
        ("CreateFacade",
         "щитовой фасад приезжает тем же SpawnPlainCube с FacadeElement — type:\"facade\""),
        ("CreatePreset",
         "готовые размеры полки из сайдбара; агенту размеры не нужны — он задаёт их числом "
         + "в create_elements"),
        ("CreateLightSource",
         "светильник заводится только из сайдбара. Решение владельца проекта, записанное в "
         + "McpSpawnerTypeCoverageTests.NotOfferedToAgents — там же оно и снимается"),
    };

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static SortedSet<string> FactoryCallsIn(params string[] parts)
    {
        var path = Path.Combine(RepoPaths.Subdir(parts.Take(parts.Length - 1).ToArray()),
            parts[parts.Length - 1]);
        var text = SourceLines.CodeOnly(File.ReadAllText(path));
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in FactoryCall.Matches(text)) found.Add(m.Groups[1].Value);
        return found;
    }

    private static SortedSet<string> SidebarReaches() =>
        FactoryCallsIn("Assets", "Scripts", "Core", "UI", "ElementSpawner.cs");

    private static SortedSet<string> AgentReaches() =>
        FactoryCallsIn("Assets", "Scripts", "Core", "MCP", "ElementSpawners.cs");

    private static bool Exempt(string method) =>
        NotCalledByMcp.Any(e => e.method == method);

    [Test]
    public void EveryFactoryMethodTheSidebarOffers_IsAlsoReachableThroughCreateElements()
    {
        var missing = SidebarReaches().Where(m => !Exempt(m) && !AgentReaches().Contains(m)).ToList();

        Assert.IsEmpty(missing,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: человек заводит этот объект кнопкой сайдбара, а агент через "
            + "create_elements — никак. Ровно так из MCP выпадали мойка и пуфик: в UI объект "
            + "есть, в реестре спаунеров его нет, и агент про него не узнает ниоткуда. "
            + "ЧТО СДЕЛАТЬ: завести тип в CreateItem.type и ветку в ElementSpawners.ByType — "
            + "либо, если объект намеренно не предлагается агенту, добавить запись с причиной "
            + "в NotCalledByMcp. "
            + McpUiParityRule.CreationAddresses
            + "Не заводится агентом: " + string.Join(", ", missing));
    }

    /// <summary>Сторож сканера. Регулярное выражение по исходнику — самый
    /// хрупкий вид проверки: переименовали фабрику, переехал файл — и скан
    /// находит пустоту, а пустота вычитается из пустоты и даёт зелёное.</summary>
    [Test]
    public void TheScan_FindsBothGateways_AndTheyAreNotTheSameFile()
    {
        var sidebar = SidebarReaches();
        var agent = AgentReaches();

        Assert.GreaterOrEqual(sidebar.Count, 15,
            "сайдбар заводит два десятка видов объектов — если скан нашёл меньше "
            + "пятнадцати вызовов фабрики, он смотрит не в тот файл");
        Assert.GreaterOrEqual(agent.Count, 15,
            "реестр спаунеров MCP немногим меньше — то же самое");

        CollectionAssert.Contains(sidebar, "CreateSink",
            "мойка — тот самый объект, который однажды был в сайдбаре и отсутствовал в MCP; "
            + "она обязана попадать в скан со стороны UI");
        CollectionAssert.Contains(agent, "CreateSink",
            "и со стороны агента, раз дефект давно закрыт");
        CollectionAssert.DoesNotContain(agent, "CreateLightSource",
            "светильник числится исключением; если он появился в MCP — убрать запись из "
            + "NotCalledByMcp, иначе она молча прикроет следующий забытый объект");
    }

    [Test]
    public void EveryExemption_StillNamesALiveFactoryMethod()
    {
        var declared = typeof(ElementFactory)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .ToList();

        foreach (var (method, why) in NotCalledByMcp)
        {
            Assert.IsNotEmpty(why,
                "исключение без причины через полгода не отличить от недосмотра: " + method);
            CollectionAssert.Contains(declared, method,
                "исключение числится за методом фабрики, которого больше нет: " + method);
            Assert.IsFalse(AgentReaches().Contains(method),
                "MCP уже зовёт этот метод, а запись об исключении осталась и теперь прикроет "
                + "следующий забытый объект — убрать из NotCalledByMcp: " + method);
        }
    }

    /// <summary>Три исключения оправданы тем, что «агент делает это другим
    /// путём». Утверждение проверяемое — и проверяется, иначе им можно было бы
    /// прикрыть настоящую дыру.</summary>
    [Test]
    public void ThePlainCubeTypes_ReallyDoArrive_ThroughCreateElements()
    {
        foreach (var (wireType, expected) in new (string, Type)[]
                 {
                     ("board", typeof(KitchenElement)),
                     ("wall", typeof(KitchenElement)),
                     ("facade", typeof(FacadeElement)),
                 })
        {
            Assert.IsTrue(ElementSpawners.CanSpawn(wireType),
                "исключение оправдано типом create_elements, которого реестр не знает: " + wireType);

            var go = ElementSpawners.Spawn(wireType, new CreateItem { name = "Cube_" + wireType }, Vector3.zero);
            _spawned.Add(go);

            var element = go.GetComponent<KitchenElement>();
            Assert.IsNotNull(element,
                "тип создал не элемент, а голый объект сцены: " + wireType);
            Assert.IsTrue(expected.IsInstanceOfType(element),
                "тип создал не тот класс, ради которого метод фабрики числится исключением: "
                + wireType + " дал " + element!.GetType().Name);
        }

        var wall = ElementSpawners.Spawn("wall", new CreateItem { name = "Cube_wall_check" }, Vector3.zero);
        _spawned.Add(wall);
        Assert.IsNotNull(wall.GetComponent<Wall>(),
            "type:\"wall\" обязан давать именно стену, иначе исключение для CreateWall "
            + "прикрывает дыру: агент получал бы обычную деталь под видом стены");
    }
}

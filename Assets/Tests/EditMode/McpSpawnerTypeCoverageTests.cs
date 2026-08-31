using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using KitchenDesigner.Tests.Geometry;

/// <summary>Лечение СЛЕПОТЫ парного теста контракта и реестра спаунеров.
///
/// CreateElements_TypeListInTheContract_MatchesTheSpawnerRegistry сверяет два
/// списка друг с другом, и это правильная проверка — но у неё есть слепое
/// пятно: СОГЛАСОВАННОЕ НЕЗНАНИЕ проходит проверку на согласованность. Мойка
/// выпала из create_elements ровно так. Класс SinkElement был заведён везде —
/// фабрика, дублирование, восстановление сцены, ElementSelector.TypeOf,
/// изометрический снимок, — а контракт и реестр одинаково его не знали, и
/// сверка двух списков оставалась зелёной. Пользователь при этом читал в
/// McpGuideTexts обещание про type:"sink" и получал отказ.
///
/// Поэтому здесь сверка идёт с ТРЕТЬИМ источником, который в сговоре не
/// участвует: <see cref="ElementTypeCatalog"/> читает объявления классов в
/// исходниках и не знает ни про контракт, ни про реестр.
///
/// И сверка идёт не по ИМЕНАМ, а по РЕЗУЛЬТАТУ: каждый объявленный контрактом
/// тип действительно спавнится, и смотрим, какой класс получился. Имя
/// проволочного типа не выводится из имени класса механически (radius_table,
/// assembled_facade, два типа drawer на один DrawerElement), поэтому любое
/// отображение имён было бы ЧЕТВЁРТЫМ списком, который тоже разъедется.
/// Спавн — не список, а факт.</summary>
public class McpSpawnerTypeCoverageTests
{
    /// <summary>Классы, которые агент создавать НЕ должен, с причиной у
    /// каждого. Долг, а не послабление: закрытая запись обязана из списка
    /// исчезнуть — это проверяет
    /// <see cref="EveryExemptType_ExplainsWhy_AndIsStillUnreachable"/>.</summary>
    private static readonly (string type, string why)[] NotOfferedToAgents =
    {
        ("LightSourceElement",
         "светильник заводится только из сайдбара; заводить ли его через MCP — решение о "
         + "продукте, а не пропущенная строка. Решено владельцем проекта, не тестом"),
    };

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static bool IsExempt(string type)
    {
        foreach (var (name, _) in NotOfferedToAgents)
            if (name == type) return true;
        return false;
    }

    /// <summary>Что на самом деле выходит из реестра для каждого типа,
    /// объявленного КОНТРАКТОМ: имя класса компонента.</summary>
    private HashSet<string> ClassesSpawnedByContractTypes()
    {
        var produced = new HashSet<string>();
        int n = 0;

        foreach (var wireType in McpContractEnums.Of(typeof(CreateItem), nameof(CreateItem.type)))
        {
            if (!ElementSpawners.CanSpawn(wireType)) continue;

            var item = new CreateItem { name = "Probe" + n++ };
            var go = ElementSpawners.Spawn(wireType, item, Vector3.zero);
            Assert.IsNotNull(go,
                "объявленный контрактом тип обязан что-то создать, иначе клиент получит "
                + "молчаливый успех вместо элемента: " + wireType);
            _spawned.Add(go);

            var element = go.GetComponent<KitchenElement>();
            Assert.IsNotNull(element,
                "созданное обязано быть элементом, а не голым объектом сцены: " + wireType);
            produced.Add(element.GetType().Name);
        }

        return produced;
    }

    [Test]
    public void EveryElementTypeInTheSources_IsReachable_ThroughTheContractAndTheSpawner()
    {
        var produced = ClassesSpawnedByContractTypes();
        Assert.IsNotEmpty(produced,
            "ни один тип контракта не создался — сторож ослеп, а не позеленел");

        var unreachable = ElementTypeCatalog.FromSources()
            .Where(t => !IsExempt(t) && !produced.Contains(t))
            .ToList();

        Assert.IsEmpty(unreachable,
            "Класс элемента есть в исходниках, но НИ ОДИН объявленный контрактом тип его не "
            + "создаёт — значит для агента его не существует. Сверка контракта с реестром такое "
            + "не видит: оба списка молчат об одном и том же типе одинаково, и равенство "
            + "сходится. Так выпала мойка. Недостижимы: " + string.Join(", ", unreachable));
    }

    /// <summary>Обратная сторона: контракт не должен обещать того, чего реестр
    /// не делает. Проверяем не по списку имён, а по факту.</summary>
    [Test]
    public void EveryTypeTheContractDeclares_IsActuallySpawnable()
    {
        var declared = McpContractEnums.Of(typeof(CreateItem), nameof(CreateItem.type)).ToList();
        Assert.IsNotEmpty(declared, "контракт не объявил ни одного типа — сторож ослеп");

        var empty = declared.Where(t => !ElementSpawners.CanSpawn(t)).ToList();

        Assert.IsEmpty(empty,
            "контракт обещает тип, которого реестр не знает: create_elements ответит отказом на "
            + "то, что сам же предложил в схеме. " + string.Join(", ", empty));
    }

    [Test]
    public void TheThirdSource_SeesTheClasses_AndIsIndependentOfTheContract()
    {
        var types = ElementTypeCatalog.FromSources();

        CollectionAssert.Contains(types, "SinkElement",
            "третий источник обязан видеть мойку — это тот самый класс, который два "
            + "согласованно молчавших списка потеряли");
        CollectionAssert.Contains(types, "AssembledFacadeElement",
            "наследник через промежуточный класс обязан попасть в список");
        Assert.GreaterOrEqual(types.Count, 16,
            "типов элементов в проекте шестнадцать; меньше — значит разбор наследования сломался");

        var declared = McpContractEnums.Of(typeof(CreateItem), nameof(CreateItem.type)).ToList();
        CollectionAssert.DoesNotContain(types, "sink",
            "третий источник обязан быть НЕЗАВИСИМ от контракта: он оперирует именами классов, "
            + "а не проволочными типами — иначе это был бы тот же список в третий раз");
        CollectionAssert.DoesNotContain(declared, "SinkElement",
            "и наоборот: контракт оперирует проволочными именами, а не классами");
    }

    [Test]
    public void EveryExemptType_ExplainsWhy_AndIsStillUnreachable()
    {
        var produced = ClassesSpawnedByContractTypes();
        var types = ElementTypeCatalog.FromSources();

        foreach (var (type, why) in NotOfferedToAgents)
        {
            Assert.IsNotEmpty(why,
                "исключение без причины через полгода не отличить от недосмотра: " + type);
            CollectionAssert.Contains(types, type,
                "исключение числится за типом, которого больше нет: " + type);
            Assert.IsFalse(produced.Contains(type),
                "тип уже создаётся через MCP, а запись об исключении осталась и теперь молча "
                + "прикроет следующий забытый тип — убрать из NotOfferedToAgents: " + type);
        }
    }

    /// <summary>Список типов живёт в описании инструмента ЕЩЁ РАЗ, прозой:
    /// «type (board | wall | … | dishwasher)». Схему читает клиентская
    /// библиотека, а прозу — агент, и расходятся они молча. Это ровно та форма
    /// дефекта, с которой началась история мойки: McpGuideTexts обещал
    /// type:"sink", а создать её было нечем.</summary>
    [Test]
    public void TheToolDescription_ListsTheSameTypes_AsTheSchema()
    {
        var tool = McpToolRegistry.Tools.First(t => t.Name == "create_elements");
        var listed = Regex.Match(tool.Description, @"type \(([^)]+), default board");
        Assert.IsTrue(listed.Success,
            "в описании create_elements больше нет прозаического перечня типов — если он убран "
            + "намеренно, убрать и этот тест, но не оставлять его зелёным ни на чём");

        var fromProse = listed.Groups[1].Value.Split(new[] { '|' })
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var declared = McpContractEnums.Of(typeof(CreateItem), nameof(CreateItem.type)).ToList();

        CollectionAssert.AreEquivalent(declared, fromProse,
            "проза описания и схема обещают РАЗНЫЕ списки типов: схему читает клиентская "
            + "библиотека, прозу — агент. Тип, забытый в прозе, агент не попробует; тип, "
            + "обещанный прозой сверх схемы, будет отвергнут валидацией. "
            + "только в схеме: [" + string.Join(", ", declared.Except(fromProse)) + "]; "
            + "только в прозе: [" + string.Join(", ", fromProse.Except(declared)) + "]");
    }

    /// <summary>Регрессия на найденный дефект: McpGuideTexts обещал
    /// type:"sink" среди врезной техники, а ElementSpawners мойку не знал.</summary>
    [Test]
    public void CreateElements_Sink_ProducesASink_NotAPlainBoard()
    {
        var go = ElementSpawners.Spawn("sink", new CreateItem { name = "Mojka" }, Vector3.zero);
        _spawned.Add(go);

        Assert.IsNotNull(go.GetComponent<SinkElement>(),
            "тип, которого нет в реестре, уходил в SpawnPlainCube и возвращал обычную деталь — "
            + "молчаливый успех вместо мойки");
    }
}

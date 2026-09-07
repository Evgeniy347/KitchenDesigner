using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>
/// Общая база для EditMode-тестов MCP-обработчика. Снимает три копии, найденные
/// аудитом 2026-09-07 в 14-19 файлах: построение <see cref="McpRequest"/>
/// (MakeReq), сам <see cref="_handler"/> и утилизацию заспавненных GameObject
/// вместе с <see cref="PartRegistry"/>.
///
/// NUnit вызывает [SetUp] от БАЗЫ к НАСЛЕДНИКУ и [TearDown] от НАСЛЕДНИКА к
/// БАЗЕ — поэтому наследник, которому нужен ещё один сброс (GroupManager,
/// CommandStack, ProjectRooms и т.п.), просто объявляет СВОЙ [SetUp]/[TearDown]
/// с этой добавкой, а не переопределяет общий метод. Это сознательный выбор:
/// объединение в ОДИН список Clear() расширило бы или сузило то, что чистил
/// конкретный тест, и тесты потекли бы друг в друга — см. agents/TEST-DESIGN.md
/// про SnapIntegrationTests, падавший только в полном PlayMode-прогоне именно
/// по этой причине. Здесь вынесено только то, что было БУКВАЛЬНО идентично во
/// всех копиях.
/// </summary>
public abstract class McpTestFixture
{
    protected readonly List<GameObject> _spawned = new List<GameObject>();
    protected McpCommandHandler? _handler;

    [SetUp]
    public void McpFixtureSetUp()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
    }

    [TearDown]
    public void McpFixtureTearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    protected static McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(JsonConvert.SerializeObject(data))
    };

    /// <summary>Голая деталь на выбранной позиции, зарегистрированная в
    /// PartRegistry и добавленная в <see cref="_spawned"/> — тело, повторённое
    /// под именами MakeElement/MakeBoard/Make в шести файлах до этого сведения.</summary>
    protected KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos = default)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }
}

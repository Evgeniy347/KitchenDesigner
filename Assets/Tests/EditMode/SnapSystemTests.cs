using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SnapSystemTests
{
    private KitchenElement? _elementA;
    private KitchenElement? _elementB;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 1;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;

        _elementA = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        _elementB = CreateElement("B", new Vector3Int(800, 400, 18), Vector3.zero);
    }

    [TearDown]
    public void Teardown()
    {
        if (_elementA != null) Object.DestroyImmediate(_elementA!.gameObject);
        if (_elementB != null) Object.DestroyImmediate(_elementB!.gameObject);
    }

    [Test]
    public void TrySnap_30mmGap_Snapped()
    {
        _elementA!.transform.position = Vector3.zero;
        _elementB!.transform.position = new Vector3(0.83f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB!.transform.position);

        Assert.IsTrue(result.snapped, "Should snap when gap is 30mm < threshold 50mm");
        Assert.AreEqual(0.800f, result.position.x, 0.001f);
    }

    [Test]
    public void TrySnap_60mmGap_NotSnapped()
    {
        _elementA!.transform.position = Vector3.zero;
        _elementB!.transform.position = new Vector3(0.86f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB!.transform.position);

        Assert.IsFalse(result.snapped, "Should NOT snap when gap is 60mm > threshold 50mm");
    }

    [Test]
    public void TrySnap_ElementAboveFloor_SnapsToFloor()
    {
        // Пол: верхняя грань на y=0.009. деталь 400мм высотой висит над полом
        // (низ на y=0.05, зазор 41мм < порога 50мм) → снэп опускает центр на
        // 0.009 + 0.2 = 0.209.
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        _elementB!.transform.position = new Vector3(0, 0.25f, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { floor }, _elementB!.transform.position);

        Assert.IsTrue(result.snapped, "Element above floor should snap down to it");
        Assert.AreEqual(0.209f, result.position.y, 0.001f);

        Object.DestroyImmediate(floor.gameObject);
    }

    [Test]
    public void TrySnap_IntersectingElements_SnapsApart()
    {
        _elementA!.transform.position = Vector3.zero;
        _elementB!.transform.position = Vector3.zero;

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB!.transform.position);

        // Полностью совпадающие детали: грани ±Z в зазоре 18 мм → снэп разведёт по Z.
        Assert.IsTrue(result.snapped, "снэп разведёт пересекающиеся детали по Z");
        Assert.AreEqual(0f, result.position.x, 0.001f);
        Assert.AreEqual(0f, result.position.y, 0.001f);
        Assert.AreEqual(-0.018f, result.position.z, 0.001f, "Z — встык");
    }

    [Test]
    public void TrySnap_SnapDisabled_NotSnapped()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        _elementA!.transform.position = Vector3.zero;
        _elementB!.transform.position = new Vector3(0.83f, 0, 0);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA }, _elementB!.transform.position);

        Assert.IsFalse(result.snapped, "Should NOT snap when snap is disabled");
    }

    [Test]
    public void TrySnap_SmallBoardNearEdge_AlignsEdgesNotCenter()
    {
        // Большая деталь A (800 шир) в плоскости XY, тонкая по Z. Маленькая B (400 шир)
        // подносится к передней грани A около ЛЕВОГО края → должны совпасть левые кромки,
        // а НЕ центры (это и была жалоба на «прилипание по середине»).
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(400, 400, 18), Vector3.zero);

        var result = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(-0.18f, 0f, 0.02f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(-0.20f, result.position.x, 0.001f, "левые кромки должны совпасть");
        Assert.AreEqual(0.018f, result.position.z, 0.001f, "плоскости заподлицо");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void TrySnap_SmallBoardNearCenter_AlignsCenters()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(400, 400, 18), Vector3.zero);

        var result = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.0f, 0f, 0.02f));

        Assert.IsTrue(result.snapped);
        Assert.AreEqual(0.0f, result.position.x, 0.001f, "у центра — центры совпадают");
        Assert.AreEqual(0.018f, result.position.z, 0.001f);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void VerboseLog_Off_KeepsTheSnapSilent_On_ExplainsTheChoice()
    {
        _elementA!.transform.position = Vector3.zero;
        var others = new List<KitchenElement> { _elementA };
        var pos = new Vector3(0.83f, 0f, 0f);

        bool savedVerbose = SnapSystem.VerboseLog;
        int logs = 0;
        Application.LogCallback counter = (condition, stackTrace, type) => { if (type == LogType.Log) logs++; };
        Application.logMessageReceived += counter;
        try
        {
            SnapSystem.VerboseLog = false;
            var quiet = SnapSystem.TrySnap(_elementB!, others, pos);
            Assume.That(quiet.snapped, Is.True, "проба должна прилипать, иначе логировать нечего");
            Assert.AreEqual(0, logs,
                "разбор выбора снэпа — отладочный инструмент, и по умолчанию он молчит: "
                + "снэп зовётся на каждый кадр перетаскивания и залил бы консоль");

            SnapSystem.VerboseLog = true;
            SnapSystem.TrySnap(_elementB!, others, pos);
            Assert.Greater(logs, 0, "включённый флаг обязан объяснить выбор");
        }
        finally
        {
            Application.logMessageReceived -= counter;
            SnapSystem.VerboseLog = savedVerbose;
        }
    }

    [Test]
    public void PosedElement_IsAReferenceType_SoTheCoreDoesNotBoxItOnEveryCall()
    {
        Assert.IsFalse(typeof(SnapSystem.PosedElement).IsValueType,
            "ядро держит источник геометрии по интерфейсу IPosedGeometry: структура "
            + "упаковывалась бы при каждой передаче, то есть на каждый вызов снэпа, "
            + "а класс упаковывается ровно ноль раз");
        Assert.IsTrue(typeof(IPosedGeometry).IsAssignableFrom(typeof(SnapSystem.PosedElement)));
    }

    [Test]
    public void TrySnap_DoesNotMoveAnythingInTheScene()
    {
        _elementA!.transform.position = Vector3.zero;
        _elementB!.transform.position = new Vector3(0.83f, 0f, 0f);
        var beforeA = _elementA!.transform.position;
        var beforeB = _elementB!.transform.position;

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA },
            new Vector3(2f, 2f, 2f));

        Assert.IsFalse(result.snapped, "проба взята заведомо далеко");
        Assert.AreEqual(beforeB, _elementB!.transform.position,
            "геометрия примеряемой позиции считается аналитически: раньше деталь для "
            + "примерки двигали записью в transform.position, и прерванный расчёт "
            + "оставлял её в чужом месте");
        Assert.AreEqual(beforeA, _elementA!.transform.position);
    }

    [Test]
    public void TrySnap_OverPrebuiltSnapshots_AgreesWithTheElementList()
    {
        _elementA!.transform.position = Vector3.zero;
        var others = new List<KitchenElement> { _elementA };
        var snapshots = others.ToGeometry();

        foreach (var x in new[] { 0.83f, 0.86f, 0.805f })
        {
            var pos = new Vector3(x, 0f, 0f);
            var byElements = SnapSystem.TrySnap(_elementB!, others, pos);
            var bySnapshots = SnapSystem.TrySnap(_elementB!, snapshots, pos);

            Assert.AreEqual(byElements.snapped, bySnapshots.snapped,
                $"x={x}: горячий путь по готовым снимкам обязан давать тот же ответ, "
                + "что и пересборка снимков на каждый вызов");
            Assert.AreEqual(byElements.position, bySnapshots.position, $"x={x}");
        }
    }

    [Test]
    public void TrySnap_InactiveElement_DoesNotSnap()
    {
        _elementA!.transform.position = Vector3.zero;
        _elementB!.gameObject.SetActive(false);

        var result = SnapSystem.TrySnap(_elementB, new List<KitchenElement> { _elementA },
            new Vector3(0.83f, 0f, 0f));

        Assert.IsFalse(result.snapped,
            "выключенная деталь не участвует в сцене — прилипать ей некуда");
        _elementB!.gameObject.SetActive(true);
    }

    private static KitchenElement CreateElement(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }
}

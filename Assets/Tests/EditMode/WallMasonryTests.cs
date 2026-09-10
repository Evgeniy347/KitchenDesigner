using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Технология кладки, шов и запас — три свойства стены, которые
/// решают, чем стена попадёт в ведомость. Они живут на компоненте
/// <see cref="Wall"/>, а не на <see cref="KitchenElement"/>, и отмена у них
/// поэтому РУЧНАЯ (<see cref="SetWallMasonryCommand"/>): сканер
/// <c>UndoableProperties</c> смотрит только на тип самого элемента, и
/// <c>[Undoable]</c> на соседнем компоненте не читает никто — урок, оплаченный
/// на <c>Wall.LoadBearing</c> в этой же сессии (см. <see cref="WallLoadBearingTests"/>).
///
/// Второе решение, зафиксированное здесь: новая стена берёт значения из вкладки
/// «Строительство», а не из константы. Иначе настройка проекта была бы украшением.</summary>
public class WallMasonryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private MasonryTechnology _savedMasonry;
    private int _savedJoint;
    private int _savedWaste;

    [SetUp]
    public void Setup()
    {
        var s = KitchenSettings.Instance;
        _savedMasonry = s.ConstructionMasonry;
        _savedJoint = s.ConstructionJointMm;
        _savedWaste = s.ConstructionWastePct;
    }

    private GameObject MakeWall(string name = "Стена")
    {
        var go = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 250), name, Vector3.zero);
        _spawned.Add(go);
        return go;
    }

    [TearDown]
    public void Teardown()
    {
        var s = KitchenSettings.Instance;
        s.ConstructionMasonry = _savedMasonry;
        s.ConstructionJointMm = _savedJoint;
        s.ConstructionWastePct = _savedWaste;

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    [Test]
    public void NewWall_TakesTheProjectDefaults_FromTheConstructionTab()
    {
        var s = KitchenSettings.Instance;
        s.ConstructionMasonry = MasonryTechnology.AeratedBlock;
        s.ConstructionJointMm = 3;
        s.ConstructionWastePct = 12;

        var wall = MakeWall().GetComponent<Wall>();

        Assert.AreEqual(MasonryTechnology.AeratedBlock, wall.Masonry,
            "стена обязана рождаться с технологией из настроек проекта, иначе вкладка "
            + "«Строительство» ничего не решает");
        Assert.AreEqual(3, wall.JointMm);
        Assert.AreEqual(12, wall.WastePct);
    }

    [Test]
    public void NewWall_OutOfTheBox_IsBrickWithATenMillimetreJointAndFivePercentSpare()
    {
        KitchenSettings.Instance.ResetConstruction();

        var wall = MakeWall().GetComponent<Wall>();

        Assert.AreEqual(MasonryTechnology.BrickSingle, wall.Masonry,
            "дефолт продукта — кирпич 250×120×65");
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_JOINT_DEFAULT_MM, wall.JointMm);
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_WASTE_DEFAULT_PCT, wall.WastePct);
    }

    [Test]
    public void Wall_RefusesAJointOrASpareOutsideItsRange()
    {
        var wall = MakeWall().GetComponent<Wall>();

        wall.JointMm = -5;
        Assert.AreEqual(0, wall.JointMm, "отрицательного шва не бывает");

        wall.JointMm = KitchenSettings.CONSTRUCTION_JOINT_MAX_MM + 100;
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_JOINT_MAX_MM, wall.JointMm,
            "шов толще предела — это уже не шов, а слой кладки");

        wall.WastePct = -1;
        Assert.AreEqual(0, wall.WastePct);

        wall.WastePct = 1000;
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_WASTE_MAX_PCT, wall.WastePct);
    }

    [Test]
    public void SetWallMasonryCommand_Undo_RestoresAllThreeValuesAtOnce()
    {
        var wall = MakeWall().GetComponent<Wall>();
        var before = SetWallMasonryCommand.Snapshot(wall);

        CommandStack.Execute(new SetWallMasonryCommand(wall, before,
            new WallMasonry(MasonryTechnology.Timber, 0, 20)));

        Assert.AreEqual(MasonryTechnology.Timber, wall.Masonry);
        Assert.AreEqual(0, wall.JointMm);
        Assert.AreEqual(20, wall.WastePct);

        CommandStack.Undo();
        Assert.AreEqual(before.Technology, wall.Masonry, "отмена возвращает технологию");
        Assert.AreEqual(before.JointMm, wall.JointMm, "и шов");
        Assert.AreEqual(before.WastePct, wall.WastePct, "и запас — одним шагом, а не тремя");

        CommandStack.Redo();
        Assert.AreEqual(MasonryTechnology.Timber, wall.Masonry);
        Assert.AreEqual(0, wall.JointMm);
        Assert.AreEqual(20, wall.WastePct);
    }

    [Test]
    public void SaveLoadRoundTrip_PreservesMasonryJointAndSpare()
    {
        var wall = MakeWall("КаркаснаяСтена").GetComponent<Wall>();
        wall.Masonry = MasonryTechnology.Frame;
        wall.JointMm = 7;
        wall.WastePct = 15;
        var el = wall.GetComponent<KitchenElement>();
        string name = el.PartName;
        PartRegistry.Register(el);

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var restored = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!)
            .Select(g => g.GetComponent<Wall>())
            .Where(w => w != null)
            .ToList();
        foreach (var go in restored.Select(w => w!.gameObject)) _spawned.Add(go);

        var back = restored.First(w => w!.GetComponent<KitchenElement>().PartName == name);
        Assert.AreEqual(MasonryTechnology.Frame, back!.Masonry);
        Assert.AreEqual(7, back.JointMm);
        Assert.AreEqual(15, back.WastePct);
    }

    /// <summary>Старый проект сохранён до появления этих трёх полей. JsonUtility
    /// построит объект и без них, поэтому единственная защита — инициализаторы в
    /// <see cref="ElementData"/>: стена обязана открыться кирпичной со швом 10 и
    /// запасом 5, а не с нулями.</summary>
    [Test]
    public void OldProjectJson_WithoutTheMasonryFields_OpensWithTheProductDefaults()
    {
        var wall = MakeWall().GetComponent<Wall>();
        wall.Masonry = MasonryTechnology.Timber;
        wall.JointMm = 0;
        wall.WastePct = 0;
        PartRegistry.Register(wall.GetComponent<KitchenElement>());

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));
        Assert.IsTrue(json.Contains("wallMasonry"),
            "проверка ничего не доказывает, если поля и так нет в текущем формате");

        string oldJson = System.Text.RegularExpressions.Regex.Replace(
            json, "\"wall(Masonry|JointMm|WastePct)\"\\s*:\\s*-?\\d+,?", "");
        Assert.IsFalse(oldJson.Contains("wallMasonry"), "поля вырезаны целиком");

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var data = SaveLoadManager.Deserialize(oldJson);
        Assert.IsNotNull(data, "старый файл обязан десериализоваться");
        var restored = SaveLoadManager.RestoreScene(data!)
            .Select(g => g.GetComponent<Wall>())
            .Where(w => w != null)
            .ToList();
        foreach (var go in restored.Select(w => w!.gameObject)) _spawned.Add(go);

        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual(MasonryTechnology.BrickSingle, restored[0]!.Masonry);
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_JOINT_DEFAULT_MM, restored[0]!.JointMm);
        Assert.AreEqual(KitchenSettings.CONSTRUCTION_WASTE_DEFAULT_PCT, restored[0]!.WastePct);
    }

    [Test]
    public void ADuplicatedWall_CarriesItsOwnMasonry_NotTheProjectDefault()
    {
        KitchenSettings.Instance.ResetConstruction();

        var source = MakeWall("Оригинал");
        var sourceWall = source.GetComponent<Wall>();
        sourceWall.Masonry = MasonryTechnology.BrickThickened;
        sourceWall.JointMm = 8;
        sourceWall.WastePct = 11;
        sourceWall.LoadBearing = false;

        var copy = ElementFactory.Duplicate(source.GetComponent<KitchenElement>());
        _spawned.Add(copy);
        var copiedWall = copy.GetComponent<Wall>();

        Assert.IsNotNull(copiedWall, "копия стены обязана остаться стеной");
        Assert.AreEqual(MasonryTechnology.BrickThickened, copiedWall!.Masonry);
        Assert.AreEqual(8, copiedWall.JointMm);
        Assert.AreEqual(11, copiedWall.WastePct);
        Assert.IsFalse(copiedWall.LoadBearing,
            "и признак несущей копируется вместе с ней — иначе дубликат перегородки "
            + "молча становится несущим");
    }

    /// <summary>Та же причина, что у <c>LoadBearing</c>: <c>[Undoable]</c> на
    /// свойстве соседнего компонента не читает никто, поэтому его здесь нет, а
    /// отмену даёт явная команда. Тест не даёт вернуть украшение обратно.</summary>
    [Test]
    public void TheThreeMasonryProperties_AreNotDecoratedUndoable_AndTheScannerConfirmsWhy()
    {
        foreach (var name in new[] { nameof(Wall.Masonry), nameof(Wall.JointMm), nameof(Wall.WastePct) })
        {
            var prop = typeof(Wall).GetProperty(name);
            Assert.IsNotNull(prop, name + " — свойство на месте");
            Assert.IsNull(prop!.GetCustomAttributes(typeof(UndoableAttribute), true).FirstOrDefault(),
                "[Undoable] на соседнем компоненте инертен: UndoableProperties.For берёт тип "
                + "KitchenElement, а Wall — сосед. Отмену даёт SetWallMasonryCommand: " + name);
        }

        var wallEl = MakeWall().GetComponent<KitchenElement>();
        var scanned = UndoableProperties.For(wallEl.GetType()).Select(p => p.Name).ToList();
        CollectionAssert.DoesNotContain(scanned, nameof(Wall.Masonry),
            "сканер действительно не видит свойств соседа — это и есть причина ручной команды");
    }
}

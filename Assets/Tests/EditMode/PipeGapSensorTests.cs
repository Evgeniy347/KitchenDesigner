using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Сенсор задачи A/B: печатает и проверяет ФАКТИЧЕСКИЙ зазор устье-в-устье
/// между трубой и обоими отводами на реальной сцене пользователя (замороженная копия
/// <c>docs/example.save.json</c>, снятая для этой задачи — сам файл этот тест не
/// трогает, agents/TESTS.md → «NEVER TOUCH IT»), а не на придуманном стенде.
///
/// Диагноз задачи A был: <c>MmGrid.Snap</c>, вызванный СРАЗУ ПОСЛЕ посадки устье в
/// устье при отпускании кнопки (<c>ElementMover.FinishDrag</c>), откатывает только
/// что севшую дробную деталь на целый миллиметр и рвёт стык. Этот тест не пересказывает
/// диагноз, а меряет ЧИСЛО: зазор либо укладывается в допуск стыка
/// (<c>PipeJoint.JoinToleranceMm</c> = <c>Tolerance.ContactMm</c> = 0.5 мм), либо нет.
/// Сцена статична — тест не перетаскивает деталь заново, а читает то, что уже лежит в
/// файле; правка ElementMover.FinishDrag на этот тест не влияет и не обязана влиять,
/// это отдельная проверка того же факта на живых координатах пользователя.</summary>
public class PipeGapSensorTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);

        _guard = ProjectLoadStateGuard.Capture();

        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;

        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
    }

    private void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    private static float MinMouthGapMm(KitchenElement a, KitchenElement b)
    {
        var pa = (ISnapPorts)a;
        var pb = (ISnapPorts)b;
        float best = float.MaxValue;
        for (int i = 0; i < pa.SnapPortCount; i++)
        {
            var mouthA = pa.SnapPortAt(i, a.transform.position);
            for (int j = 0; j < pb.SnapPortCount; j++)
            {
                var mouthB = pb.SnapPortAt(j, b.transform.position);
                float gapMm = Vector3.Distance(mouthA.Position, mouthB.Position)
                    / AppConstants.MM_TO_UNITS;
                if (gapMm < best) best = gapMm;
            }
        }
        return best;
    }

    private static void AssertGapWithinTolerance(List<KitchenElement> elements,
        string pipeName, string fittingName)
    {
        var pipe = elements.FirstOrDefault(e => e.PartName == pipeName);
        var fitting = elements.FirstOrDefault(e => e.PartName == fittingName);
        Assert.IsNotNull(pipe, $"в сцене обязана быть труба {pipeName}");
        Assert.IsNotNull(fitting, $"в сцене обязан быть фитинг {fittingName}");
        Assert.IsInstanceOf<ISnapPorts>(pipe, $"{pipeName} обязана нести устья");
        Assert.IsInstanceOf<ISnapPorts>(fitting, $"{fittingName} обязан нести устья");

        float gapMm = MinMouthGapMm(pipe!, fitting!);
        TestContext.WriteLine($"{pipeName} <-> {fittingName}: зазор устье-в-устье = "
            + $"{gapMm:F4} мм (допуск PipeJoint.JoinToleranceMm = "
            + $"{PipeJoint.JoinToleranceMm} мм)");

        Assert.LessOrEqual(gapMm, PipeJoint.JoinToleranceMm,
            $"зазор {gapMm:F4} мм между {pipeName} и {fittingName} больше допуска стыка "
            + $"({PipeJoint.JoinToleranceMm} мм) — PipeJoint.Connects не признает их "
            + "соединёнными, и схема концов трубы покажет «нет» на этом торце");
    }

    [Test]
    public void MouthToMouthGap_TrubaToOtvod92_IsWithinTheProjectJoinTolerance()
    {
        var elements = RestoreScene();
        AssertGapWithinTolerance(elements, "Truba", "Otvod_92");
    }

    [Test]
    public void MouthToMouthGap_TrubaToOtvod91_IsWithinTheProjectJoinTolerance()
    {
        var elements = RestoreScene();
        AssertGapWithinTolerance(elements, "Truba", "Otvod_91");
    }
}

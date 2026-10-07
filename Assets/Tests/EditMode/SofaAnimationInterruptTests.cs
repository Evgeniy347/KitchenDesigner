using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Раскладывание дивана идёт во времени (<c>Advance</c> из <c>Update</c>), и
/// пользователь вправе вмешаться посреди движения: сохранить проект, потянуть размер,
/// удалить диван и вернуть его отменой. Раскладывание в покое проверено в
/// <c>SofaUnfoldElementTests</c>; здесь — только ПРЕРВАННОЕ движение, где расходятся
/// «куда едем» (этап, <c>UnfoldStage</c>) и «где сейчас» (прогресс).
///
/// Время ведёт сам тест через <c>Advance(секунды)</c> — тем же методом, что и <c>Update</c>.</summary>
public class SofaAnimationInterruptTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float Eps = 1e-4f;
    private const float AngleEps = 1e-2f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        CommandStack.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private SofaElement Sofa()
    {
        var go = ElementFactory.CreateSofa(
            new Vector3Int(SofaLayout.DefaultWidthMM, SofaLayout.OverallHeightMM, SofaLayout.DefaultDepthMM),
            SofaLayout.DefaultCornerRadiusMM, SofaLayout.DefaultSeatHeightMM, "Диван-прерван", Vector3.zero);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>();
        Assert.IsNotNull(sofa, "фабрика обязана вернуть SofaElement");
        return sofa!;
    }

    private static Transform Front(SofaElement sofa) => sofa.transform.Find(SofaLayout.FrontGroupName)!;

    private static Transform Hinge(SofaElement sofa) => sofa.transform.Find(SofaLayout.HingeGroupName)!;

    private static float HingeAngle(SofaElement sofa)
        => Quaternion.Angle(Quaternion.identity, Hinge(sofa).localRotation);

    private static SofaPose PoseAtProgress(SofaElement sofa, float progress)
        => SofaUnfold.PoseAt(progress, sofa.DimensionsMM.z, sofa.SeatHeightMM);

    private SofaElement Restored(SofaElement source)
    {
        var data = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(ElementCapture.FromElement(source)));
        var go = ElementRestorers.Restore(ElementFactory.Instance, data);
        _spawned.Add(go);
        return go.GetComponent<SofaElement>()!;
    }

    [TestCase(SofaStage.Folded, SofaStage.Bed, 0.5f)]
    [TestCase(SofaStage.Folded, SofaStage.Bed, 1.5f)]
    [TestCase(SofaStage.Folded, SofaStage.Extended, 0.5f)]
    [TestCase(SofaStage.Bed, SofaStage.Folded, 0.5f)]
    [TestCase(SofaStage.Bed, SofaStage.Folded, 1.5f)]
    [TestCase(SofaStage.Bed, SofaStage.Extended, 0.5f)]
    public void Save_MidAnimation_WritesTheTargetStage(SofaStage from, SofaStage target, float secondsSpent)
    {
        var sofa = Sofa();
        sofa.SnapToStage(from);
        sofa.GoToStage(target);
        sofa.Advance(secondsSpent);
        Assume.That(sofa.Advance(0f), Is.True, "диван обязан ещё ехать, иначе это не прерванное движение");

        var data = ElementCapture.FromElement(sofa);

        Assert.AreEqual((int)target, data.sofaUnfoldStage,
            "в проект пишется ЦЕЛЬ движения (" + from + " → " + target + ", прошло " + secondsSpent
            + " с), а не округлённый прогресс: загруженный проект не должен ни проигрывать движение "
            + "заново, ни застывать на полпути");

        var loaded = Restored(sofa);
        var pose = PoseAtProgress(loaded, (float)target);
        Assert.AreEqual(target, loaded.UnfoldStage, "этап после загрузки — цель");
        Assert.IsFalse(loaded.Advance(0f), "загруженный диван стоит: анимации не осталось");
        Assert.AreEqual(pose.SeatSlideMM * Mm, Front(loaded).localPosition.z, Eps,
            "сиденье сразу в позе цели");
        Assert.AreEqual(pose.BackrestAngleDeg, HingeAngle(loaded), AngleEps, "и спинка тоже");
    }

    [Test]
    public void Resize_MidAnimation_KeepsProgress_AndContinues()
    {
        var sofa = Sofa();
        sofa.GoToStage(SofaStage.Bed);
        sofa.Advance(1.5f);
        float angleBefore = HingeAngle(sofa);
        Assume.That(angleBefore, Is.InRange(1f, 89f), "спинка обязана быть посреди поворота");

        sofa.DimensionsMM = new Vector3Int(SofaLayout.DefaultWidthMM, SofaLayout.OverallHeightMM, 1200);

        var mid = PoseAtProgress(sofa, 1.5f);
        Assert.AreEqual(mid.BackrestAngleDeg, HingeAngle(sofa), AngleEps,
            "пересборка не сбросила и не дожала поворот: спинка на том же месте пути (45 из 90), "
            + "иначе правка размера на ходу скакала бы в начало или в конец движения");
        Assert.AreEqual(mid.SeatSlideMM * Mm, Front(sofa).localPosition.z, Eps,
            "сиденье в позе того же прогресса, но с ходом, пересчитанным под новую глубину");
        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage, "цель движения не сбилась");

        Assert.IsTrue(sofa.Advance(0.1f), "движение продолжается, а не закончилось при пересборке");
        Assert.IsFalse(sofa.Advance(10f), "и доходит до конца");
        var end = PoseAtProgress(sofa, 2f);
        Assert.AreEqual(90f, HingeAngle(sofa), AngleEps, "спинка легла");
        Assert.AreEqual(end.SeatSlideMM * Mm, Front(sofa).localPosition.z, Eps,
            "сиденье стоит в зазоре кровати для НОВОЙ глубины");
    }

    [Test]
    public void Delete_MidAnimation_ThenUndo_RestoresAtTheTarget()
    {
        var sofa = Sofa();
        sofa.GoToStage(SofaStage.Bed);
        sofa.Advance(1.2f);
        var go = sofa.gameObject;

        CommandStack.Execute(new DeleteCommand(go));
        Assume.That(go.activeSelf, Is.False, "удаление гасит объект: Update у него не идёт");
        CommandStack.Undo();

        Assert.IsTrue(go.activeSelf, "отмена вернула диван в сцену");
        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage,
            "цель движения пережила удаление и отмену: диван не сложился и не лёг сам");
        Assert.AreEqual(PoseAtProgress(sofa, 1.2f).BackrestAngleDeg, HingeAngle(sofa), AngleEps,
            "а поза осталась ровно там, где его застало удаление, — без прыжка");

        sofa.Advance(10f);

        Assert.AreEqual(90f, HingeAngle(sofa), AngleEps, "после отмены диван доехал до цели");
        Assert.AreEqual(PoseAtProgress(sofa, 2f).SeatSlideMM * Mm, Front(sofa).localPosition.z, Eps,
            "и сиденье стоит в позе кровати");
    }
}

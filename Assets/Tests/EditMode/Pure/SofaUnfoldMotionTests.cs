using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Конечный автомат раскладывания дивана: ОДИН параметр прогресса от 0
/// до 2, где целая часть — этап. Именно поэтому два этапа строго последовательны
/// сами собой: спинка зависит от прогресса выше единицы, сиденье — от прогресса
/// до единицы, и перепутать порядок нечем.</summary>
public class SofaUnfoldMotionTests
{
    private const float Eps = 1e-5f;

    [Test]
    public void New_StartsFolded_AndIdle()
    {
        var motion = new SofaUnfoldMotion();

        Assert.AreEqual(0f, motion.Progress, Eps, "диван стартует сложенным");
        Assert.AreEqual(SofaStage.Folded, motion.Target, "и цель — сложенный");
        Assert.IsFalse(motion.IsMoving, "двигаться нечему");
    }

    [Test]
    public void GoTo_Bed_RunsBothStagesInOneSweep_TakingOneSecondEach()
    {
        var motion = new SofaUnfoldMotion();
        motion.GoTo(SofaStage.Bed);

        Assert.IsTrue(motion.IsMoving, "после GoTo диван в движении");
        Assert.AreEqual(2f * SofaUnfold.SecondsPerStage, motion.RemainingSeconds, Eps,
            "от сложенного до кровати — два этапа");

        Assert.IsTrue(motion.Advance(1f), "через секунду ещё едет");
        Assert.AreEqual(1f, motion.Progress, Eps, "и стоит на границе этапов");

        Assert.IsFalse(motion.Advance(1f), "ещё через секунду приехал");
        Assert.AreEqual(2f, motion.Progress, Eps, "и стоит ровно на кровати");
    }

    [Test]
    public void Advance_NeverOvershootsTheTarget_EvenWithAHugeFrame()
    {
        var motion = new SofaUnfoldMotion();
        motion.GoTo(SofaStage.Extended);

        motion.Advance(100f);

        Assert.AreEqual(1f, motion.Progress, Eps,
            "лаг в сто секунд не должен унести прогресс за цель: диван остановился бы "
            + "в несуществующей позе");
        Assert.IsFalse(motion.IsMoving, "и стоит");
    }

    [Test]
    public void GoTo_Folded_FromTheBed_ReversesThroughTheStageBoundary()
    {
        var motion = new SofaUnfoldMotion();
        motion.Snap(SofaStage.Bed);
        motion.GoTo(SofaStage.Folded);

        motion.Advance(1f);

        Assert.AreEqual(1f, motion.Progress, Eps,
            "складывание идёт тем же путём назад: сначала поднимается спинка (прогресс "
            + "2→1), и только потом уезжает сиденье (1→0)");
    }

    [Test]
    public void GoTo_RetargetedMidWay_TurnsAroundFromWhereItStands()
    {
        var motion = new SofaUnfoldMotion();
        motion.GoTo(SofaStage.Bed);
        motion.Advance(0.7f);

        motion.GoTo(SofaStage.Folded);

        Assert.AreEqual(0.7f, motion.Progress, Eps, "переназначение цели не прыгает");
        Assert.AreEqual(0.7f * SofaUnfold.SecondsPerStage, motion.RemainingSeconds, Eps,
            "и обратно диван едет оставшиеся 0,7 этапа, а не весь путь");
    }

    [Test]
    public void Snap_PutsTheSofaStraightIntoAStage_WithoutAnimation()
    {
        var motion = new SofaUnfoldMotion();

        motion.Snap(SofaStage.Bed);

        Assert.AreEqual(2f, motion.Progress, Eps, "загрузка сохранения ставит кровать сразу");
        Assert.AreEqual(SofaStage.Bed, motion.Target, "цель совпадает с позой");
        Assert.IsFalse(motion.IsMoving, "и анимации после этого нет");
    }

    [Test]
    public void Advance_WithANegativeStep_DoesNotRunTheSofaBackwards()
    {
        var motion = new SofaUnfoldMotion();
        motion.GoTo(SofaStage.Bed);
        motion.Advance(0.5f);

        motion.Advance(-3f);

        Assert.AreEqual(0.5f, motion.Progress, Eps,
            "отрицательное время кадра (скачок часов) не должно крутить анимацию вспять");
    }
}

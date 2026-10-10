#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Прогресс загрузки в консоль: не на каждый кусок, а раз в десять процентов и не чаще раза в
/// несколько секунд. Границы проверены вплотную: 0, 9, 10, 100 процентов и край окна по времени.
/// </summary>
public class DownloadProgressThrottleTests
{
    private const long Total = 1000;
    private const float Window = DownloadProgressThrottle.MinSecondsBetweenReports;

    private static DownloadProgressThrottle Fresh(float startedAt = 0f) => new DownloadProgressThrottle(startedAt);

    [Test]
    public void TheConstants_AreTenPercentAndAFewSeconds()
    {
        Assert.AreEqual(10, DownloadProgressThrottle.PercentStep);
        Assert.Greater(DownloadProgressThrottle.MinSecondsBetweenReports, 0f);
    }

    [Test]
    public void ZeroPercent_IsNeverReported()
    {
        Assert.IsFalse(Fresh().ShouldReport(0, Total, 100f));
    }

    [Test]
    public void NinePercent_IsNotReported_EvenAfterALongTime()
    {
        Assert.IsFalse(Fresh().ShouldReport(99, Total, 100f));
    }

    [Test]
    public void TenPercent_IsReported_WhenTheWindowHasPassed()
    {
        Assert.IsTrue(Fresh().ShouldReport(100, Total, Window));
    }

    [Test]
    public void TenPercent_IsHeldBack_JustInsideTheWindow()
    {
        Assert.IsFalse(Fresh().ShouldReport(100, Total, Window - 0.01f));
    }

    [Test]
    public void TheSameStep_IsReportedOnlyOnce()
    {
        var throttle = Fresh();

        Assert.IsTrue(throttle.ShouldReport(100, Total, Window));
        Assert.IsFalse(throttle.ShouldReport(105, Total, Window * 3));
        Assert.IsFalse(throttle.ShouldReport(199, Total, Window * 4));
    }

    [Test]
    public void TheNextStep_IsReported_OnlyAfterAnotherWindow()
    {
        var throttle = Fresh();
        throttle.ShouldReport(100, Total, Window);

        Assert.IsFalse(throttle.ShouldReport(200, Total, Window + 1f));
        Assert.IsTrue(throttle.ShouldReport(200, Total, Window * 2));
    }

    [Test]
    public void AHeldBackStep_IsNotLost_ItIsReportedAtTheNextChunkAfterTheWindow()
    {
        var throttle = Fresh();
        Assert.IsFalse(throttle.ShouldReport(100, Total, 0.5f));

        Assert.IsTrue(throttle.ShouldReport(130, Total, Window + 0.1f),
            "десять процентов пропущены из-за окна по времени — их нужно сказать позже, а не потерять");
    }

    [Test]
    public void AJumpAcrossSeveralSteps_IsReportedOnce()
    {
        var throttle = Fresh();

        Assert.IsTrue(throttle.ShouldReport(450, Total, Window));
        Assert.IsFalse(throttle.ShouldReport(459, Total, Window * 3));
        Assert.IsTrue(throttle.ShouldReport(500, Total, Window * 3));
    }

    [Test]
    public void OneHundredPercent_IsAlwaysReported_IgnoringTheWindow_ButOnlyOnce()
    {
        var throttle = Fresh();
        throttle.ShouldReport(900, Total, Window);

        Assert.IsTrue(throttle.ShouldReport(1000, Total, Window + 0.001f));
        Assert.IsFalse(throttle.ShouldReport(1000, Total, Window * 10));
    }

    [Test]
    public void OneHundredPercent_OnTheVeryFirstCall_IsReported()
    {
        Assert.IsTrue(Fresh().ShouldReport(Total, Total, 0f));
    }

    [Test]
    public void MoreThanTheTotal_CountsAsComplete()
    {
        Assert.IsTrue(Fresh().ShouldReport(Total + 50, Total, 0f));
    }

    [Test]
    public void NinetyNinePercent_IsNotCompletion()
    {
        var throttle = Fresh();
        throttle.ShouldReport(900, Total, Window);

        Assert.IsFalse(throttle.ShouldReport(999, Total, Window * 2),
            "99% ещё в том же десятке, что и 90%; 100% — только когда получено всё");
    }

    [Test]
    public void TheWindow_IsMeasuredFromTheStartOfTheDownload()
    {
        var throttle = Fresh(startedAt: 50f);

        Assert.IsFalse(throttle.ShouldReport(100, Total, 50f + Window - 0.01f));
        Assert.IsTrue(throttle.ShouldReport(100, Total, 50f + Window));
    }

    [Test]
    public void UnknownTotal_IsReportedByTimeOnly()
    {
        var throttle = Fresh();

        Assert.IsFalse(throttle.ShouldReport(5_000_000, 0, Window - 0.01f));
        Assert.IsTrue(throttle.ShouldReport(5_000_000, 0, Window));
        Assert.IsFalse(throttle.ShouldReport(9_000_000, 0, Window + 1f));
        Assert.IsTrue(throttle.ShouldReport(9_000_000, 0, Window * 2));
    }

    [Test]
    public void TotalsBeyondTheIntRange_DoNotOverflowThePercentage()
    {
        var throttle = Fresh();
        long total = 4_000_000_000L;

        Assert.IsTrue(throttle.ShouldReport(total / 10, total, Window));
        Assert.IsFalse(throttle.ShouldReport(total / 10 + 1, total, Window * 5));
    }

    [Test]
    public void Message_ShowsPercentAndMegabytes()
    {
        Assert.AreEqual("загрузка: 40% (24 из 60 МБ)",
            UpdateMessages.Progress(24L * 1024 * 1024, 60L * 1024 * 1024));
    }

    [Test]
    public void Message_AtCompletion_SaysOneHundred()
    {
        StringAssert.StartsWith("загрузка: 100%", UpdateMessages.Progress(70, 60));
    }

    [Test]
    public void Message_WithUnknownTotal_ShowsOnlyMegabytes()
    {
        Assert.AreEqual("загружено 3 МБ", UpdateMessages.Progress(3L * 1024 * 1024, 0));
    }
}

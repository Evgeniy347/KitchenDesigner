using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>Задержка перед показом подсказки: без неё подсказки вспыхивали бы
/// при каждом проходе курсора по тулбару.</summary>
public class TooltipScheduleTests
{
    private const float Delay = TooltipSchedule.DefaultDelaySeconds;

    [Test]
    public void DueAt_BeforeDelayPasses_StaysSilent()
    {
        var schedule = new TooltipSchedule();
        schedule.Request(now: 10f);

        Assert.IsFalse(schedule.DueAt(10f + Delay * 0.5f),
            "курсор, проехавший по кнопке, не должен зажигать подсказку");
        Assert.IsTrue(schedule.DueAt(10f + Delay),
            "задержавшийся курсор подсказку получает");
    }

    [Test]
    public void DueAt_AfterItFired_DoesNotFireAgain()
    {
        var schedule = new TooltipSchedule();
        schedule.Request(now: 0f);
        Assume.That(schedule.DueAt(Delay), Is.True);

        Assert.IsFalse(schedule.DueAt(Delay + 10f),
            "показ одноразовый: иначе подсказка перестраивалась бы каждый кадр");
    }

    [Test]
    public void Cancel_BeforeDelayPasses_KillsThePendingTooltip()
    {
        var schedule = new TooltipSchedule();
        schedule.Request(now: 0f);
        schedule.Cancel();

        Assert.IsFalse(schedule.DueAt(Delay + 1f),
            "курсор ушёл с кнопки (или кнопку выключили) — отложенный показ обязан "
            + "сняться, иначе подсказка всплывёт над уже неактуальной кнопкой");
        Assert.IsFalse(schedule.Pending);
    }
}

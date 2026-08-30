using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// Дорого купленное знание о производительности, которое до сих пор жило
/// комментарием в UIManager: счётчик проблем на кнопке «Ошибки» пересчитывался
/// таймером раз в секунду, а полный анализ сцены — O(n²) по коллизиям и
/// покрытию кромок — на 259 элементах занимал 150–220 мс. Раз в секунду камера
/// заметно дёргалась, и виновника искали в камере.
///
/// Лечение было двойным, и обе половины обязаны жить: пересчёт идёт только на
/// ИЗМЕНЕНИИ сцены (иначе анализ крутится вечно) и только после ПАУЗЫ (иначе
/// перетаскивание детали, двигающее ревизию каждый кадр, запускает анализ
/// каждый кадр — хуже прежнего таймера).
/// </summary>
public class SceneSettleThrottleTests
{
    private const float Settle = SceneSettleThrottle.DefaultSettleSeconds;
    private const float Frame = 1f / 60f;

    [Test]
    public void UnchangedScene_IsAnalysedExactlyOnce_NoMatterHowLongTheAppRuns()
    {
        var throttle = new SceneSettleThrottle();
        int recomputes = 0;

        for (float now = 0f; now < 60f; now += Frame)
            if (throttle.DueAfterSceneSettled(7, now)) recomputes++;

        Assert.AreEqual(1, recomputes,
            "за час простоя сцена не менялась ни разу — анализ (O(n²), 150–220 мс на 259 "
            + "элементах) обязан отработать РОВНО один раз. Больше — вернулся прежний "
            + "таймер и вместе с ним рывок камеры раз в секунду");
    }

    [Test]
    public void SceneChangingEveryFrame_IsNeverAnalysed()
    {
        var throttle = new SceneSettleThrottle();
        int recomputes = 0;
        int revision = 0;

        for (int frame = 0; frame < 600; frame++)
            if (throttle.DueAfterSceneSettled(++revision, frame * Frame)) recomputes++;

        Assert.AreEqual(0, recomputes,
            "во время перетаскивания ревизия растёт каждый кадр — пока движение идёт, "
            + "анализ не запускается ВОВСЕ");
    }

    [Test]
    public void AfterAChange_TheAnalysisWaitsForTheSettleDelay()
    {
        var throttle = new SceneSettleThrottle();

        Assert.IsFalse(throttle.DueAfterSceneSettled(3, 0f), "кадр самого изменения — только взвод паузы");
        Assert.IsFalse(throttle.DueAfterSceneSettled(3, Settle * 0.5f),
            "сцена изменилась только что: пауза ещё не вышла, анализ ждёт");
        Assert.IsTrue(throttle.DueAfterSceneSettled(3, Settle + Frame),
            "сцена постояла неизменной дольше паузы — вот теперь ровно один пересчёт");
        Assert.IsFalse(throttle.DueAfterSceneSettled(3, Settle + 10f),
            "и больше ни разу, пока сцена не изменится снова");
    }

    [Test]
    public void EveryNewChange_GetsItsOwnAnalysis()
    {
        var throttle = new SceneSettleThrottle();
        float now = 0f;
        int recomputes = 0;

        for (int revision = 1; revision <= 5; revision++)
        {
            throttle.DueAfterSceneSettled(revision, now);
            now += Settle + Frame;
            if (throttle.DueAfterSceneSettled(revision, now)) recomputes++;
            now += Frame;
        }

        Assert.AreEqual(5, recomputes,
            "экономия не должна превратиться в «бейдж больше не обновляется»: "
            + "каждое успокоившееся изменение сцены получает свой пересчёт");
    }
}

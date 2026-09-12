using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Решение «этот кадр растягивания повторяет предыдущий» вынесено из
/// <c>ResizeHandleManager</c> отдельным типом ровно затем же, зачем
/// <see cref="DragFrameRepeat"/> вынесен из <c>ElementMover</c>: чтобы ключ повтора
/// проверялся здесь, на быстром пути, целиком.
///
/// Зачем оно: дамп test-results/perf/perf_20260911_191534.csv дал 68 тяжёлых кадров из
/// 177 на пути <c>ResizeHandleManager.UpdateResize</c> → <c>RefreshHighlights</c>, то есть
/// полная валидация сцены и перекраска каждой детали на каждом кадре жеста — включая те
/// кадры, в которых размер не изменился ни на миллиметр.
///
/// Ключ здесь шире, чем у перетаскивания, и это не украшение: размер, который увидит
/// подсветка, считается ИЗ геометрии самой детали (<c>ResizeMath.Compute</c> получает
/// <c>self</c>), поэтому в ключ входят и применённые габариты, и применённый центр — те
/// самые величины, которыми подсветка питается.</summary>
public class ResizeFrameRepeatTests
{
    private static readonly Vector3Int Dims = new Vector3Int(600, 720, 18);
    private static readonly Vector3 Center = new Vector3(0.1f, 0.36f, 0f);

    /// <summary>Первый кадр жеста не с чем сравнивать — иначе растягивание не начнётся
    /// вовсе.</summary>
    [Test]
    public void FirstFrameOfAResize_IsNeverARepeat()
    {
        var frame = default(ResizeFrameRepeat);

        Assert.IsFalse(frame.Repeats(0.25f, true, 7, Dims, Center),
            "у первого кадра нет предыдущего — сравнивать не с чем");
    }

    /// <summary>Тот же ход ручки, та же сцена, то же прилипание и тот же применённый
    /// размер — повтор.</summary>
    [Test]
    public void SamePointerSameSceneSameAppliedSize_IsARepeat()
    {
        var frame = default(ResizeFrameRepeat);
        frame.Remember(0.25f, true, 7, Dims, Center);

        Assert.IsTrue(frame.Repeats(0.25f, true, 7, Dims, Center));
    }

    /// <summary>Пять отрицательных контролей к тесту выше: без них «повтор» был бы зелёным
    /// просто потому, что метод всегда отвечает «да». Каждое слагаемое ключа обязано в
    /// одиночку ломать повтор — устаревшая подсветка это молчаливая ложь пользователю.</summary>
    [Test]
    public void EachPartOfTheKeyAlone_BreaksTheRepeat()
    {
        var frame = default(ResizeFrameRepeat);
        frame.Remember(0.25f, true, 7, Dims, Center);

        Assert.IsFalse(frame.Repeats(0.251f, true, 7, Dims, Center),
            "ручку потянули дальше — размер пересчитывается");
        Assert.IsFalse(frame.Repeats(0.25f, false, 7, Dims, Center),
            "Ctrl выключил прилипание — ход тот же, а размер выйдет другой");
        Assert.IsFalse(frame.Repeats(0.25f, true, 8, Dims, Center),
            "сцена изменилась под деталью — и грань, к которой липнет размер, тоже");
        Assert.IsFalse(frame.Repeats(0.25f, true, 7, new Vector3Int(601, 720, 18), Center),
            "габарит детали — вход расчёта и то, что видит подсветка");
        Assert.IsFalse(frame.Repeats(0.25f, true, 7, Dims, Center + new Vector3(0.001f, 0f, 0f)),
            "центр детали уехал — прошлый ответ подсветки описывает не эту сцену");
    }

    /// <summary>Память жеста обязана обнуляться: иначе следующее растягивание той же детали
    /// в то же положение начнётся с «делать нечего» и не изменит ничего.</summary>
    [Test]
    public void AfterForget_TheSameFrameIsNoLongerARepeat()
    {
        var frame = default(ResizeFrameRepeat);
        frame.Remember(0.25f, true, 7, Dims, Center);
        frame.Forget();

        Assert.IsFalse(frame.Repeats(0.25f, true, 7, Dims, Center));
    }
}

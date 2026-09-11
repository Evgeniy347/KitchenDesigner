using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Решение «этот кадр перетаскивания повторяет предыдущий» вынесено из
/// <c>ElementMover</c> отдельным типом именно затем, чтобы его можно было проверить
/// здесь, на быстром пути, а не только сценовым тестом.
///
/// Зачем оно вообще: кадр перетаскивания на проекте пользователя (401 деталь) стоит
/// ~4,1 мс поиска прилипания и ~3,9 мс полной валидации сцены — обе цифры измерены
/// на ядре под dotnet и обе растут линейно с числом деталей. Пока мышь стоит,
/// вся эта работа повторяет сама себя байт в байт.</summary>
public class DragFrameRepeatTests
{
    /// <summary>Первый кадр жеста не с чем сравнивать — он обязан считаться новым,
    /// иначе перетаскивание не начнётся вовсе.</summary>
    [Test]
    public void FirstFrameOfAGesture_IsNeverARepeat()
    {
        var frame = default(DragFrameRepeat);

        Assert.IsFalse(frame.Repeats(new Vector3(1f, 2f, 3f), 7, true),
            "у первого кадра нет предыдущего — сравнивать не с чем");
    }

    /// <summary>Тот же кандидат позиции, та же ревизия сцены, то же прилипание —
    /// повтор.</summary>
    [Test]
    public void SamePoseSameRevisionSameSnapping_IsARepeat()
    {
        var frame = default(DragFrameRepeat);
        frame.Remember(new Vector3(1f, 2f, 3f), 7, true);

        Assert.IsTrue(frame.Repeats(new Vector3(1f, 2f, 3f), 7, true));
    }

    /// <summary>Три отрицательных контроля к тесту выше: без них «повтор» был бы
    /// зелёным просто потому, что метод всегда отвечает «да». Каждое слагаемое ключа
    /// обязано в одиночку ломать повтор.</summary>
    [Test]
    public void EachPartOfTheKeyAlone_BreaksTheRepeat()
    {
        var frame = default(DragFrameRepeat);
        frame.Remember(new Vector3(1f, 2f, 3f), 7, true);

        Assert.IsFalse(frame.Repeats(new Vector3(1f, 2f, 3.001f), 7, true),
            "мышь сдвинула деталь — кадр обязан пересчитаться");
        Assert.IsFalse(frame.Repeats(new Vector3(1f, 2f, 3f), 8, true),
            "сцена изменилась под деталью — прошлый ответ устарел");
        Assert.IsFalse(frame.Repeats(new Vector3(1f, 2f, 3f), 7, false),
            "Ctrl выключил прилипание — позиция та же, а результат другой");
    }

    /// <summary>Память жеста обязана обнуляться: иначе следующее перетаскивание той же
    /// детали в ту же точку начнётся с «ничего делать не надо» и не сдвинет ничего.</summary>
    [Test]
    public void AfterForget_TheSameFrameIsNoLongerARepeat()
    {
        var frame = default(DragFrameRepeat);
        frame.Remember(new Vector3(1f, 2f, 3f), 7, true);
        frame.Forget();

        Assert.IsFalse(frame.Repeats(new Vector3(1f, 2f, 3f), 7, true));
    }
}

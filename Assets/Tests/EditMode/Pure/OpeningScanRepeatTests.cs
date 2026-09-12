using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Калитка перед сканом препятствий для открывающейся дверцы, ящика и
/// посудомойки — третья в том же ряду, что <see cref="DragFrameRepeat"/> и
/// <see cref="ResizeFrameRepeat"/>, и вынесена сюда ровно за тем же: решение
/// «этот кадр повторяет предыдущий» проверяется на быстром пути, а не только
/// сценовым тестом. Сенсор работы здесь не заводится — число сканов уже считает
/// <c>OpeningCollision.TakeBuildObstacleCalls</c>, и сценовые сторожа
/// (<c>FacadeOpeningCostTests</c> и «упёршаяся посудомойка» в
/// <c>DishwasherElementTests</c>) спрашивают именно его.
///
/// Цена вопроса из дампа пользователя (perf_20260912_185912.csv): на фазе
/// открытия 149 кадров дороже 33 мс, и в 128 из них доминирует
/// <c>OpeningCollision.FindMaxProgress</c> — 39,5 мс на кадр, из них 36,5 мс
/// в <c>ScanForBlock</c>. При этом в 121 кадре из 138 ничего не анимировалось:
/// скан повторял сам себя.
///
/// Ключ обязан быть ПОЛНЫМ ВХОДОМ скана: ревизия сцены (препятствия), поза
/// покоя (откуда дверца метёт) и габарит (сколько метёт). Стоит выпасть любому
/// слагаемому — калитка начнёт возвращать чужой ответ, и дверца пройдёт сквозь
/// препятствие. Поэтому у положительного теста есть четыре отрицательных
/// контроля, по одному на слагаемое.</summary>
public class OpeningScanRepeatTests
{
    private static readonly Vector3 Rest = new Vector3(0.1f, 0.45f, -3.06f);
    private const float Sin45 = 0.70710678f;
    private static readonly Quaternion Facing = new Quaternion(0f, Sin45, 0f, Sin45);
    private static readonly Vector3 Span = new Vector3(0.596f, 0.716f, 0.018f);

    private static OpeningScanRepeat Remembered(float safeProgress = 0.35f)
    {
        var gate = default(OpeningScanRepeat);
        gate.Remember(7, Rest, Facing, Span, safeProgress);
        return gate;
    }

    /// <summary>Первому кадру жеста не с чем сравниваться — он обязан считаться
    /// новым, иначе дверца откроется не спросив препятствий вовсе.</summary>
    [Test]
    public void FirstFrameOfAGesture_IsNeverARepeat()
    {
        var gate = default(OpeningScanRepeat);

        Assert.IsFalse(gate.Repeats(7, Rest, Facing, Span),
            "у первого кадра нет предыдущего — сравнивать не с чем");
        Assert.AreEqual(1f, gate.SafeProgress,
            "пока скана не было, калитка не смеет ограничивать ход");
    }

    [Test]
    public void SameRevisionSameRestPoseSameSpan_IsARepeat()
    {
        var gate = Remembered();

        Assert.IsTrue(gate.Repeats(7, Rest, Facing, Span));
        Assert.AreEqual(0.35f, gate.SafeProgress, 1e-6f,
            "повтор обязан отдать прошлый ответ, а не пересчитать его");
    }

    /// <summary>Четыре отрицательных контроля к тесту выше: без них «повтор» был бы
    /// зелёным просто потому, что метод всегда отвечает «да». Каждое слагаемое ключа
    /// обязано в одиночку ломать повтор — и первое из них, ревизия сцены, и есть
    /// «препятствие появилось или сдвинулось».</summary>
    [Test]
    public void EachPartOfTheKeyAlone_BreaksTheRepeat()
    {
        var gate = Remembered();

        Assert.IsFalse(gate.Repeats(8, Rest, Facing, Span),
            "препятствие появилось или сдвинулось — прошлый ответ устарел");
        Assert.IsFalse(gate.Repeats(7, Rest + new Vector3(0f, 0f, 0.001f), Facing, Span),
            "деталь переехала — метёт она теперь другое место");
        Assert.IsFalse(gate.Repeats(7, Rest, Quaternion.identity, Span),
            "деталь развернули — сектор открывания другой");
        Assert.IsFalse(gate.Repeats(7, Rest, Facing, Span + new Vector3(0.1f, 0f, 0f)),
            "деталь растянули — дверца стала длиннее и цепляет дальше");
    }

    /// <summary>Память жеста обязана обнуляться: иначе следующее открытие той же
    /// дверцы начнётся с чужого ответа, снятого до правки сцены.</summary>
    [Test]
    public void AfterForget_TheSameFrameIsNoLongerARepeat()
    {
        var gate = Remembered();
        gate.Forget();

        Assert.IsFalse(gate.Repeats(7, Rest, Facing, Span));
        Assert.AreEqual(1f, gate.SafeProgress,
            "забытая калитка снова не ограничивает ход");
    }

    /// <summary>Новый ответ вытесняет прошлый: иначе дверца, однажды упёршаяся,
    /// продолжила бы упираться там же и после того, как препятствие убрали.</summary>
    [Test]
    public void RememberingAgain_ReplacesTheAnswerAndTheKey()
    {
        var gate = Remembered(0.35f);
        gate.Remember(8, Rest, Facing, Span, 1f);

        Assert.IsFalse(gate.Repeats(7, Rest, Facing, Span),
            "прошлая ревизия больше не повтор");
        Assert.IsTrue(gate.Repeats(8, Rest, Facing, Span));
        Assert.AreEqual(1f, gate.SafeProgress, 1e-6f,
            "препятствие ушло — путь свободен, и калитка отдаёт именно это");
    }
}

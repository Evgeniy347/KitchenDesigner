using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ключ памяти скана препятствий: полный ВХОД вопроса «докуда этой дверце
/// можно». Ключ строит САМ носитель — и носители строят его по-разному, потому что
/// по-разному метут: у фасада и ящика поза покоя это `_closedPos`/`_closedRot`
/// (трансформ во время открывания уехал), у остальных — собственный трансформ;
/// габарит у фасада, ящика, духовки и посудомойки берётся из `localScale`, а у
/// стиральной машины и оконного проёма из `DimensionsMM`, потому что размер им
/// задаёт пользователь. Общий тип ключ ПРИНИМАЕТ, а не диктует.
///
/// Живёт в Pure ровно затем, чтобы отрицательные контроли шли на быстром пути: это
/// чистая арифметика сравнения, и она стоит 0 секунд прогона. Раньше здесь стоял
/// `OpeningScanRepeat` — калитка, несущая ещё и ОТВЕТ; ответ переехал в
/// `OpeningScanMemo` к памяти препятствий, а сюда осталась только сама сверка.
///
/// Цена вопроса, из-за которой всё это есть (perf_20260912_185912.csv и
/// perf_20260913_080751.csv): один скан — до 39,5 мс на кадр, и без ключа он
/// повторял сам себя на каждом кадре, где не менялось ничего.</summary>
public class OpeningScanKeyTests
{
    private const float Sin45 = 0.70710678f;

    private static readonly Vector3 Rest = new Vector3(0.1f, 0.45f, -3.06f);
    private static readonly Quaternion Facing = new Quaternion(0f, Sin45, 0f, Sin45);
    private static readonly Vector3 Span = new Vector3(0.596f, 0.716f, 0.018f);

    private static OpeningScanKey Key(int revision = 7) =>
        new OpeningScanKey(revision, Rest, Facing, Span);

    [Test]
    public void SameRevisionSameRestPoseSameSpan_IsTheSameKey()
    {
        Assert.IsTrue(Key().Matches(Key()));
    }

    /// <summary>Четыре отрицательных контроля к тесту выше: без них «совпал» был бы
    /// зелёным просто потому, что метод всегда отвечает «да». Каждое слагаемое ключа
    /// обязано в одиночку ломать совпадение — и первое из них, ревизия сцены, и есть
    /// «препятствие появилось или сдвинулось».</summary>
    [Test]
    public void EachPartOfTheKeyAlone_BreaksTheMatch()
    {
        var kept = Key();

        Assert.IsFalse(kept.Matches(Key(8)),
            "препятствие появилось или сдвинулось — прошлый ответ устарел");
        Assert.IsFalse(kept.Matches(
                new OpeningScanKey(7, Rest + new Vector3(0f, 0f, 0.001f), Facing, Span)),
            "деталь переехала — метёт она теперь другое место");
        Assert.IsFalse(kept.Matches(new OpeningScanKey(7, Rest, Quaternion.identity, Span)),
            "деталь развернули — сектор открывания другой");
        Assert.IsFalse(kept.Matches(
                new OpeningScanKey(7, Rest, Facing, Span + new Vector3(0.1f, 0f, 0f))),
            "деталь растянули — дверца стала длиннее и цепляет дальше");
    }

    /// <summary>Ключ обязан хранить то, что в него положили: носитель читает его
    /// обратно, когда строит следующий, и подменённое поле означало бы совпадение
    /// с чужим кадром.</summary>
    [Test]
    public void TheKey_ReadsBackEveryPartItWasBuiltFrom()
    {
        var key = Key(11);

        Assert.AreEqual(11, key.Revision);
        Assert.AreEqual(Rest, key.RestPosition);
        Assert.AreEqual(Facing, key.RestRotation);
        Assert.AreEqual(Span, key.RestSpan);
    }

    /// <summary>Ключ по умолчанию — не «совпал со всем»: носитель, который ещё ни разу
    /// не спрашивал сцену, обязан спросить. Иначе дверца поехала бы, не глянув
    /// на препятствия ни разу.</summary>
    [Test]
    public void ADefaultKey_MatchesNothingReal()
    {
        Assert.IsFalse(default(OpeningScanKey).Matches(Key()));
        Assert.IsFalse(Key().Matches(default(OpeningScanKey)));
    }
}

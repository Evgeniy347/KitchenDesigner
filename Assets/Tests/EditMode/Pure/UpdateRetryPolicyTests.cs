#nullable disable
using System;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Политика повторов автообновления: сколько попыток, сколько ждать перед
/// следующей и какой отказ вообще имеет смысл повторять. Здесь нет ни сети, ни
/// корутин — проверяются только решения, потому что именно они ломаются молча:
/// повтор после «404» тратит время впустую, повтор после «Отмена» возвращает
/// пользователю окно, которое он только что закрыл.
/// </summary>
public class UpdateRetryPolicyTests
{
    private static UpdateRetryPolicy TwoPauses() => new UpdateRetryPolicy(1f, 2f);

    [Test]
    public void MaxAttempts_IsOneMoreThanTheNumberOfPauses()
    {
        Assert.AreEqual(3, TwoPauses().MaxAttempts,
            "паузы стоят МЕЖДУ попытками: две паузы обслуживают три попытки. "
            + "Если считать попытку на каждую паузу, последняя пауза окажется "
            + "перед несуществующей попыткой и приложение просто подождёт и сдастся");
    }

    [Test]
    public void FirstAttempt_StartsWithoutAPause()
    {
        Assert.AreEqual(0f, TwoPauses().PauseBeforeAttemptSeconds(1),
            "пауза перед ПЕРВОЙ попыткой — это задержка старта проверки на ровном "
            + "месте: сбоя ещё не было, ждать нечего");
    }

    [Test]
    public void PauseBeforeEachRetry_GrowsWithTheAttemptNumber()
    {
        var policy = TwoPauses();
        Assert.AreEqual(1f, policy.PauseBeforeAttemptSeconds(2),
            "вторая попытка идёт сразу после короткой паузы: чаще всего сеть "
            + "моргнула на секунду");
        Assert.Greater(policy.PauseBeforeAttemptSeconds(3), policy.PauseBeforeAttemptSeconds(2),
            "растущая пауза — это признание, что дело не в моргании. Равные паузы "
            + "означают три одинаковых удара в мёртвый канал за то же время");
    }

    [Test]
    public void PauseBeyondTheLastAttempt_IsZero_NotAnIndexCrash()
    {
        Assert.AreEqual(0f, TwoPauses().PauseBeforeAttemptSeconds(4),
            "номер за пределами расписания приходит из цикла, который сбился: "
            + "ответить нулём безопаснее, чем упасть по индексу внутри корутины, "
            + "где исключение съедается и обновление просто не происходит");
    }

    [Test]
    public void APolicyWithPausesThatDoNotGrow_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new UpdateRetryPolicy(5f, 2f),
            "убывающая пауза — это опечатка в порядке чисел, и она не видна "
            + "нигде, кроме как здесь: повторы просто станут агрессивнее к концу");
        Assert.Throws<ArgumentException>(() => new UpdateRetryPolicy(0f),
            "нулевая пауза превращает повтор в мгновенный второй запрос — ровно то, "
            + "чего сервер после 429 и ждёт от плохого клиента");
    }

    [Test]
    public void AttemptsExhausted_IsTrueOnTheLastAttempt_NotAfterIt()
    {
        var policy = TwoPauses();
        Assert.IsFalse(policy.AttemptsExhausted(2),
            "на второй из трёх попыток запас ещё есть");
        Assert.IsTrue(policy.AttemptsExhausted(3),
            "исчерпание проверяется ПОСЛЕ попытки: спросив «исчерпаны?» только "
            + "про номер 4, цикл сделает лишний четвёртый запрос");
    }

    [Test]
    public void CancelledByTheUser_IsNeverRetried_EvenOnTheFirstAttempt()
    {
        Assert.IsFalse(TwoPauses().ShouldRetryAfter(1, UpdateAttemptFailure.Cancelled()),
            "«Отмена» — это решение пользователя, а не сбой сети. Повтор после неё "
            + "заново открывает окно загрузки, которое он только что закрыл, и "
            + "качает файл, который ему не нужен");
    }

    [Test]
    public void ANetworkFailureWithoutAnyResponse_IsRetried()
    {
        Assert.IsTrue(TwoPauses().ShouldRetryAfter(1, UpdateAttemptFailure.NoAnswer()),
            "ради этого случая всё и затевалось: оборванное соединение и "
            + "молчащий сервер — самая частая причина неудачного обновления");
    }

    [Test]
    public void ServerErrorsAndThrottling_AreRetried()
    {
        var policy = TwoPauses();
        Assert.IsTrue(policy.ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(503)),
            "5xx — это «сейчас не могу», а не «никогда»");
        Assert.IsTrue(policy.ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(429)),
            "429 просит подождать и повторить — именно это растущая пауза и делает");
        Assert.IsTrue(policy.ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(408)),
            "408 — таймаут на стороне сервера, следующий запрос может успеть");
    }

    [Test]
    public void ClientErrors_AreNotRetried_BecauseTheAnswerWillNotChange()
    {
        var policy = TwoPauses();
        Assert.IsFalse(policy.ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(404)),
            "релиз удалили или ссылка неверна: три одинаковых запроса дадут три "
            + "одинаковых 404 и растянут отказ на секунды вместо мгновенного");
        Assert.IsFalse(policy.ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(403)),
            "403 — это лимит или запрет, повтор его не снимет");
    }

    [Test]
    public void ASuccessfulResponseWithABrokenBody_IsNotRetried()
    {
        Assert.IsFalse(TwoPauses().ShouldRetryAfter(1, UpdateAttemptFailure.FromResponse(200)),
            "сервер ответил целиком, а разобрать ответ не вышло — это сломанный "
            + "манифест, и он придёт таким же во второй раз");
    }

    [Test]
    public void ARetryableFailure_StopsBeingRetried_OnceTheAttemptsRunOut()
    {
        var policy = TwoPauses();
        Assert.IsTrue(policy.ShouldRetryAfter(2, UpdateAttemptFailure.NoAnswer()),
            "вторая попытка из трёх — запас ещё есть");
        Assert.IsFalse(policy.ShouldRetryAfter(3, UpdateAttemptFailure.NoAnswer()),
            "иначе повторяемый отказ крутит цикл вечно, и пользователь не увидит "
            + "ни ошибки, ни конца загрузки");
    }

    [Test]
    public void TheShippedPolicies_GiveThreeAttempts_AndWaitLongerBeforeTheBiggerFile()
    {
        var check = UpdateRetryPolicy.ForReleaseCheck();
        var download = UpdateRetryPolicy.ForInstallerDownload();

        Assert.AreEqual(3, check.MaxAttempts,
            "три попытки — это то число, которое видит пользователь в тексте "
            + "«Повторная попытка N из 3»");
        Assert.AreEqual(3, download.MaxAttempts,
            "загрузка считает попытки так же, как проверка: иначе текст окна врёт");
        Assert.Greater(download.PauseBeforeAttemptSeconds(2), check.PauseBeforeAttemptSeconds(2),
            "проверка релиза идёт на старте приложения и её ждёт пользователь — "
            + "она обязана быть быстрой. Перед повтором стомегабайтной загрузки "
            + "спешить некуда, и лишние секунды дают сети встать на ноги");
    }
}

#nullable disable
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core.Update;

/// <summary>
/// Реальный даунлоадер установщика против локального файла (протокол file:// —
/// без сети и внешних серверов, тест детерминирован). Покрывает регрессию, когда
/// запрос создавался, но SendWebRequest() не вызывался: прогресс стоял на нуле,
/// колбэки не приходили и «Отмена» ничего не делала. Тест на завершение
/// падал бы по таймауту, если запрос снова не стартует.
///
/// Повторы проверяются ПАРОЙ тестов, и поодиночке ни один из них не годится:
/// «ненайденный источник» требует ровно одной попытки и остался бы зелёным,
/// даже если цикл повторов выкинуть целиком, а «неотвечающий хост» требует
/// всех трёх и краснеет ровно в этом случае. Разделяет их не удача, а
/// классификация отказа: внятный ответ сервера повторять нечего, молчание —
/// нужно. Попытки считаются через onAttemptStarted, а не по времени: паузы в
/// тестах сжаты до кадров (см. Impatient).
/// </summary>
public class UpdateDownloaderTests
{
    private const float TimeoutSeconds = 15f;

    // Порт 1 на петле: соединение отвергается мгновенно и локально — ни DNS, ни
    // внешней сети, ни ожидания таймаута, поэтому тест детерминирован и быстр.
    // Именно такой отказ (соединения нет, кода ответа нет) политика и обязана
    // повторять; несуществующий file:// для этого не годится — на него Unity
    // отвечает кодом, то есть источник ВНЯТНО сказал «файла нет».
    private const string UnreachableUrl = "http://127.0.0.1:1/kd-installer.exe";

    private static string TempPath(string prefix)
        => Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N") + ".bin");

    // Тот же расклад попыток, что в бою (три), но с паузами в кадры вместо
    // секунд: боевые 2 и 5 с превратили бы каждый тест на отказ в семисекундное
    // ожидание, а проверяется здесь СЧЁТ попыток, не их темп.
    private static UpdateRetryPolicy Impatient() => new UpdateRetryPolicy(0.02f, 0.05f);

    [UnityTest]
    public IEnumerator Downloader_DownloadsLocalFile_ReportsProgressAndCompletes()
    {
        var src = TempPath("kd-update-src-");
        var dst = TempPath("kd-update-dst-");
        var content = new byte[64 * 1024];
        for (int i = 0; i < content.Length; i++) content[i] = (byte)(i % 251);
        File.WriteAllBytes(src, content);

        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();

            bool done = false, failed = false;
            float lastProgress = -1f;
            downloader.BeginDownload(new Uri(src).AbsoluteUri, dst,
                p => lastProgress = p, (_, _) => { }, () => done = true, (_, _) => failed = true);

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(done, $"скачивание не завершилось за {TimeoutSeconds} с");
            Assert.IsFalse(failed);
            Assert.AreEqual(1f, lastProgress, 0.01f, "прогресс должен дойти до 1");
            Assert.IsTrue(File.Exists(dst), "целевой файл не создан");
            CollectionAssert.AreEqual(content, File.ReadAllBytes(dst),
                "содержимое скачанного файла отличается от исходного");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            // Если запрос так и не завершился (таймаут теста), DownloadHandlerFile
            // ещё держит файл — при таком отказе удаление может кинуть IOException
            // и замаскировать настоящую причину падения.
            try { File.Delete(src); } catch (IOException) { }
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }

    [UnityTest]
    public IEnumerator Downloader_ReportsFailure_ForMissingSource()
    {
        var dst = TempPath("kd-update-dst-");
        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();

            bool done = false, failed = false, cancelled = false;
            string reason = null;
            int attempts = 0;
            downloader.RetryPolicy = Impatient();
            downloader.BeginDownload(new Uri(TempPath("kd-update-missing-")).AbsoluteUri, dst,
                _ => { }, (_, _) => attempts++, () => done = true,
                (m, c) => { failed = true; reason = m; cancelled = c; });

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(failed, "отсутствующий источник должен завершиться отказом");
            Assert.IsFalse(done);
            Assert.IsFalse(cancelled);
            Assert.AreEqual(1, attempts,
                "источник ОТВЕТИЛ, и ответ этот — «такого файла нет». Он не станет "
                + "другим ни через две секунды, ни через семь: повторять здесь значит "
                + "тянуть отказ, о котором уже всё известно. Повторяется молчание "
                + "канала (см. соседний тест на неотвечающий хост), а не внятный "
                + "ответ. Причина: " + reason);
            Assert.IsFalse(File.Exists(dst), "при отказе недокачанный файл удаляется");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }

    [UnityTest]
    public IEnumerator Downloader_RetriesUntilAttemptsRunOut_WhenTheHostNeverAnswers()
    {
        var dst = TempPath("kd-update-dst-");
        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();

            bool done = false, failed = false, cancelled = false;
            string reason = null;
            int attempts = 0;
            downloader.RetryPolicy = Impatient();
            downloader.BeginDownload(UnreachableUrl, dst,
                _ => { }, (_, _) => attempts++, () => done = true,
                (m, c) => { failed = true; reason = m; cancelled = c; });

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(failed, $"отказ не дошёл до колбэка за {TimeoutSeconds} с");
            Assert.IsFalse(done);
            Assert.IsFalse(cancelled, "пользователь ничего не отменял: " + reason);
            Assert.AreEqual(downloader.RetryPolicy.MaxAttempts, attempts,
                "ради этого случая ретраи и делались: сервер не ответил вообще, кода "
                + "ответа нет, и следующая попытка через пару секунд вполне может "
                + "пройти. Одна попытка здесь означает, что цикл повторов не крутится "
                + "вовсе — и тогда тест на ненайденный файл зеленеет по совершенно "
                + "другой причине, чем думает его автор. Причина: " + reason);
            Assert.IsFalse(File.Exists(dst),
                "после исчерпания попыток в temp не остаётся ни одного огрызка");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }

    [UnityTest]
    public IEnumerator Downloader_Cancelled_ReportsCancellation_AndLeavesNoPartialFile()
    {
        var src = TempPath("kd-update-src-");
        var dst = TempPath("kd-update-dst-");
        File.WriteAllBytes(src, new byte[4 * 1024 * 1024]);

        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();

            bool done = false, failed = false, cancelled = false;
            string reason = null;
            int attempts = 0;
            downloader.RetryPolicy = Impatient();
            downloader.BeginDownload(new Uri(src).AbsoluteUri, dst,
                _ => { }, (_, _) => attempts++, () => done = true,
                (m, c) => { failed = true; reason = m; cancelled = c; });
            downloader.Cancel();

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(failed, $"отмена не дошла до колбэка за {TimeoutSeconds} с");
            Assert.IsTrue(cancelled,
                "Abort() отдаёт обычный ConnectionError — по результату запроса отмену от "
                + "сетевого сбоя не отличить. Решает наш собственный флаг, и решает ПЕРВЫМ: "
                + "иначе пользователь, нажавший «Отмена», получает окно с ошибкой сети. "
                + "Причина: " + reason);
            Assert.IsFalse(done);
            Assert.AreEqual(1, attempts,
                "отмена — не сбой сети: повторять нечего. Повтор после «Отмена» "
                + "качает файл, от которого пользователь только что отказался, "
                + "и показывает ему окно, которое он закрыл");
            Assert.IsFalse(File.Exists(dst),
                "недокачанный файл после отмены остаётся мусором в temp и, что хуже, "
                + "выглядит как готовый установщик");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            try { File.Delete(src); } catch (IOException) { }
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }

    // Пара к Cancel_BeforeAnythingStarted: там _active ещё null, и `?.` гасит
    // вызов сам — тест зелёный при ЛЮБОЙ реализации Abort(). Здесь запрос
    // реально был и уже завершился, то есть проверяется именно то, чем метод
    // назван (AbortEvenIfTheRequestAlreadyFinished). Раньше эту ветку прикрывал
    // пустой `catch (Exception) { }`: он не давал ни отменить, ни узнать, что
    // отмена не сработала. Catch снят, и его отсутствие держит этот тест — если
    // Unity когда-нибудь начнёт бросать из Abort() по завершённому запросу,
    // обработчик кнопки «Отмена» вынесет наружу, и упадёт здесь, а не у
    // пользователя.
    [UnityTest]
    public IEnumerator Cancel_AfterDownloadCompleted_DoesNotThrow()
    {
        var src = TempPath("kd-update-src-");
        var dst = TempPath("kd-update-dst-");
        File.WriteAllBytes(src, new byte[4096]);

        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();

            bool done = false, failed = false;
            downloader.BeginDownload(new Uri(src).AbsoluteUri, dst,
                _ => { }, (_, _) => { }, () => done = true, (_, _) => failed = true);

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(done, $"скачивание не завершилось за {TimeoutSeconds} с");
            Assert.DoesNotThrow(() => downloader.Cancel(),
                "«Отмена» после того, как загрузка уже закончилась — обычный порядок "
                + "кликов, а не редкость: Abort() по завершённому запросу обязан быть "
                + "безвредным, иначе кнопка отмены роняет UI");
            Assert.DoesNotThrow(() => downloader.Cancel());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            try { File.Delete(src); } catch (IOException) { }
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }

    [Test]
    public void Cancel_BeforeAnythingStarted_DoesNotThrow()
    {
        var go = new GameObject("downloader");
        try
        {
            var downloader = go.AddComponent<UnityWebRequestDownloader>();
            Assert.DoesNotThrow(() => downloader.Cancel(),
                "Отмена приходит из UI и может опередить запрос или прийти дважды — "
                + "Abort() по уже завершённому или ещё не созданному запросу не должен "
                + "выносить обработчик кнопки");
            Assert.DoesNotThrow(() => downloader.Cancel());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
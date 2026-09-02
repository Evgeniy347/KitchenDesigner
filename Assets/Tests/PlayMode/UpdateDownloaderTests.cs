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
/// </summary>
public class UpdateDownloaderTests
{
    private const float TimeoutSeconds = 15f;

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
            downloader.Start(new Uri(src).AbsoluteUri, dst,
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
            int attempts = 0;
            downloader.RetryPolicy = Impatient();
            downloader.Start(new Uri(TempPath("kd-update-missing-")).AbsoluteUri, dst,
                _ => { }, (_, _) => attempts++, () => done = true,
                (_, c) => { failed = true; cancelled = c; });

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(failed, "отсутствующий источник должен завершиться отказом");
            Assert.IsFalse(done);
            Assert.IsFalse(cancelled);
            Assert.AreEqual(downloader.RetryPolicy.MaxAttempts, attempts,
                "сетевой отказ повторяется до исчерпания попыток, и об отказе "
                + "пользователю говорят ОДИН раз — после последней. Одна попытка "
                + "здесь означает, что цикл повторов не работает вовсе");
            Assert.IsFalse(File.Exists(dst), "при отказе недокачанный файл удаляется");
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
            downloader.Start(new Uri(src).AbsoluteUri, dst,
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
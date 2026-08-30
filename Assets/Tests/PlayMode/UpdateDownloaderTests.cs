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
                p => lastProgress = p, () => done = true, (_, _) => failed = true);

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
            downloader.Start(new Uri(TempPath("kd-update-missing-")).AbsoluteUri, dst,
                _ => { }, () => done = true, (_, c) => { failed = true; cancelled = c; });

            float elapsed = 0f;
            while (!done && !failed && elapsed < TimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(failed, "отсутствующий источник должен завершиться отказом");
            Assert.IsFalse(done);
            Assert.IsFalse(cancelled);
            Assert.IsFalse(File.Exists(dst), "при отказе недокачанный файл удаляется");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            try { if (File.Exists(dst)) File.Delete(dst); } catch (IOException) { }
        }
    }
}
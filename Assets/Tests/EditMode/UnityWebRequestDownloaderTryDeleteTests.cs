#nullable disable
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// TryDelete's catch narrowed from a bare `catch { }` to IOException +
/// UnauthorizedAccessException (was swallowing anything, including a NullReferenceException
/// from a real bug in the caller). The narrowing only holds if a locked-file delete really
/// raises one of those two types on this runtime — proved here with an actual file lock,
/// not assumed.
/// </summary>
public class UnityWebRequestDownloaderTryDeleteTests
{
    private static readonly MethodInfo TryDeleteMethod =
        typeof(UnityWebRequestDownloader).GetMethod("TryDelete",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static void TryDelete(string path)
    {
        try
        {
            TryDeleteMethod.Invoke(null, new object[] { path });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    [Test]
    public void LockedFile_DoesNotThrow_AndIsLeftInPlace()
    {
        var path = Path.Combine(Path.GetTempPath(), "kd-trydelete-locked-" + Guid.NewGuid() + ".tmp");
        File.WriteAllText(path, "locked");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.DoesNotThrow(() => TryDelete(path),
                "a partial-download cleanup that races an antivirus/indexer file lock must "
                + "not throw out of the download coroutine");
            Assert.IsTrue(File.Exists(path),
                "the lock should have made File.Delete fail — if the file is gone, this test "
                + "is not exercising the locked path it claims to");
        }

        File.Delete(path);
    }

    [Test]
    public void MissingFile_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "kd-trydelete-missing-" + Guid.NewGuid() + ".tmp");

        Assert.DoesNotThrow(() => TryDelete(path));
    }
}

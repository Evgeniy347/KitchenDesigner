#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Генератор docs/overview.png — общий вид приложения в обычном режиме:
/// 3D-сцена из example.save.json плюс живой интерфейс (тулбар, левая панель).
/// Это кадр «как выглядит программа», поэтому фоторежим здесь не включается.</summary>
[Explicit("генератор docs/overview.png — см. tools\\artifacts.ps1")]
public class OverviewScreenshotTests : DocsArtifactFixture
{
    /// <summary>Камера: target и углы из example.save.json, зашиты жёстко, чтобы
    /// кадр не зависел от того, куда её увели в сейве.
    ///
    /// Дистанция 3,4, а не 3,9: на 3,9 объектив оказывается ровно в толще
    /// западной стены (её плита занимает x от −1,835 до −1,585, камера
    /// приходила в −1,645). Стену в обычном режиме прячет обрезка «как в The
    /// Sims», но её чёрный контур — настоящие бруски, и в сантиметрах от
    /// объектива они растягивались через весь кадр поверх левой панели.</summary>
    private static readonly CameraState SavedCamera = new CameraState
    {
        valid = true,
        targetX = 0.7281801f,
        targetY = 1.1341923f,
        targetZ = -2.7433026f,
        angleX = 26.399876f,
        angleY = -582.8002f,
        distance = 3.4f
    };

    [UnityTest]
    public IEnumerator Overview_NormalMode_ShowsSceneWithUi()
    {
        yield return LoadExampleProject();
        yield return ApplyCameraState(SavedCamera);
        yield return AttachUiOverlay();
        yield return CaptureToDocs("overview.png");
    }
}

#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Генераторы картинок про зазоры фасадов: общий вид с включёнными
/// контурами рёбер (docs/gaps_overview.png) и крупный план шва между двумя
/// фасадами ящика (docs/facade-gaps.png).
///
/// Обе картинки — про геометрию, а не про интерфейс, поэтому UI в кадр не
/// подмешивается: панели только отъедали бы место у того, ради чего кадр.</summary>
[Explicit("генераторы docs/gaps_overview.png и docs/facade-gaps.png — см. tools\\artifacts.ps1")]
public class GapsScreenshotTests : DocsArtifactFixture
{
    /// <summary>Общий вид: та же орбита, что у overview.png, чтобы две картинки
    /// в README читались как «одна сцена, разные режимы отображения».</summary>
    private static readonly CameraState OverviewCamera = new CameraState
    {
        valid = true,
        targetX = 0.7281801f,
        targetY = 1.1341923f,
        targetZ = -2.7433026f,
        angleX = 26.399876f,
        angleY = -582.8002f,
        distance = 3.4f
    };

    /// <summary>Крупный план: та же точка сцены, что у GIF-анимации ящика
    /// (там эта орбита проверена кадрами), но вдвое ближе и чуть ниже — в кадр
    /// должны попасть швы между фасадами, а не ящик целиком.</summary>
    private static readonly CameraState CloseUpCamera = new CameraState
    {
        valid = true,
        targetX = 0.9263714f,
        targetY = 0.75004f,
        targetZ = -2.1795108f,
        angleX = 18f,
        angleY = -588.40015f,
        distance = 0.85f
    };

    [UnityTest]
    public IEnumerator GapsOverview_EdgeOutline_OutlinesWholeKitchen()
    {
        yield return LoadExampleProject();
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        yield return null;

        yield return ApplyCameraState(OverviewCamera);
        yield return CaptureToDocs("gaps_overview.png");
    }

    [UnityTest]
    public IEnumerator FacadeGaps_EdgeOutline_ShowsSeamCloseUp()
    {
        yield return LoadExampleProject();
        KitchenSettings.Instance.NormalView.edgeOutline = true;
        yield return null;

        Cam.fieldOfView = 36f;
        yield return ApplyCameraState(CloseUpCamera);
        yield return CaptureToDocs("facade-gaps.png");
    }
}

#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Генератор docs/specification.png — окно спецификации поверх сцены:
/// таблица деталей с размерами и площадью и кнопка выгрузки в CSV.
///
/// Окно открывает САМ генератор (ProjectWindowsTestState), а не сейв: состав
/// открытых окон в example.save.json меняется от каждого ручного сохранения
/// десктопа, и картинка бы каждый раз ехала вслед за ним.</summary>
[Explicit("генератор docs/specification.png — см. tools\\artifacts.ps1")]
public class SpecificationScreenshotTests : DocsArtifactFixture
{
    private const string SpecificationWindowId = "specification";

    private static readonly CameraState SavedCamera = new CameraState
    {
        valid = true,
        targetX = 0.7281801f,
        targetY = 1.1341923f,
        targetZ = -2.7433026f,
        angleX = 26.399876f,
        angleY = -582.8002f,
        // 3,4 — потолок, за которым камера уходит в толщу западной стены;
        // разобрано у OverviewScreenshotTests.SavedCamera.
        distance = 3.4f
    };

    [UnityTest]
    public IEnumerator Specification_WindowOpen_ShowsPartsTableAndExport()
    {
        yield return LoadExampleProject();
        yield return ApplyCameraState(SavedCamera);

        ProjectWindowsTestState.ShowOnly(SpecificationWindowId);
        yield return null;
        yield return null;

        yield return AttachUiOverlay();
        yield return CaptureToDocs("specification.png");
    }
}

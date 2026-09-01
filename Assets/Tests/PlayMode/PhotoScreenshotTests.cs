#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Генератор docs/photo.png — заглавный кадр README.
///
/// Обычный режим намеренно лёгкий: у URP-ассета shadowDistance = 0, теней нет
/// вовсе, и на кадре это читается как «плоский свет». Красивую картинку в
/// приложении даёт фоторежим — он поднимает тени, сглаживание, AO и bloom,
/// достраивает потолок и снимает прозрачность. Поэтому заглавный кадр
/// снимается именно им, на максимальном пресете и без интерфейса.</summary>
[Explicit("генератор docs/photo.png — см. tools\\artifacts.ps1")]
public class PhotoScreenshotTests : DocsArtifactFixture
{
    /// <summary>Кухня в example.save.json стоит в комнате примерно 3,2 × 3,0 м:
    /// стены ограничивают её по x от −1,585 до 1,588 и по z от −3,618. В
    /// фоторежиме потолок достроен, а стены не прячутся — камера обязана
    /// остаться ВНУТРИ этой коробки, иначе кадр занимает изнанка перекрытия
    /// (так вышло на 4,2 м с углами обычного вида) или наружная стена (на 2,2 м
    /// по орбите PhotoModeScreenshotTests: она смотрит на юг, а до южной стены
    /// от цели всего метр с небольшим).
    ///
    /// Поэтому берётся орбита overview.png — она кухню кадрирует и её камера
    /// заведомо внутри комнаты — с углом подъёма 20° вместо 26°: на 26° объектив
    /// поднимается на 2,65 м и упирается в достроенное перекрытие, на 20° встаёт
    /// на 2,30 м, в 40 см под ним.
    ///
    /// photoTarget и photoAngle не задаются намеренно: при нулях SetState
    /// подставляет в них обычные target и углы, а фотодистанцию берёт свою.</summary>
    private static readonly CameraState InteriorCamera = new CameraState
    {
        valid = true,
        targetX = 0.7281801f, targetY = 1.1341923f, targetZ = -2.7433026f,
        angleX = 20f, angleY = -582.8002f,
        distance = 3.4f, photoDistance = 3.4f
    };

    [UnityTest]
    public IEnumerator Photo_PhotoMode_ShowsInteriorWithoutUi()
    {
        yield return LoadExampleProject();

        Cam.fieldOfView = 50f;
        yield return ApplyCameraState(InteriorCamera);

        PhotoQualityPresetTable.Apply(PhotoQualityPreset.High, KitchenSettings.Instance);
        PhotoMode.SetActive(true);

        // Потолок, тени-кастеры и объём пост-обработки встают не мгновенно:
        // снимать сразу после входа — значит поймать кадр без половины из них.
        for (int i = 0; i < 6; i++) yield return null;

        HideAllUi();
        yield return CaptureToDocs("photo.png");

        PhotoMode.SetActive(false);
        yield return null;
    }
}

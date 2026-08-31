using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class TextureOverlaySnapTests
{
    private static readonly Vector2Int FaceA = new Vector2Int(3000, 2500);

    [Test]
    public void NeighbourEdges_DoNotRequireOverlapAcrossTheOtherAxis()
    {
        var overlays = new List<TextureOverlaySpec>
        {
            new TextureOverlaySpec(OverlaySide.A, "oak", 100, 0, 800, 300),
            new TextureOverlaySpec(OverlaySide.A, "white", 1000, 2000, 800, 400),
        };

        var u = TextureOverlaySnap.NeighbourEdges(overlays, 0, (int)OverlaySide.A, FaceA, alongU: true);

        CollectionAssert.AreEquivalent(new[] { 1000, 1800 }, u,
            "соседка учитывается, даже когда по V области не пересекаются: на плоскости "
            + "выравнивание краёв по одной линии — такой же осмысленный жест, как встык, "
            + "в отличие от контакта двух коробок в ResizeSnap");
    }

    [Test]
    public void NeighbourEdges_SkipOverlayResolvedOffTheFace()
    {
        var overlays = new List<TextureOverlaySpec>
        {
            new TextureOverlaySpec(OverlaySide.A, "oak", 100, 200, 800, 600),
            new TextureOverlaySpec(OverlaySide.A, "white", 3000, 400, 800, 500),
        };
        Assume.That(overlays[1].Resolve(FaceA).width, Is.EqualTo(0),
            "проба должна быть вырожденной, иначе тест ничего не проверяет");

        var u = TextureOverlaySnap.NeighbourEdges(overlays, 0, (int)OverlaySide.A, FaceA, alongU: true);

        CollectionAssert.IsEmpty(u,
            "уехавшая с грани накладка рёбер не даёт: иначе оба её края слиплись бы в 3000 "
            + "и область прилипала бы к пустому месту у края грани");
    }

    [Test]
    public void ThresholdMM_IsTheSameSettingAsForParts_AndZeroWhenSnappingIsOff()
    {
        var settings = KitchenSettings.Instance;
        bool savedEnabled = settings.SnapEnabled;
        float savedThreshold = settings.SnapThreshold;
        try
        {
            settings.SnapEnabled = true;
            settings.SnapThreshold = 37f;
            Assert.AreEqual(37f, TextureOverlaySnap.ThresholdMM(), 0.0001f,
                "порог у накладок тот же, что у деталей: пользователь настраивает его один раз");

            settings.SnapEnabled = false;
            Assert.AreEqual(0f, TextureOverlaySnap.ThresholdMM(), 0.0001f,
                "выключенная привязка отдаёт нулевой порог, который Nearest трактует "
                + "как «прилипать не к чему»");
        }
        finally
        {
            settings.SnapEnabled = savedEnabled;
            settings.SnapThreshold = savedThreshold;
        }
    }
}

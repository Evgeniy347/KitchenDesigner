using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Мерцание (z-fighting) в сложенном диване: две грани в ОДНОЙ плоскости,
/// смотрящие в ОДНУ сторону. Короб лежит внутри сиденья, и его задняя стенка
/// стояла бы в той же плоскости z, что задняя грань сиденья, — ровно та пара,
/// которую ловит <see cref="CoplanarSurfaceDetector"/> на настоящем элементе
/// (<c>CoplanarSurfaceCoverageTests</c>, а он требует Unity). Здесь тот же сенсор
/// гоняется на быстром пути по тем же коробкам: <c>FurniturePartBox</c> даёт
/// габарит каждой детали, а короб — охватывающий габарит его панелей.</summary>
public class SofaCoplanarSurfaceTests
{
    private static (Vector3 centerMM, Vector3 sizeMM)[] FoldedPartsMM(Vector3Int dims, int seat,
        int radius, out string[] names)
    {
        var parts = new List<(Vector3, Vector3)>();
        var labels = new List<string>();

        void Add(FurniturePartBox box)
        {
            parts.Add((box.CentreMM, box.SizeMM));
            labels.Add(box.Name);
        }

        Add(SofaLayout.Seat(dims, seat, radius));
        Add(SofaLayout.Backrest(dims));
        foreach (var cushion in SofaLayout.Cushions(dims, seat)) Add(cushion);

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var panel in SofaBoxLayout.Panels(dims, seat, radius))
        {
            min = Vector3.Min(min, panel.CentreMM - panel.SizeMM * 0.5f);
            max = Vector3.Max(max, panel.CentreMM + panel.SizeMM * 0.5f);
        }

        parts.Add(((min + max) * 0.5f, max - min));
        labels.Add("SofaBox");

        names = labels.ToArray();
        return parts.ToArray();
    }

    [Test]
    public void FoldedSofa_HasNoTwoSurfacesFightingForTheSamePixel_AtTheDefaults()
    {
        var parts = FoldedPartsMM(new Vector3Int(SofaLayout.DefaultWidthMM,
                SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM),
            SofaLayout.DefaultSeatHeightMM, SofaLayout.DefaultCornerRadiusMM, out var names);

        var fights = CoplanarSurfaceDetector.Fights(parts, i => names[i]);

        Assert.IsEmpty(fights,
            "Две грани сложенного дивана смотрят в одну сторону из одной плоскости и "
            + "перекрываются площадью — вид сбоку или сзади мерцает. Найдено:\n"
            + string.Join("\n", fights));
    }

    [Test]
    public void FoldedSofa_HasNoFights_AtTheSeatHeightsAtBothEndsOfTheRange()
    {
        foreach (int seat in new[] { SofaLayout.MinSeatHeightMM, SofaLayout.MaxSeatHeightMM })
        {
            var parts = FoldedPartsMM(new Vector3Int(1800, SofaLayout.OverallHeightMM, 1100),
                seat, 100, out var names);

            var fights = CoplanarSurfaceDetector.Fights(parts, i => names[i]);

            Assert.IsEmpty(fights,
                "на краях диапазона высоты сиденья то же правило: короб не должен совпасть "
                + "ни с чем плоскостью. Высота сиденья " + seat + ". Найдено:\n"
                + string.Join("\n", fights));
        }
    }

    [Test]
    public void TheSensor_ActuallyFiresOnTheBoxWithoutTheSetback()
    {
        var dims = new Vector3Int(SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM,
            SofaLayout.DefaultDepthMM);
        var seat = SofaLayout.Seat(dims, SofaLayout.DefaultSeatHeightMM,
            SofaLayout.DefaultCornerRadiusMM);
        float seatRearZ = seat.CentreMM.z - seat.SizeMM.z * 0.5f;
        var flushBox = (centerMM: new Vector3(0f, -300f, seatRearZ + 250f),
            sizeMM: new Vector3(1900f, 150f, 500f));

        var fights = CoplanarSurfaceDetector.Fights(
            new (Vector3 centerMM, Vector3 sizeMM)[] { (seat.CentreMM, seat.SizeMM), flushBox },
            i => i == 0 ? "seat" : "box");

        Assert.IsNotEmpty(fights,
            "контроль сенсора: короб, прижатый задней стенкой вровень с задней гранью "
            + "сиденья, ОБЯЗАН находиться — иначе тесты выше зелены потому, что сенсор "
            + "ничего не видит, а не потому, что зазор есть");
    }
}

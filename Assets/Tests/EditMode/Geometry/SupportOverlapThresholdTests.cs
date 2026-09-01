using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SupportOverlapThresholdTests
{
    private const float MM = 0.001f;
    private const float Threshold = 50f * MM;
    private const float Tol = 0.001f;

    private static readonly Vector3 PlateSize = new Vector3(800f, 18f, 400f) * MM;

    private static readonly float RestingY = 9f * MM;

    private sealed class Plate : IPosedGeometry
    {
        public ElementGeometry At(Vector3 position)
            => ElementGeometry.Box("plate", position, PlateSize);
    }

    private static float OffsetForRatio(float ratio) => PlateSize.x * (1f - ratio);

    private static ElementGeometry SupportGeometry()
        => ElementGeometry.Box("support", new Vector3(0f, -RestingY, 0f), PlateSize);

    private static bool SnapSeatsThePlate(float ratio)
    {
        var start = new Vector3(OffsetForRatio(ratio), RestingY + 30f * MM, 0f);
        var result = SnapCore.TrySnap(new Plate(), new List<ElementGeometry> { SupportGeometry() },
            start, Threshold);
        return result.snapped && Mathf.Abs(result.position.y - RestingY) < Tol;
    }

    private static Vector3[] Corners(Vector3 center, Vector3 size)
    {
        var half = size * 0.5f;
        var verts = new Vector3[8];
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    verts[i++] = center + new Vector3(half.x * sx, half.y * sy, half.z * sz);
        return verts;
    }

    private static ValidationElement Element(string name, Vector3 center, ElementKind kind)
    {
        var geometry = ElementGeometry.Box(name, center, PlateSize);
        return new ValidationElement(geometry, Corners(center, PlateSize), kind,
            ValidationElement.NoGroup, null, Span.FromCenter(center.y, PlateSize.y),
            ValidationElement.NoIndex);
    }

    private static bool ValidationCallsThePlateSupported(float ratio)
    {
        var support = Element("support", new Vector3(0f, -RestingY, 0f),
            ElementKind.Anchor | ElementKind.FloorAnchor);
        var plate = Element("plate", new Vector3(OffsetForRatio(ratio), RestingY, 0f),
            ElementKind.None);

        var result = ValidationCore.Validate(new List<ValidationElement> { support, plate });
        return !result.Violations.Contains(1);
    }

    [Test]
    public void SnapAndConnectivity_AgreeOnEveryOverlapRatio_BecauseTheThresholdIsShared()
    {
        float t = Tolerance.MinSupportOverlap;
        var ratios = new[] { 1f, t + 0.3f, t + 0.15f, t + 0.02f, t - 0.02f, t - 0.15f, t * 0.5f };
        var disagreed = new List<string>();

        foreach (float ratio in ratios)
        {
            bool snaps = SnapSeatsThePlate(ratio);
            bool supported = ValidationCallsThePlateSupported(ratio);
            if (snaps != supported)
                disagreed.Add($"перекрытие {ratio:0.###}: снэп={snaps}, опора={supported}");
        }

        Assert.IsEmpty(disagreed,
            "порог опорного перекрытия ЕДИНЫЙ для снэпа и для связности. Разные пороги "
            + "дают полосу, в которой деталь прилипает, но тут же подсвечивается красным "
            + "как висящая в воздухе — ровно это было при 30% у снэпа и 50% у связности:\n"
            + string.Join("\n", disagreed));
    }

    [Test]
    public void OverlapAboveTheThreshold_Supports_AndBelowIt_DoesNot()
    {
        float t = Tolerance.MinSupportOverlap;

        Assert.IsTrue(SnapSeatsThePlate(t + 0.02f),
            "контроль сверху: перекрытие больше порога обязано сажать деталь на опору");
        Assert.IsTrue(ValidationCallsThePlateSupported(t + 0.02f),
            "контроль сверху: то же перекрытие обязано считаться опорой");
        Assert.IsFalse(SnapSeatsThePlate(t - 0.02f),
            "контроль снизу: перекрытие меньше порога опорой не считается — иначе "
            + "оба сравнения из предыдущего теста были бы согласны просто потому, "
            + "что всегда отвечают «да»");
        Assert.IsFalse(ValidationCallsThePlateSupported(t - 0.02f),
            "контроль снизу для связности");
    }

    [Test]
    public void UpDotThreshold_SeparatesANearVerticalAxisFromASlantedOne()
    {
        double fiveDegrees = System.Math.Cos(5.0 * System.Math.PI / 180.0);
        double fifteenDegrees = System.Math.Cos(15.0 * System.Math.PI / 180.0);

        Assert.Less(Tolerance.UpDotThreshold, 1f,
            "порог строго меньше единицы: иначе запасной up включался бы только для "
            + "ИДЕАЛЬНО вертикальной оси, а float такой не бывает");
        Assert.Greater((float)fiveDegrees, Tolerance.UpDotThreshold,
            "ось в 5° от вертикали считается коллинеарной up — LookRotation по ней "
            + "вырождается, нужен запасной up");
        Assert.Less((float)fifteenDegrees, Tolerance.UpDotThreshold,
            "ось в 15° от вертикали уже задаёт поворот сама, запасной up ей не нужен");
    }

    [Test]
    public void EpsilonSqr_IsTheSquareOfOneMillimetre()
    {
        double side = System.Math.Sqrt(Tolerance.EpsilonSqr);

        Assert.AreEqual(1f, (float)side / MM, 1e-3f,
            "EpsilonSqr сравнивают с sqrMagnitude ДО normalize: физически это «короче "
            + "миллиметра — не длина, а шум». Записан квадратом, чтобы не звать sqrt "
            + "на каждый кадр");
    }

    [Test]
    public void ClearanceMm_IsFinerThanTheMillimetreItGuards()
    {
        Assert.Less(Tolerance.ClearanceMm, 1f,
            "допуск на зазор мельче миллиметра, в котором зазоры и задаются: допуск "
            + "в целый миллиметр проглотил бы целую ступень допускового контроля");
        Assert.Greater(Tolerance.ClearanceMm, 0f,
            "нулевой допуск ловил бы float-шум как выход за допуск");
    }
}

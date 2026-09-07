using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Опора, которую тянут ПОПЕРЁК детали от края до края, обязана
/// предлагать пять посадок, а не одну середину.
///
/// Свип: опора едет вдоль оси хозяина шагами по 1 мм и на каждом шаге
/// спрашивает ядро, куда бы её посадили. Ответы схлопываются в полосы, и
/// падение печатает НАБОР координат, которые ядро на самом деле предложило,
/// против ожидаемых пяти. До правки набор состоял из одного нуля: центровка
/// была единственным детентом опоры (SnapCandidateCollector.AddCentringContact
/// звал EdgeDetents.CentreDelta по обеим осям).
///
/// Шаг намеренно сдвинут на 0,3 мм: детент, на котором опора УЖЕ стоит, даёт
/// нулевой сдвиг, и ядро правильно не предлагает ничего. Сетка из круглых
/// миллиметров попадала бы ровно в −190, 0 и +190, и три посадки из пяти
/// пропали бы из наблюдений — тест бы врал про собственный код.</summary>
public class ScrewLegMountDetentSweepTests : SnapCoreTestBase
{
    /// <summary>60 мм от кромки — то же число, что
    /// <c>ScrewLegSpec.MOUNT_DETENT_FROM_EDGE_MM</c>. Ядро констант из Pure не
    /// видит (Pure ссылается на Geometry, не наоборот), поэтому опора приносит
    /// его в снимок сама; что она приносит именно это число, стережёт
    /// ScrewLegHostSectionTests в Unity-наборе.</summary>
    private const float FromEdgeMM = 60f;

    private const float SideBottomY = 150f * MM;
    private const int SideDepthMM = 500;
    private const float LegHalfY = 29f * MM;
    private const float LegHalfZ = 12.5f * MM;

    private static ElementGeometry Side() =>
        At(Make("Side", new Vector3Int(16, 700, SideDepthMM)),
            new Vector3(0f, SideBottomY + 350f * MM, 0f));

    private static Box Leg() =>
        new Box("Leg", new Vector3Int(25, 58, 25), null, false, Vector3.up, FromEdgeMM * MM);

    private static Vector3 LegAt(float zMM) =>
        new Vector3(0f, SideBottomY - LegHalfY, zMM * MM);

    private static float HalfDepth => SideDepthMM * 0.5f * MM;

    private static readonly float[] ExpectedMM =
    {
        -(HalfDepthMM - 12.5f),
        -(HalfDepthMM - FromEdgeMM),
        0f,
        HalfDepthMM - FromEdgeMM,
        HalfDepthMM - 12.5f,
    };

    private const float HalfDepthMM = SideDepthMM * 0.5f;

    private static string Mm(float value) =>
        value.ToString("F1", CultureInfo.InvariantCulture);

    [Test]
    public void DraggedAcrossTheSide_TheLegOffersFiveSeats_NotOnlyTheCentre()
    {
        var seen = new List<float>();
        var bands = new List<string>();
        float bandStart = 0f;
        float bandSeat = float.NaN;
        int bandSteps = 0;

        for (float z = -300f + 0.3f; z <= 300f; z += 1f)
        {
            var r = Snap(Leg(), Side(), LegAt(z));
            float seat = r.snapped ? Mathf.Round(r.position.z / MM * 10f) / 10f : float.NaN;

            if (!seat.Equals(bandSeat))
            {
                if (bandSteps > 0 && !float.IsNaN(bandSeat))
                    bands.Add($"{Mm(bandStart)}..{Mm(z - 1f)} → {Mm(bandSeat)} ({bandSteps} шагов)");
                bandSeat = seat;
                bandStart = z;
                bandSteps = 0;
            }
            bandSteps++;

            if (!float.IsNaN(seat) && !seen.Contains(seat)) seen.Add(seat);
        }
        if (bandSteps > 0 && !float.IsNaN(bandSeat))
            bands.Add($"{Mm(bandStart)}.. → {Mm(bandSeat)} ({bandSteps} шагов)");

        seen.Sort();
        var expected = new List<float>(ExpectedMM);
        expected.Sort();

        CollectionAssert.AreEqual(expected, seen,
            "тянем опору поперёк бока 500 мм и смотрим, куда ядро её сажает. Ожидаются пять "
            + "посадок: ребро опоры к кромке (" + Mm(ExpectedMM[0]) + "), "
            + Mm(FromEdgeMM) + " мм от кромки (" + Mm(ExpectedMM[1]) + "), центр (0,0), "
            + "и то же самое с другой стороны. Наблюдалось: ["
            + string.Join("; ", seen.ConvertAll(Mm)) + "]. Полосы: " + string.Join(" | ", bands));
    }

    [Test]
    public void TheLegEdge_MeetsTheEdgeOfTheSide_WithTheWholeLegInside()
    {
        var r = Snap(Leg(), Side(), LegAt(-244f));

        Assert.IsTrue(r.snapped, "кромка детали в шести с половиной миллиметрах — это в пределах порога");
        Assert.AreEqual(-(HalfDepth - LegHalfZ), r.position.z, Tol,
            "первая посадка — ребро опоры вровень с кромкой детали, опора целиком внутри: "
            + "центр стоит на 12,5 мм внутрь от кромки, а не НА кромке");
    }

    [Test]
    public void SixtyMillimetresFromTheEdge_IsASeatOfItsOwn()
    {
        var r = Snap(Leg(), Side(), LegAt(-196f));

        Assert.IsTrue(r.snapped, "шесть миллиметров до посадки — в пределах порога");
        Assert.AreEqual(-(HalfDepth - FromEdgeMM * MM), r.position.z, Tol,
            "вторая посадка — центр опоры в 60 мм от кромки детали. Если тут окажется 0, "
            + "значит опоре снова доступна одна середина");
    }

    [Test]
    public void BetweenTheSeats_TheLegIsFree()
    {
        var r = Snap(Leg(), Side(), LegAt(-120f));

        Assert.IsFalse(r.snapped,
            "между −190 и 0 нет ни одной посадки ближе 50 мм: посадка — это детент, "
            + "а не магнит, иначе опору нельзя поставить, куда хочет человек");
    }

    /// <summary>Обратная сторона правки: на торце тоньше самой опоры кромочные
    /// детенты обязаны исчезнуть, иначе футорка выйдет за пласть. Тот же бок,
    /// но ось X — 16 мм против Ø25 опоры.</summary>
    [Test]
    public void AcrossTheSixteenMillimetreThickness_OnlyTheCentreIsOffered()
    {
        var r = Snap(Leg(), Side(),
            new Vector3(4f * MM, SideBottomY - LegHalfY, 0f));

        Assert.IsTrue(r.snapped, "торец в четырёх миллиметрах — в пределах порога");
        Assert.AreEqual(0f, r.position.x, Tol,
            "поперёк 16 мм опора Ø25 не помещается, и кромочные детенты для этой оси "
            + "не заводятся: остаётся только середина");
    }
}

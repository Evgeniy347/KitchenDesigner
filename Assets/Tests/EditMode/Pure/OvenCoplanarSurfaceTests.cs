using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Мерцание на лице дверцы духовки (z-fighting) — это не шейдер и не
/// камера, а две грани в ОДНОЙ плоскости, смотрящие в ОДНУ сторону: плита
/// дверцы 19.5 мм и накладки (стекло, панель управления) 2 мм кончались на
/// z = 567.5/2 обе, и растеризатор выбирал победителя по округлению глубины.
///
/// Сенсор смотрит на закрытую позу целиком — корпус плюс дверца — и ищет любую
/// такую пару. Совпадающие грани с ПРОТИВОПОЛОЖНЫМИ нормалями (низ одной
/// коробки на верху другой) законны: отсечение задних граней рисует ровно одну
/// из них, и такие пары сенсор пропускает.
///
/// Чего сенсор НЕ видит: открытую дверцу (её грани уезжают поворотом), соседние
/// ЭЛЕМЕНТЫ сцены, прижатые к духовке вплотную, и мерцание от настроек камеры
/// (near/far) — там своя причина и свой сенсор.</summary>
public class OvenCoplanarSurfaceTests
{
    /// <summary>Порог: грани ближе половины миллиметра друг к другу дерутся за
    /// пиксель на любой разумной дальности камеры. Починка развела их на 2 мм —
    /// на толщину накладки, а не на «лишь бы не мерцало».</summary>
    private const float MIN_SEPARATION_MM = 0.5f;

    private const float AREA_EPS_MM = 0.01f;

    private static List<string> Fights(
        (Vector3 centerMM, Vector3 sizeMM)[] parts, System.Func<int, string> nameOf,
        float minSeparationMM)
    {
        var found = new List<string>();
        for (int i = 0; i < parts.Length; i++)
            for (int j = i + 1; j < parts.Length; j++)
                for (int axis = 0; axis < 3; axis++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float pi = Plane(parts[i], axis, side);
                        float pj = Plane(parts[j], axis, side);
                        if (Mathf.Abs(pi - pj) >= minSeparationMM) continue;
                        if (!OverlapsAcross(parts[i], parts[j], axis)) continue;

                        found.Add(nameOf(i) + " и " + nameOf(j) + ": грани по "
                            + "XYZ"[axis] + (side > 0 ? "+" : "-") + " на "
                            + pi.ToString("0.###") + " и " + pj.ToString("0.###")
                            + " мм — расходятся на " + Mathf.Abs(pi - pj).ToString("0.###"));
                    }
        return found;
    }

    private static float Plane((Vector3 centerMM, Vector3 sizeMM) part, int axis, int side) =>
        part.centerMM[axis] + side * part.sizeMM[axis] * 0.5f;

    private static bool OverlapsAcross(
        (Vector3 centerMM, Vector3 sizeMM) a, (Vector3 centerMM, Vector3 sizeMM) b, int axis)
    {
        for (int k = 0; k < 3; k++)
        {
            if (k == axis) continue;
            float lo = Mathf.Max(a.centerMM[k] - a.sizeMM[k] * 0.5f,
                b.centerMM[k] - b.sizeMM[k] * 0.5f);
            float hi = Mathf.Min(a.centerMM[k] + a.sizeMM[k] * 0.5f,
                b.centerMM[k] + b.sizeMM[k] * 0.5f);
            if (hi - lo <= AREA_EPS_MM) return false;
        }
        return true;
    }

    [Test]
    public void Oven_ClosedPose_HasNoTwoSurfacesFightingForTheSamePixel()
    {
        var fights = Fights(OvenBody.ClosedPartsMM(), OvenBody.PartName, MIN_SEPARATION_MM);

        Assert.IsEmpty(fights,
            "Две грани духовки смотрят в одну сторону из одной плоскости и перекрываются "
            + "площадью — пользователь видит мерцание (z-fighting) на этом месте. Разведите "
            + "их на осмысленную величину (толщину детали или щель) или уберите лишнюю "
            + "поверхность; сдвиг «на 0.1 мм чтобы не мерцало» — не починка. Найдено:\n"
            + string.Join("\n", fights));
    }

    /// <summary>Положительный контроль: без него зелёный сенсор выше не означает
    /// ничего — сломанный детектор молчит точно так же, как исправная геометрия.</summary>
    [Test]
    public void Detector_TwoBoxesSharingAFrontFace_ReportsThem()
    {
        var flush = new[]
        {
            (new Vector3(0f, 0f, -10f), new Vector3(100f, 100f, 20f)),
            (new Vector3(0f, 0f, -1f), new Vector3(50f, 50f, 2f)),
        };

        var fights = Fights(flush, i => i == 0 ? "плита" : "накладка", MIN_SEPARATION_MM);

        Assert.IsNotEmpty(fights, "две коробки с общим лицом на z = 0 обязаны быть найдены");
    }

    /// <summary>Второй контроль, с другой стороны: соприкосновение коробок —
    /// норма, и сенсор не имеет права краснеть на нём, иначе его отключат.</summary>
    [Test]
    public void Detector_BoxStackedOnAnother_IsNotAFight()
    {
        var stacked = new[]
        {
            (new Vector3(0f, 0f, 0f), new Vector3(100f, 20f, 100f)),
            (new Vector3(0f, 20f, 0f), new Vector3(100f, 20f, 100f)),
        };

        var fights = Fights(stacked, i => "коробка" + i, MIN_SEPARATION_MM);

        Assert.IsEmpty(fights,
            "у стыка нормали противоположны, отсечение задних граней рисует одну: "
            + string.Join("\n", fights));
    }

    [Test]
    public void OvenFacade_KeepsItsFrameVisible_BehindTheOverlays()
    {
        var door = OvenBody.DoorPartsMM();
        var slab = door[0];
        var glass = door[1];

        float slabFront = slab.centerMM.z + slab.sizeMM.z * 0.5f;
        float glassFront = glass.centerMM.z + glass.sizeMM.z * 0.5f;

        Assert.AreEqual(OvenBody.OVERLAY_THICKNESS_MM, glassFront - slabFront, 0.001f,
            "накладка стоит ПЕРЕД плитой ровно на свою толщину: это и есть зазор, "
            + "который снял мерцание");
        Assert.AreEqual(OvenBody.FACADE_THICKNESS_MM,
            glassFront - (slab.centerMM.z - slab.sizeMM.z * 0.5f), 0.001f,
            "общая толщина дверцы не изменилась — 19.5 мм, как у производителя");
    }
}

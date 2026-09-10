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
    [Test]
    public void Oven_ClosedPose_HasNoTwoSurfacesFightingForTheSamePixel()
    {
        var fights = CoplanarSurfaceDetector.Fights(OvenBody.ClosedPartsMM(), OvenBody.PartName);

        Assert.IsEmpty(fights,
            "Две грани духовки смотрят в одну сторону из одной плоскости и перекрываются "
            + "площадью — пользователь видит мерцание (z-fighting) на этом месте. Разведите "
            + "их на осмысленную величину (толщину детали или щель) или уберите лишнюю "
            + "поверхность; сдвиг «на 0.1 мм чтобы не мерцало» — не починка. Найдено:\n"
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

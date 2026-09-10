using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Тот же дефект, что и у духовки (`AGENTS.md` → «Накладка, лежащая на
/// детали, не имеет права кончаться в её плоскости»), нашёлся в дверце
/// посудомойки: плита двери 20 мм и накладка панели управления 2 мм кончались
/// на одной z = halfD обе. Починка — как у духовки: плита утоплена на толщину
/// накладки (`DishwasherBody.DOOR_SLAB_THICKNESS_MM`), общая толщина двери не
/// изменилась.</summary>
public class DishwasherCoplanarSurfaceTests
{
    [Test]
    public void Dishwasher_ClosedPose_HasNoTwoSurfacesFightingForTheSamePixel()
    {
        var fights = CoplanarSurfaceDetector.Fights(
            DishwasherBody.ClosedPartsMM(), DishwasherBody.ChildName);

        Assert.IsEmpty(fights,
            "Две грани посудомойки смотрят в одну сторону из одной плоскости и "
            + "перекрываются площадью — пользователь увидит мерцание (z-fighting) на "
            + "этом месте. Найдено:\n" + string.Join("\n", fights));
    }

    [Test]
    public void DishwasherDoor_KeepsItsSlabVisible_BehindTheControlPanel()
    {
        var door = DishwasherBody.DoorPartsMM();
        var slab = door[0];
        var panel = door[1];

        float slabFront = slab.centerMM.z + slab.sizeMM.z * 0.5f;
        float panelFront = panel.centerMM.z + panel.sizeMM.z * 0.5f;

        Assert.AreEqual(DishwasherBody.OVERLAY_THICKNESS_MM, panelFront - slabFront, 0.001f,
            "накладка панели управления стоит ПЕРЕД плитой двери ровно на свою толщину");
        Assert.AreEqual(DishwasherBody.DOOR_THICKNESS_MM,
            panelFront - (slab.centerMM.z - slab.sizeMM.z * 0.5f), 0.001f,
            "общая толщина двери не изменилась — 20 мм");
    }
}

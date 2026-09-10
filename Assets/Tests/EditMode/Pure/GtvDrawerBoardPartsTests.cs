using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Единственные детали ящика GTV, реально выпиленные из листа — дно и задняя стенка.
/// Размеры обязаны зеркалить <see cref="DrawerMesh.ComputeBoxes"/> (те же вставки и толщина
/// панели), иначе цифры в спецификации разойдутся с тем, что реально построено на сцене.</summary>
public class GtvDrawerBoardPartsTests
{
    [Test]
    public void BottomDimsMM_SubtractsInsetsAndUsesPanelThickness()
    {
        var dims = GtvDrawerBoardParts.BottomDimsMM(lwMM: 500, nlMM: 400);

        Assert.AreEqual(500 - DrawerConstants.BOTTOM_WIDTH_INSET, dims.x);
        Assert.AreEqual(DrawerConstants.PANEL_THICKNESS, dims.y);
        Assert.AreEqual(400 - DrawerConstants.BOTTOM_DEPTH_INSET, dims.z);
    }

    /// <summary>Противоположный вход: другая ширина/длина ящика даёт другие размеры дна —
    /// формула не возвращает константу, независимую от входа.</summary>
    [Test]
    public void BottomDimsMM_DifferentInput_DifferentOutput()
    {
        var a = GtvDrawerBoardParts.BottomDimsMM(400, 350);
        var b = GtvDrawerBoardParts.BottomDimsMM(600, 500);

        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void BackDimsMM_UsesTypeBackHeightAndPanelThickness()
    {
        var dims = GtvDrawerBoardParts.BackDimsMM(lwMM: 500, DrawerType.B);

        Assert.AreEqual(500 - DrawerConstants.BACK_WIDTH_INSET, dims.x);
        Assert.AreEqual(DrawerConstants.GetBackHeight(DrawerType.B), dims.y);
        Assert.AreEqual(DrawerConstants.PANEL_THICKNESS, dims.z);
    }

    /// <summary>Противоположный вход: другой тип (высота ящика) обязан дать другую высоту
    /// задней стенки — таблица `GetBackHeight` разная по типам A/B/C/D.</summary>
    [Test]
    public void BackDimsMM_DifferentType_DifferentHeight()
    {
        var low = GtvDrawerBoardParts.BackDimsMM(500, DrawerType.A);
        var tall = GtvDrawerBoardParts.BackDimsMM(500, DrawerType.D);

        Assert.AreNotEqual(low.y, tall.y);
    }
}

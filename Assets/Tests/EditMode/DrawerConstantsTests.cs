using NUnit.Framework;
using KitchenDesigner.Core;

public class DrawerConstantsTests
{
    [Test]
    public void ValidLengths_HasEightEntries()
    {
        Assert.AreEqual(8, DrawerConstants.ValidLengths.Length);
    }

    [Test]
    public void ValidLengths_ContainsAllExpected()
    {
        var expected = new[] { 250, 300, 350, 400, 450, 500, 550, 600 };
        Assert.AreEqual(expected.Length, DrawerConstants.ValidLengths.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], DrawerConstants.ValidLengths[i]);
    }

    [Test]
    public void IsValidLength_ReturnsTrue_ForAllValid()
    {
        foreach (var len in DrawerConstants.ValidLengths)
            Assert.IsTrue(DrawerConstants.IsValidLength(len), $"len={len} should be valid");
    }

    [Test]
    public void IsValidLength_ReturnsFalse_ForInvalid()
    {
        var invalid = new[] { 200, 625, 700, 0, -1 };
        foreach (var len in invalid)
            Assert.IsFalse(DrawerConstants.IsValidLength(len), $"len={len} should be invalid");
    }

    [Test]
    public void GetTypeHeight_A_Returns86()
    {
        Assert.AreEqual(86, DrawerConstants.GetTypeHeight(DrawerType.A));
    }

    [Test]
    public void GetTypeHeight_B_Returns120()
    {
        Assert.AreEqual(120, DrawerConstants.GetTypeHeight(DrawerType.B));
    }

    [Test]
    public void GetTypeHeight_C_Returns168()
    {
        Assert.AreEqual(168, DrawerConstants.GetTypeHeight(DrawerType.C));
    }

    [Test]
    public void GetTypeHeight_D_Returns200()
    {
        Assert.AreEqual(200, DrawerConstants.GetTypeHeight(DrawerType.D));
    }

    [Test]
    public void GetTypeLabel_ReturnsCorrect()
    {
        var types = new[] { DrawerType.A, DrawerType.B, DrawerType.C, DrawerType.D };
        foreach (var t in types)
            Assert.IsNotEmpty(DrawerConstants.GetTypeLabel(t), $"type={t} label should not be empty");
    }

    [Test]
    public void GetColorName_Anthracite()
    {
        Assert.AreEqual("Антрацит", DrawerConstants.GetColorName(DrawerColor.Anthracite));
    }

    [Test]
    public void GetColorName_White()
    {
        Assert.AreEqual("Белый", DrawerConstants.GetColorName(DrawerColor.White));
    }

    [Test]
    public void GetColorName_Black()
    {
        Assert.AreEqual("Чёрный", DrawerConstants.GetColorName(DrawerColor.Black));
    }

    [Test]
    public void AllColors_HaveUniqueMaterialIds()
    {
        var a = DrawerConstants.GetColorMaterialId(DrawerColor.Anthracite);
        var w = DrawerConstants.GetColorMaterialId(DrawerColor.White);
        var b = DrawerConstants.GetColorMaterialId(DrawerColor.Black);
        Assert.AreNotEqual(a, w);
        Assert.AreNotEqual(a, b);
        Assert.AreNotEqual(w, b);
    }

    [Test]
    public void GetCycleButtonLabel_Closed()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.Closed);
        StringAssert.Contains("Открыть", label);
    }

    [Test]
    public void GetCycleButtonLabel_BothOpen()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.BothOpen);
        StringAssert.Contains("верхний", label);
    }

    [Test]
    public void GetCycleButtonLabel_LowerOnly()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.LowerOnly);
        StringAssert.Contains("всё", label);
    }

    [Test]
    public void NextCycleState_Closed_ReturnsBothOpen()
    {
        Assert.AreEqual(DoubleDrawerState.BothOpen, DrawerConstants.NextCycleState(DoubleDrawerState.Closed));
    }

    [Test]
    public void NextCycleState_BothOpen_ReturnsLowerOnly()
    {
        Assert.AreEqual(DoubleDrawerState.LowerOnly, DrawerConstants.NextCycleState(DoubleDrawerState.BothOpen));
    }

    [Test]
    public void NextCycleState_LowerOnly_ReturnsClosed()
    {
        Assert.AreEqual(DoubleDrawerState.Closed, DrawerConstants.NextCycleState(DoubleDrawerState.LowerOnly));
    }

    [Test]
    public void CycleCompletesInThreeSteps()
    {
        var state = DoubleDrawerState.Closed;
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.BothOpen, state);
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.LowerOnly, state);
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.Closed, state);
    }

    [Test]
    public void DrawerTypeEnum_A_Equals86()
    {
        Assert.AreEqual(86, (int)DrawerType.A);
    }

    [Test]
    public void DrawerTypeEnum_D_Equals200()
    {
        Assert.AreEqual(200, (int)DrawerType.D);
    }
}

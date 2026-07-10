using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class GridManagerTests
{
    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 16;
        KitchenSettings.Instance.GridEnabled = true;
    }

    [Test]
    public void SnapToGrid_17RoundsTo16()
    {
        Vector3 result = GridManager.SnapToGrid(new Vector3(0.017f, 0, 0));
        Assert.AreEqual(0.016f, result.x, 0.0001f);
    }

    [Test]
    public void SnapToGrid_9RoundsTo16()
    {
        Vector3 result = GridManager.SnapToGrid(new Vector3(0.009f, 0, 0));
        Assert.AreEqual(0.016f, result.x, 0.0001f);
    }

    [Test]
    public void SnapToGrid_0Stays0()
    {
        Vector3 result = GridManager.SnapToGrid(Vector3.zero);
        Assert.AreEqual(Vector3.zero, result);
    }

    [Test]
    public void SnapToGrid_DisabledReturnsInput()
    {
        KitchenSettings.Instance.GridEnabled = false;
        Vector3 input = new Vector3(0.025f, 0.037f, 0.012f);
        Vector3 result = GridManager.SnapToGrid(input);
        Assert.AreEqual(input, result);
    }
}

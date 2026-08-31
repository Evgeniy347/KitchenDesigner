using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class BasePlateTests
{
    [Test]
    public void BasePlate_CreatesWithCorrectSize()
    {
        var plate = BasePlate.Create();
        Assert.NotNull(plate);
        Assert.NotNull(plate.Element);

        var element = plate.Element;
        Assert.AreEqual(new Vector3Int(3000, 18, 3000), element.DimensionsMM);

        var scale = plate.transform.localScale;
        Assert.AreEqual(3f, scale.x, 0.001f);
        Assert.AreEqual(0.018f, scale.y, 0.001f);
        Assert.AreEqual(3f, scale.z, 0.001f);

        Object.DestroyImmediate(plate.gameObject);
    }
}

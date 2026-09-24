using NUnit.Framework;
using KitchenDesigner.Core.UI;

public class LoadWindowLayoutTests
{
    [Test]
    public void HeightFor_UsesThePreferredHeight_OnALargeScreen()
    {
        Assert.AreEqual(480f, LoadWindowLayout.HeightFor(480f, 1080f));
    }

    [Test]
    public void HeightFor_ClampsToHalfTheScreen_OnASmallScreen()
    {
        Assert.AreEqual(300f, LoadWindowLayout.HeightFor(480f, 600f));
    }

    [Test]
    public void HeightFor_NeverGoesBelowTheMinimum_OnATinyScreen()
    {
        Assert.AreEqual(LoadWindowLayout.MinHeight, LoadWindowLayout.HeightFor(480f, 100f));
    }

    [Test]
    public void HeightFor_ResultIsAtMostHalfTheScreen_AcrossASweep()
    {
        for (float screen = 200f; screen <= 4000f; screen += 137f)
        {
            float h = LoadWindowLayout.HeightFor(480f, screen);
            float cap = System.Math.Max(screen * LoadWindowLayout.MaxScreenHeightFraction,
                LoadWindowLayout.MinHeight);
            Assert.LessOrEqual(h, cap + 0.001f, $"screen={screen}");
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Общий кэш материалов техники. Раньше каждый прибор держал свою пару
/// «статическое поле + ленивый геттер», и над ними стояло предупреждение про
/// `== null` вместо `??=`. Предупреждение переехало сюда тестом.
/// </summary>
public class ApplianceMaterialsTests
{
    [Test]
    public void Cached_DestroyedMaterial_IsRebuilt_NotHandedOutAsAGhost()
    {
        Material? slot = null;
        var first = ApplianceMaterials.Cached(ref slot, Color.red, 0.1f, 0.2f);
        Assert.IsNotNull(first);

        Object.DestroyImmediate(first);

        var second = ApplianceMaterials.Cached(ref slot, Color.red, 0.1f, 0.2f);
        Assert.IsTrue(second != null,
            "у UnityEngine.Object есть состояние «уничтожен, но ссылка не null»: `??=` его не видит "
            + "и вернул бы призрак, поэтому кэш обязан сравнивать через `== null`");
        Assert.AreNotSame(first, second);

        Object.DestroyImmediate(second);
    }

    [Test]
    public void Cached_SecondCall_ReturnsTheSameInstance()
    {
        Material? slot = null;
        var first = ApplianceMaterials.Cached(ref slot, Color.green, 0f, 0f);
        var second = ApplianceMaterials.Cached(ref slot, Color.green, 0f, 0f);
        Assert.AreSame(first, second, "материал общий: пересборка геометрии не должна плодить копии");
        Object.DestroyImmediate(first);
    }

    [Test]
    public void CooktopDecor_IsLighterThanTheGlass_OrItIsInvisibleOnBlack()
    {
        float glass = ApplianceMaterials.CooktopGlass.color.grayscale;
        float decor = ApplianceMaterials.CooktopDecor.color.grayscale;
        Assert.Greater(decor, glass,
            "конфорки и панель управления рисуются поверх чёрного стекла: сравняй их по яркости — "
            + "и рисунка прибора не видно вовсе");
    }

    [Test]
    public void SinkBowlBottom_IsDarkerThanTheSteel_OrTheBowlReadsAsAFlatFill()
    {
        float steel = ApplianceMaterials.SinkSteel.color.grayscale;
        float bottom = ApplianceMaterials.SinkBowlBottom.color.grayscale;
        Assert.Less(bottom, steel,
            "дно чаши чуть темнее стенок — иначе чаша читается плоской заливкой");
    }
}

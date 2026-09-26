using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Ventilation;

/// <summary>Ведомость (V5c): лист металла на воздуховод считается формулой самого меша
/// (BoxRunMesh для прямоугольного профиля, боковая площадь цилиндра для круглого) — тест не
/// пересчитывает площадь вручную, а сверяет с той же формулой производственного кода, чтобы
/// не быть тестом самого себя (conventions/TEST-NAMING.md — "тест, который пересчитывает
/// формулу").</summary>
public class DuctSpecItemsTests
{
    [Test]
    public void SheetMetalAreaM2_Round_MatchesCircumferenceTimesLength()
    {
        var profile = DuctProfile.Round(200);
        float expected = (float)System.Math.PI * 200f * 1250f * 1e-6f;
        Assert.AreEqual(expected, DuctSpecItems.SheetMetalAreaM2(profile, 1250f), 1e-4f);
    }

    [Test]
    public void SheetMetalAreaM2_Rect_MatchesBoxRunMeshUnfoldedArea()
    {
        var profile = DuctProfile.Rect(200, 150);
        var from = new UnityEngine.Vector3(0f, 0f, 0f);
        var to = new UnityEngine.Vector3(0f, 1250f, 0f);
        float expected = BoxRunMesh.UnfoldedAreaM2(from, to, 200f, 150f);

        Assert.AreEqual(expected, DuctSpecItems.SheetMetalAreaM2(profile, 1250f), 1e-6f);
    }

    [Test]
    public void SheetMetalAreaM2_ZeroLength_IsZero()
    {
        Assert.AreEqual(0f, DuctSpecItems.SheetMetalAreaM2(DuctProfile.Round(200), 0f));
    }

    [Test]
    public void DuctLine_UsesTheStructuresSection_AndTheProfileIdAsMaterial()
    {
        var profile = DuctProfile.Rect(200, 150);
        var item = DuctSpecItems.DuctLine(profile, 1250f);

        Assert.AreEqual(SpecSections.Structures, item.section);
        Assert.AreEqual(profile.ProfileId, item.material);
        Assert.AreEqual(SpecUnit.AreaM2, item.unit);
    }

    [Test]
    public void GrilleLine_IsOnePieceNamedByItsSize()
    {
        var item = DuctSpecItems.GrilleLine(150, 200);

        Assert.AreEqual(SpecUnit.Pieces, item.unit);
        Assert.AreEqual(1f, item.qty);
        Assert.AreEqual("150x200", item.material);
    }
}

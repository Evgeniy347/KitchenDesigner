using System;
using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>Сечение воздуховода: круг Ø100…315 (диапазон решён в 3.5, не наш домысел) или
/// прямоугольник w×h. ProfileId — строка для сверки устий (PortJoint.ProfilesCompatible,
/// V5a): порядок w×h в ней НЕ нормализуется, иначе 100×200 и 200×100 стали бы неотличимы,
/// а физически это разные устья.</summary>
public class DuctProfileTests
{
    [Test]
    public void Round_AcceptsTheDocumentedRange()
    {
        Assert.AreEqual(100, DuctProfile.Round(100).DiameterMm);
        Assert.AreEqual(315, DuctProfile.Round(315).DiameterMm);
    }

    [Test]
    public void Round_RejectsBelowTheDocumentedMinimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DuctProfile.Round(99));
    }

    [Test]
    public void Round_RejectsAboveTheDocumentedMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DuctProfile.Round(316));
    }

    [Test]
    public void Rect_RejectsNonPositiveWidthOrHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DuctProfile.Rect(0, 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => DuctProfile.Rect(200, 0));
    }

    [Test]
    public void ProfileId_Distinguishes100x200From200x100()
    {
        Assert.AreNotEqual(DuctProfile.Rect(100, 200).ProfileId, DuctProfile.Rect(200, 100).ProfileId,
            "100×200 и 200×100 — разные устья: профиль не поворачивается сам собой");
    }

    [Test]
    public void ProfileId_IsStableForTheSameRect()
    {
        Assert.AreEqual(DuctProfile.Rect(100, 200).ProfileId, DuctProfile.Rect(100, 200).ProfileId);
    }

    [Test]
    public void ProfileId_DistinguishesRoundFromRect_AtTheSameNominalNumber()
    {
        Assert.AreNotEqual(DuctProfile.Round(200).ProfileId, DuctProfile.Rect(200, 200).ProfileId);
    }
}

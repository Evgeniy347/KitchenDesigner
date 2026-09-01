using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Арифметика винтовой опоры с футоркой.
///
/// Каталожная железка — это M6x50 с пяткой Ø25x8. Из ТЗ: до пола тянется ДЛИНА
/// РЕЗЬБЫ, а не отдельный габарит «высота опоры», поэтому высота над полом —
/// производная: резьба минус то, что ушло в корпус, плюс пятка. При значениях по
/// умолчанию это 50 − 25 + 8 = 33 мм, и именно эта тройка чисел ломается первой,
/// если кто-нибудь решит, что «высота» и «длина резьбы» — одно и то же.</summary>
public class ScrewLegSpecTests
{
    [Test]
    public void DefaultLeg_StandsThirtyThreeMillimetresOffTheFloor()
    {
        Assert.AreEqual(33, ScrewLegSpec.HeightAboveFloorMM(
            ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
            ScrewLegSpec.DEFAULT_INSERTION_MM,
            ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM));
    }

    [Test]
    public void BodyHeight_CountsTheWholeRod_IncludingThePartInsideThePart()
    {
        Assert.AreEqual(58, ScrewLegSpec.BodyHeightMM(50, 8),
            "габарит опоры меряется по мешу: пятка плюс ВСЯ резьба, включая ту "
            + "четверть, что сидит в детали — иначе торчащий насквозь конец "
            + "окажется вне коробки и пройдёт сквозь полку молча");
    }

    [Test]
    public void ThreadLengthForHeight_IsTheInverseOfHeightAboveFloor()
    {
        foreach (int height in new[] { 33, 60, 100, 150 })
        {
            int thread = ScrewLegSpec.ThreadLengthForHeightMM(height, 25, 8);
            Assert.AreEqual(height, ScrewLegSpec.HeightAboveFloorMM(thread, 25, 8),
                $"высота {height} мм не пережила пересчёт в длину резьбы и обратно");
        }
    }

    [Test]
    public void ThreadLengthForHeight_ClampsInsteadOfGoingNegative()
    {
        Assert.AreEqual(ScrewLegSpec.MIN_THREAD_LENGTH_MM,
            ScrewLegSpec.ThreadLengthForHeightMM(-500, 25, 8),
            "опора под потолком (пол выше точки крепления) не должна давать "
            + "отрицательную резьбу — кламп, а не мусор");
    }

    [Test]
    public void Insertion_NeverExceedsTheThreadItself()
    {
        Assert.AreEqual(20, ScrewLegSpec.ClampInsertionMM(25, 20),
            "заход глубже самой резьбы физически невозможен");
        Assert.AreEqual(ScrewLegSpec.MIN_INSERTION_MM, ScrewLegSpec.ClampInsertionMM(0, 50));
    }

    [Test]
    public void Protrusion_IsWhatSticksOutTheOtherSideOfAThinBoard()
    {
        Assert.AreEqual(9, ScrewLegSpec.ProtrusionMM(25, 16),
            "в торец 16 мм заход 25 мм выходит наружу на 9 мм — это норма, "
            + "а не ошибка коллизии");
        Assert.AreEqual(7, ScrewLegSpec.ProtrusionMM(25, 18));
        Assert.AreEqual(0, ScrewLegSpec.ProtrusionMM(25, 40),
            "в толстую деталь резьба уходит целиком и наружу не выходит");
    }

    [Test]
    public void Thread_FallsBackToM6_ForAnythingUnknown()
    {
        Assert.AreEqual(ScrewLegSpec.ThreadM6, ScrewLegSpec.NormalizeThread("мусор"));
        Assert.AreEqual(ScrewLegSpec.ThreadM6, ScrewLegSpec.NormalizeThread(null));
        Assert.AreEqual(ScrewLegSpec.ThreadM8, ScrewLegSpec.NormalizeThread("m8"),
            "регистр в обозначении резьбы значения не имеет");
    }

    [Test]
    public void ThreadDiameter_FollowsTheDesignation()
    {
        Assert.AreEqual(6, ScrewLegSpec.ThreadDiameterMM(ScrewLegSpec.ThreadM6));
        Assert.AreEqual(8, ScrewLegSpec.ThreadDiameterMM(ScrewLegSpec.ThreadM8));
        Assert.AreEqual(10, ScrewLegSpec.ThreadDiameterMM(ScrewLegSpec.ThreadM10));
    }

    [Test]
    public void Centring_IsDemandedOnlyOnSidesThinnerThanTwentyFive()
    {
        Assert.IsTrue(ScrewLegSpec.NeedsCentring(16f), "торец 16 мм — крепление в середину");
        Assert.IsTrue(ScrewLegSpec.NeedsCentring(18f), "торец 18 мм — тоже");
        Assert.IsFalse(ScrewLegSpec.NeedsCentring(25f),
            "ровно 25 мм — граница включительно: футорке уже хватает мяса");
        Assert.IsFalse(ScrewLegSpec.NeedsCentring(600f));
    }

    [Test]
    public void Centred_TolerateHalfAMillimetre_AndNoMore()
    {
        Assert.IsTrue(ScrewLegSpec.IsCentred(0.5f));
        Assert.IsTrue(ScrewLegSpec.IsCentred(-0.5f));
        Assert.IsFalse(ScrewLegSpec.IsCentred(0.6f));
    }
}

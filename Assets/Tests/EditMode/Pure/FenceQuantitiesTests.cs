using KitchenDesigner.Core.Construction;
using NUnit.Framework;

public class FenceQuantitiesTests
{
    private static readonly FenceRun[] TwoRuns =
    {
        new FenceRun("Run-A", 10000f),
        new FenceRun("Run-B", 6000f),
    };

    [Test]
    public void FenceQuantities_PostsPerRun_GrowsAsSoonAsPartOfAStepIsCrossed()
    {
        Assert.AreEqual(2, FenceQuantities.PostsPerRun(1f, 2500f),
            "любой ненулевой прогон стоит хотя бы на двух столбах — по одному на каждом конце");
        Assert.AreEqual(2, FenceQuantities.PostsPerRun(2500f, 2500f),
            "ровно один шаг — по-прежнему два столба, без лишнего среднего");
        Assert.AreEqual(3, FenceQuantities.PostsPerRun(2501f, 2500f),
            "1 мм сверх шага — уже третий столб: формула Σ⌈L/шаг⌉+1 округляет ВВЕРХ, "
            + "а не вниз, как WallQuantities.StudCount — забор не может кончиться без "
            + "столба, каркас стены может стерпеть недобор утеплителя между стойками");
        Assert.AreEqual(0, FenceQuantities.PostsPerRun(0f, 2500f),
            "у прогона нулевой длины столбов нет");
    }

    [Test]
    public void FenceQuantities_PostsPerRun_ZeroOrNegativeStep_IsZeroPosts()
    {
        Assert.AreEqual(0, FenceQuantities.PostsPerRun(1000f, 0f),
            "нулевой шаг столбов — не «столб на каждый миллиметр», а невалидный ввод");
        Assert.AreEqual(0, FenceQuantities.PostsPerRun(1000f, -5f),
            "отрицательный шаг — тоже невалидный ввод, а не отрицательное число столбов");
    }

    [Test]
    public void FenceQuantities_SpansPerRun_HasOneFewerThanPosts()
    {
        Assert.AreEqual(4, FenceQuantities.SpansPerRun(10000f, 2500f),
            "10 000 / 2 500 = 4 — ровно 4 пролёта между 5 столбами");
        Assert.AreEqual(3, FenceQuantities.SpansPerRun(6000f, 2500f),
            "6 000 / 2 500 = 2,4, вверх до целого = 3 пролёта — 4 столба тоже делят "
            + "прогон на 3 пролёта, а не на 2");
    }

    [Test]
    public void FenceQuantities_PostCount_TwoRuns10000And6000_Step2500_Is9PostsTotal()
    {
        Assert.AreEqual(9, FenceQuantities.PostCount(TwoRuns, 2500f),
            "5 столбов на первом прогоне (10 000 / 2 500 = 4, +1) плюс 4 столба на "
            + "втором (6 000 / 2 500 = 2,4 → 3, +1) = 9. Столб на стыке прогонов "
            + "считается дважды — калькулятор суммирует ПРОГОНЫ, а не общую полилинию");
    }

    [Test]
    public void FenceQuantities_PostCount_NullRuns_IsZero()
    {
        Assert.AreEqual(0, FenceQuantities.PostCount(null, 2500f),
            "забор, у которого список прогонов ещё не собран, считается пустым, а не падает");
    }

    [Test]
    public void FenceQuantities_SpanCount_TwoRuns10000And6000_Step2500_Is7SpansTotal()
    {
        Assert.AreEqual(7, FenceQuantities.SpanCount(TwoRuns, 2500f),
            "4 пролёта первого прогона + 3 пролёта второго = 7");
    }

    [Test]
    public void FenceQuantities_TotalLengthM_TwoRuns10000And6000_Is16Metres()
    {
        Assert.AreEqual(16d, FenceQuantities.TotalLengthM(TwoRuns), 1e-9d,
            "10 000 + 6 000 = 16 000 мм = 16 м суммарной длины забора");
    }

    [Test]
    public void FenceQuantities_PostConcreteM3_9Posts_Hole200mmDepth800mm_Is0_226m3()
    {
        var concrete = FenceQuantities.PostConcreteM3(9, 200f, 800f);

        Assert.AreEqual(0.22619467105846514d, concrete, 1e-9d,
            "яма Ø200×800: объём цилиндра π×0,1²×0,8 = 0,0251327 м³ на столб; "
            + "9 столбов × 0,0251327 = 0,226195 м³. Число посчитано вручную, а не "
            + "переформулой продукта — иначе тест зелёный даже если множитель забыт");
    }

    [Test]
    public void FenceQuantities_PostConcreteM3_ZeroPosts_IsZero()
    {
        Assert.AreEqual(0d, FenceQuantities.PostConcreteM3(0, 200f, 800f), 1e-9d,
            "без столбов бетона в ямы не льют");
    }

    [Test]
    public void FenceQuantities_SheetCount_TwoRuns_SheetWidth1150mm_Is15SheetsTotal()
    {
        Assert.AreEqual(15, FenceQuantities.SheetCount(TwoRuns, 1150f),
            "10 000 / 1 150 = 8,70 → 9 листов на первом прогоне; "
            + "6 000 / 1 150 = 5,22 → 6 листов на втором; 9 + 6 = 15. Листы считаются "
            + "по прогону: обрезок в конце одного прогона не покрывает начало соседнего");
    }

    [Test]
    public void FenceQuantities_SheetAreaM2_TwoRuns_Height2000mm_Is32m2()
    {
        Assert.AreEqual(32d, FenceQuantities.SheetAreaM2(TwoRuns, 2000f), 1e-9d,
            "16 м суммарной длины × 2 м высоты = 32 м² листового материала");
    }

    [Test]
    public void FenceQuantities_RailRunningMetres_TwoRuns_2Rails_Is32Metres()
    {
        Assert.AreEqual(32d, FenceQuantities.RailRunningMetres(TwoRuns, 2), 1e-9d,
            "16 м забора × 2 лаги = 32 погонных метра — каждая лага идёт вдоль ВСЕЙ длины");
    }

    [Test]
    public void FenceQuantities_RailRunningMetres_ZeroRails_IsZero()
    {
        Assert.AreEqual(0d, FenceQuantities.RailRunningMetres(TwoRuns, 0), 1e-9d,
            "без лаг погонных метров нет, даже если сам забор длинный");
    }
}

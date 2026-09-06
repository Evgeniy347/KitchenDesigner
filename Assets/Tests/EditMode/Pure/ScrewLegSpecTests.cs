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

    /// <summary>Раньше здесь стоял допуск ±0,5 мм НА ПОЛОЖЕНИЕ, и он не знал ни
    /// толщины детали, ни диаметра резьбы: на 16-мм царге M6 он запрещал 1,5 мм,
    /// оставляющие 3,5 мм стенки, и разрешал бы те же 0,5 мм на 8-мм планке, где
    /// стенки нет вовсе. Правило существует ради МЯСА вокруг футорки — его и
    /// считаем; допуск на положение был подменой измеряемой величины.</summary>
    [Test]
    public void TheWallLeftBesideTheInsert_IsWhatIsMeasured()
    {
        Assert.AreEqual(5f, ScrewLegSpec.InsertWallMM(0f, 16f, 6f), 0.001f,
            "M6 ровно по центру 16-мм царги: (16-6)/2 — по 5 мм с каждой стороны");
        Assert.AreEqual(3f, ScrewLegSpec.InsertWallMM(1.5f, 16f, 6f), 0.001f,
            "смещение съедает стенку с той стороны, куда ушла опора: 3,5 мм, "
            + "и в меньшую сторону это 3");
        Assert.AreEqual(3f, ScrewLegSpec.InsertWallMM(-1.5f, 16f, 6f), 0.001f,
            "знак смещения роли не играет — тонкой становится противоположная стенка");
    }

    /// <summary>Округление ввёл владелец проекта вместо правки самого порога:
    /// «не порог чинить, а округлять в меньшую сторону». Дробная часть больше
    /// ничего не решает, и решение всегда падает на безопасную сторону — 3,9 мм
    /// стенки судятся как 3, а 2,9 как 2. Величина округляется РОВНО ОДНА —
    /// стенка вокруг футорки, и ровно в одном месте, чтобы правило, панель и
    /// этот тест читали одно число, а не три похожих.</summary>
    [Test]
    public void TheWall_IsRoundedDownToWholeMillimetres_BeforeItIsJudged()
    {
        Assert.AreEqual(3f, ScrewLegSpec.InsertWallMM(1.1f, 16f, 6f), 0.001f,
            "3,9 мм — это 3, а не 4: вверх не округляем никогда");
        Assert.AreEqual(2f, ScrewLegSpec.InsertWallMM(2.1f, 16f, 6f), 0.001f,
            "2,9 мм — это 2, и порог 3 мм такую установку не пропустит");
        Assert.AreEqual(3, ScrewLegSpec.FloorMM(3.9f), "округление живёт в одной функции");
        Assert.AreEqual(-1, ScrewLegSpec.FloorMM(-0.5f),
            "отрицательная стенка (резьба вышла за грань) уходит ВНИЗ, а не к нулю");
    }

    /// <summary>Округление вниз по float опасно ровно на целых числах: 3,0 мм,
    /// посчитанные из координат сцены, приходят как 2,9999998 и без допуска
    /// становятся двойкой — правильная установка покраснела бы из-за одной
    /// седьмой значащей цифры. Поэтому у пола есть свой названный допуск.</summary>
    [Test]
    public void FloorMM_DoesNotFallThroughAWholeMillimetre_ArrivedAtByFloatError()
    {
        Assert.AreEqual(3, ScrewLegSpec.FloorMM(2.9999998f),
            "промах в 2e-7 мм — это ошибка вычисления, а не 2 мм стенки");
        Assert.AreEqual(2, ScrewLegSpec.FloorMM(2.99f),
            "контроль: настоящие 2,99 мм по-прежнему двойка — допуск не съел десятые");
    }

    [Test]
    public void ThreeMillimetresOfWall_IsTheLine()
    {
        Assert.IsTrue(ScrewLegSpec.InsertHolds(1.5f, 16f, 6f),
            "1,5 мм от центра 16-мм царги: 3,5 мм стенки, после округления 3 — "
            + "футорке есть за что держаться");
        Assert.IsTrue(ScrewLegSpec.InsertHolds(2f, 16f, 6f), "ровно 3 мм — граница включительно");
        Assert.IsFalse(ScrewLegSpec.InsertHolds(2.1f, 16f, 6f),
            "2,9 мм округляются до 2 — граница проходит по целым миллиметрам");
        Assert.IsFalse(ScrewLegSpec.InsertHolds(4f, 16f, 6f),
            "4 мм от центра 16-мм торца оставляют 1 мм — футорка выйдет боком");
        Assert.IsFalse(ScrewLegSpec.InsertHolds(1.5f, 16f, 10f),
            "та же установка под M10 уже не держит: резьба толще, мяса меньше");
    }

    /// <summary>Второе правило про то же крепление: мало войти по центру — надо
    /// ещё войти достаточно глубоко. Порог 5 мм — число владельца проекта.</summary>
    [Test]
    public void FiveMillimetresInsideTheHost_IsTheLine()
    {
        Assert.AreEqual(5, ScrewLegSpec.MIN_INSERTION_INTO_HOST_MM,
            "порог назван константой, а не литералом в правиле");
        Assert.IsTrue(ScrewLegSpec.InsertionHolds(38),
            "38 мм в 80-мм царге цоколя — так стоит опора в живом проекте");
        Assert.IsTrue(ScrewLegSpec.InsertionHolds(5), "ровно 5 мм — граница включительно");
        Assert.IsFalse(ScrewLegSpec.InsertionHolds(4), "4 мм — резьбе не за что держаться");
        Assert.IsFalse(ScrewLegSpec.InsertionHolds(0),
            "нулевой заход: опора просто подпирает деталь снизу, а не ввинчена в неё");
    }
}

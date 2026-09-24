using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Диаметр и шаг арматуры — обычные дефолты полей (docs/todo_evolution.md §3.3: «Это
/// дефолты полей, а не инженерные правила — правило констант на них не распространяется»), как
/// шов и запас у кладки: Ø12 и шаг 300 мм — ходовые значения для ленты частного дома, без
/// претензии на цитату норматива. Защитный слой — другое дело: §3.3 называет его явным
/// исключением («дефолт берётся из норматива с пунктом в тесте — СП 63.13330 для защитного
/// слоя»), а исполнитель пункт 10.3.2 не сверял — отсюда [Category("NormativeUnverified")]
/// только на тесте, который его проверяет.</summary>
public class FoundationRebarDefaultsTests
{
    [Test]
    public void DiameterAndStep_AreOrdinaryFieldDefaults_NotFromANormativeClause()
    {
        Assert.AreEqual(12, FoundationRebarDefaults.DiameterMm,
            "Ø12 — ходовое сечение для ленты частного дома, дефолт поля, как шов кладки 10 мм");
        Assert.AreEqual(300, FoundationRebarDefaults.StepMm,
            "шаг хомутов 300 мм — тот же порядок, что и шаг стоек каркаса (WallQuantities."
            + "FrameStudStepMm = 600 мм для несущих стоек, здесь мельче — хомут тоньше стойки)");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void CoverMm_Is40_CitedToSp63_13330_ButThePointIsNotVerified()
    {
        Assert.AreEqual(40, FoundationRebarDefaults.CoverMm,
            "40 мм — защитный слой при бетонной подготовке, " + FoundationRebarDefaults.CoverSource
            + ". Без подготовки норматив обычно требует больше (типично называют 70 мм) — сюда "
            + "взято меньшее число, потому что подушка (песок+щебень) в этом приложении и есть "
            + "аналог подготовки под лентой");
    }
}

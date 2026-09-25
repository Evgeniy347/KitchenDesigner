using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>docs/NORMATIVE-DEFAULTS.md §4: минвата толщиной 100–150 мм считается по СП
/// 50.13330.2012 через ГСОП, но число именно для региона «Урал» источники не дают напрямую —
/// отсюда [Category("NormativeUnverified")] на утеплителе. Вентзазор и шаг обрешётки — чистая
/// практика без номера СП, дефолты полей как шов/запас кладки (docs/todo_evolution.md §3.3).</summary>
public class WallLayerDefaultsTests
{
    [Test]
    [Category("NormativeUnverified")]
    public void InsulationThickness_Is100_MiddleOfThePracticeRangeForSp50_ButUralIsNotVerified()
    {
        Assert.AreEqual(100, WallLayerDefaults.InsulationThicknessMm,
            "100 мм — нижняя граница практики 100–150 мм по расчёту через ГСОП (СП 50.13330.2012, "
            + "docs/NORMATIVE-DEFAULTS.md §4); число для конкретно региона «Урал» источники не дают");
    }

    [Test]
    public void VentGapAndBatten_AreOrdinaryFieldDefaults_NotFromANormativeClause()
    {
        Assert.AreEqual(30, WallLayerDefaults.VentGapThicknessMm,
            "30 мм — нижняя граница практики 30–40 мм для вентфасада, docs/NORMATIVE-DEFAULTS.md §4");
        Assert.AreEqual(600, WallLayerDefaults.VentGapBattenStepMm,
            "600 мм — типовой шаг обрешётки из того же источника, совпадает с шагом каркасных "
            + "стоек стены (WallQuantities.FrameStudStepMm)");
        Assert.AreEqual(20, WallLayerDefaults.CladdingThicknessMm,
            "20 мм — тонкая облицовка (сайдинг/доска), дефолт поля без претензии на норматив");
    }

    [Test]
    public void ClampBounds_KeepDefaultsInsideThem()
    {
        Assert.LessOrEqual(WallLayerDefaults.MinThicknessMm, WallLayerDefaults.InsulationThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.InsulationThicknessMm, WallLayerDefaults.MaxThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.MinThicknessMm, WallLayerDefaults.VentGapThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.VentGapThicknessMm, WallLayerDefaults.MaxThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.MinThicknessMm, WallLayerDefaults.CladdingThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.CladdingThicknessMm, WallLayerDefaults.MaxThicknessMm);
        Assert.LessOrEqual(WallLayerDefaults.MinBattenStepMm, WallLayerDefaults.VentGapBattenStepMm);
        Assert.LessOrEqual(WallLayerDefaults.VentGapBattenStepMm, WallLayerDefaults.MaxBattenStepMm);
    }
}

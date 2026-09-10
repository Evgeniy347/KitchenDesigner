using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Проверки сенсора самого по себе, отдельно от духовки: логика
/// перекочевала из <see cref="OvenCoplanarSurfaceTests"/> в
/// <see cref="CoplanarSurfaceDetector"/> без изменений, чтобы им мог
/// пользоваться любой элемент, а не только духовка.</summary>
public class CoplanarSurfaceDetectorTests
{
    /// <summary>Положительный контроль: без него зелёный сенсор ничего не
    /// доказывает — сломанный детектор молчит точно так же, как исправная
    /// геометрия.</summary>
    [Test]
    public void Detector_TwoBoxesSharingAFrontFace_ReportsThem()
    {
        var flush = new[]
        {
            (new Vector3(0f, 0f, -10f), new Vector3(100f, 100f, 20f)),
            (new Vector3(0f, 0f, -1f), new Vector3(50f, 50f, 2f)),
        };

        var fights = CoplanarSurfaceDetector.Fights(flush, i => i == 0 ? "плита" : "накладка");

        Assert.IsNotEmpty(fights, "две коробки с общим лицом на z = 0 обязаны быть найдены");
    }

    /// <summary>Второй контроль, с другой стороны: соприкосновение коробок —
    /// норма, и сенсор не имеет права краснеть на нём, иначе его отключат.</summary>
    [Test]
    public void Detector_BoxStackedOnAnother_IsNotAFight()
    {
        var stacked = new[]
        {
            (new Vector3(0f, 0f, 0f), new Vector3(100f, 20f, 100f)),
            (new Vector3(0f, 20f, 0f), new Vector3(100f, 20f, 100f)),
        };

        var fights = CoplanarSurfaceDetector.Fights(stacked, i => "коробка" + i);

        Assert.IsEmpty(fights,
            "у стыка нормали противоположны, отсечение задних граней рисует одну: "
            + string.Join("\n", fights));
    }

    /// <summary>Третий контроль: две коробки, разошедшиеся ровно на пороговое
    /// расстояние, не должны попасть в отчёт — порог строгий (`>=`), не
    /// нестрогий.</summary>
    [Test]
    public void Detector_BoxesExactlyAtTheThreshold_AreNotAFight()
    {
        float box1FrontZ = 10f;
        float box2FrontZ = box1FrontZ + CoplanarSurfaceDetector.DEFAULT_MIN_SEPARATION_MM;
        var apart = new[]
        {
            (new Vector3(0f, 0f, 0f), new Vector3(100f, 100f, box1FrontZ * 2f)),
            (new Vector3(0f, 0f, box2FrontZ - 1f), new Vector3(50f, 50f, 2f)),
        };

        var fights = CoplanarSurfaceDetector.Fights(apart, i => "коробка" + i);

        Assert.IsEmpty(fights,
            "грани разведены ровно на порог — это ещё не дефект: " + string.Join("\n", fights));
    }

    /// <summary>Четвёртый контроль: совпадение граней БЕЗ перекрытия площадью
    /// (коробки рядом, не друг над другом) — не дефект, иначе сенсор красит
    /// любые два соседних объекта на одной высоте.</summary>
    [Test]
    public void Detector_CoplanarBoxesWithNoAreaOverlap_IsNotAFight()
    {
        var sideBySide = new[]
        {
            (new Vector3(-60f, 0f, 0f), new Vector3(100f, 100f, 20f)),
            (new Vector3(60f, 0f, 0f), new Vector3(100f, 100f, 20f)),
        };

        var fights = CoplanarSurfaceDetector.Fights(sideBySide, i => "коробка" + i);

        Assert.IsEmpty(fights,
            "коробки стоят рядом на одной глубине, но не перекрываются по X — не "
            + "дефект: " + string.Join("\n", fights));
    }

    /// <summary>Сенсор для реальных элементов читает МЕШИ через
    /// `Renderer.bounds` (мировые единицы Unity), а не готовые мм-коробки —
    /// эта конверсия обязана быть обратимой и точной, иначе общий обход по
    /// всем типам элементов сравнивает несопоставимые числа.</summary>
    [Test]
    public void FromWorldBounds_ConvertsUnityUnitsToMillimetres()
    {
        var bounds = new Bounds(new Vector3(0.1f, 0.2f, -0.3f), new Vector3(0.6f, 0.02f, 0.4f));

        var (centerMM, sizeMM) = CoplanarSurfaceDetector.FromWorldBounds(bounds);

        Assert.AreEqual(100f, centerMM.x, 0.01f);
        Assert.AreEqual(200f, centerMM.y, 0.01f);
        Assert.AreEqual(-300f, centerMM.z, 0.01f);
        Assert.AreEqual(600f, sizeMM.x, 0.01f);
        Assert.AreEqual(20f, sizeMM.y, 0.01f);
        Assert.AreEqual(400f, sizeMM.z, 0.01f);
    }
}

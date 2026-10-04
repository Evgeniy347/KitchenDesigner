using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>GRD-01 — край детали не на целом миллиметре. Раньше это молча чинила
/// ЗАГРУЗКА: <c>SceneRestorer</c> прогонял <c>MmGrid.Snap</c> по каждой детали и
/// двигал её до 0,5 мм без шага отмены, а следующее сохранение закрепляло сдвиг в
/// файле пользователя. Теперь загрузка не двигает ничего, а несовпадение с сеткой
/// становится находкой: человек видит строку и решает сам.
///
/// Уровень — предупреждение, а не ошибка: 0,4 мм не мешают деталям стоять, но
/// искажают спецификацию и раскрой, где вся предметная область целочисленная.
///
/// Порог находки (0,05 мм) на порядок ГРУБЕЕ порога, с которым округляет
/// <c>MmGrid.Snap</c> (<c>EpsMm</c> = 0,01 мм), и это намеренно: хвост вида
/// 124,9997 — накопленная погрешность float на координатах в метрах, а не ошибка
/// человека, и жаловаться на него значит утопить настоящие 0,4 мм в шуме.</summary>
public class MmGridFindingTests
{
    private const float Tol = MmGridMath.OffGridFindingToleranceMm;

    private static string? Finding(float xMm, float yMm = 500f, float zMm = 300f) =>
        MmGridIssueCatalog.OffMillimetreGrid(new Vector3(xMm, yMm, zMm), Tol);

    [Test]
    public void OffMillimetreGrid_EdgeAt124_4_NamesTheAxisTheShiftAndWhatToDo()
    {
        var message = Finding(124.4f);

        Assert.IsNotNull(message, "124,4 мм — это 0,4 мм мимо целого миллиметра, "
            + "ровно тот случай, ради которого находка заведена");
        StringAssert.Contains("X 124,4 мм — сдвиг 0,4 мм", message!,
            "строка обязана назвать ось, координату грани и величину сдвига: без них "
            + "человек не знает, какую деталь и куда двигать");
        StringAssert.Contains("отпускание выровняет её по сетке", message!,
            "находка обязана сказать, ЧЕМ она лечится — жест пользователя, а не загрузка: "
            + "иначе список превращается в жалобу без выхода");
    }

    [Test]
    public void OffMillimetreGrid_NamesOnlyTheAxesThatAreOff()
    {
        var message = Finding(124.4f, 500f, 300f);

        StringAssert.DoesNotContain("Y ", message!,
            "Y стоит ровно на 500 мм — ось без отклонения в находке не упоминается");
        StringAssert.DoesNotContain("Z ", message!, "и Z тоже: 300 мм — целое число");
    }

    [Test]
    public void OffMillimetreGrid_TwoAxesOff_NamesBothInAxisOrder()
    {
        var message = Finding(124.4f, 500f, 299.7f);

        StringAssert.Contains("X 124,4 мм — сдвиг 0,4 мм, Z 299,7 мм — сдвиг 0,3 мм", message!,
            "две оси мимо сетки — обе названы, порядок X, Y, Z");
    }

    [Test]
    public void OffMillimetreGrid_FloatTailWithinTolerance_ReportsNothing()
    {
        Assert.IsNull(Finding(124.9997f),
            "124,9997 мм — накопленный хвост float, а не сдвиг детали: отклонение 0,0003 мм "
            + "меньше порога 0,05 мм и находкой быть не должно. Противоположный вход к "
            + "первому тесту: правило, которое умеет только срабатывать, не описывает "
            + "зелёного состояния");
    }

    [Test]
    public void OffMillimetreGrid_WholeMillimetresOnEveryAxis_ReportsNothing()
    {
        Assert.IsNull(Finding(124f, 500f, 300f),
            "деталь, стоящая гранями на целых миллиметрах, — норма, а не находка");
    }

    [Test]
    public void OffMillimetreGrid_JustInsideTheTolerance_StaysSilent_AndJustOutsideItSpeaks()
    {
        Assert.IsNull(Finding(124f + Tol * 0.98f),
            "0,049 мм — внутри порога 0,05 мм, находки нет");
        Assert.IsNotNull(Finding(124f + Tol * 1.02f),
            "а 0,051 мм уже снаружи и обязано говорить — иначе порог был бы бесконечным "
            + "и находка недостижимой");
    }

    [Test]
    public void ShiftToWholeMm_PointsFromTheEdgeToTheNearestWholeMillimetre_HalfUp()
    {
        Assert.AreEqual(-0.4f, MmGridMath.ShiftToWholeMm(124.4f), 1e-4f,
            "124,4 → 124: сдвиг отрицательный, потому что грань стоит ПРАВЕЕ целого");
        Assert.AreEqual(0.4f, MmGridMath.ShiftToWholeMm(123.6f), 1e-4f,
            "123,6 → 124: зеркальный случай, сдвиг положительный");
        Assert.AreEqual(0.5f, MmGridMath.ShiftToWholeMm(1208.5f), 1e-4f,
            "ровно половина уходит ВВЕРХ — то же правило, что у MmGrid.RoundMm, "
            + "и та же единственная реализация");
    }

    [Test]
    public void TryMeasureOffGrid_ReportsTheShiftPerAxis_AndZeroOnTheAxesThatAreOnTheGrid()
    {
        Assert.IsTrue(MmGridMath.TryMeasureOffGrid(new Vector3(124.4f, 500f, 300f), Tol,
            out var shifts));
        Assert.AreEqual(-0.4f, shifts.x, 1e-4f, "по X деталь надо подвинуть на 0,4 мм назад");
        Assert.AreEqual(0f, shifts.y, "ось на сетке обязана дать ровный ноль, а не «почти»");
        Assert.AreEqual(0f, shifts.z, "и Z тоже");
    }
}

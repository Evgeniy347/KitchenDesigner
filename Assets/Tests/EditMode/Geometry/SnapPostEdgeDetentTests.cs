using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Вертикальная стойка, приставленная торцом к кромке горизонтальной панели,
/// при ПЕРЕТАСКИВАНИИ по вертикали обязана ловить плоскости этой панели — так же,
/// как их ловит растягивание.
///
/// Сцена — упрощённая копия реальной (A12_upper_shelf_2_1_2 против B12_upper_top):
///   панель  X[545..1567]  Y[2242..2260]  Z[-3620..-3288];
///   стойка  X[1567..1585] Y[..]          Z[-3619.5..-3288.5];
/// footprint'ы делят РОВНО РЕБРО по X (1567) — контакт кромочный.
/// Рядом стена в 0.5 мм по Z: она забирает основной проход снэпа.
///
/// Баг: кромочный кандидат отбрасывался в проходах добора целиком, поэтому снэп
/// по вертикали срабатывал только если выигрывал ПЕРВЫЙ проход — то есть если его
/// зазор меньше вообще всех прочих. Стена с зазором 0.5 мм всегда забирала первый
/// проход, и стойка не ловила панель ни на одном миллиметре, хотя ресайз той же
/// пары работал.
/// </summary>
public class SnapPostEdgeDetentTests : SnapCoreTestBase
{
    /// <summary>Y центра стойки, при котором её верх заподлицо с низом панели.</summary>
    private const float FlushUnderPanel = 1.7925f;

    /// <summary>Y центра стойки, при котором её верх заподлицо с ВЕРХОМ панели
    /// (дальняя кромка, 2260 мм). Второй детент той же панели.</summary>
    private const float FlushWithPanelTop = 1.8105f;

    private static readonly Box Post = Make("Post", new Vector3Int(18, 899, 331));

    private static List<ElementGeometry> Others() => new List<ElementGeometry>
    {
        At(Make("Panel", new Vector3Int(1022, 18, 332)), new Vector3(1.056f, 2.251f, -3.454f)),
        // Стена сзади: её -Z грань в 0.5 мм от +Z грани стойки (-3.2885).
        At(Make("Wall", new Vector3Int(4000, 2700, 100)), new Vector3(1.0f, 1.35f, -3.2380f)),
    };

    /// <summary>Ставит стойку центром в posY и возвращает результат снэпа.</summary>
    private static SnapResult SnapAt(float posY)
        => Snap(Post, Others(), new Vector3(1.576f, posY, -3.454f));

    [Test]
    public void Post_DraggedUpUnderPanel_SnapsFlushToPanelUnderside()
    {
        // Верх стойки на 10 мм ниже низа панели — в пределах порога 50 мм.
        var r = SnapAt(FlushUnderPanel - 10f * MM);

        Assert.IsTrue(r.snapped, "стойка должна прилипнуть");
        Assert.AreEqual(FlushUnderPanel, r.position.y, Tol,
            "верх стойки встаёт заподлицо с низом панели");
    }

    /// <summary>Детент ловится издалека, а не только вплотную. Смещения взяты в
    /// пределах, где ближайшая плоскость — именно низ панели (середина между
    /// детентами — +9 мм).</summary>
    [Test]
    public void Post_SnapsToSameDetent_FromBothSidesAcrossThreshold()
    {
        foreach (float offsetMm in new[] { -30f, -20f, -5f, 5f, 8f })
        {
            var r = SnapAt(FlushUnderPanel + offsetMm * MM);
            Assert.IsTrue(r.snapped, $"смещение {offsetMm} мм: снэп обязан сработать");
            Assert.AreEqual(FlushUnderPanel, r.position.y, Tol,
                $"смещение {offsetMm} мм: тот же детент");
        }
    }

    /// <summary>У панели ДВЕ кромки, и обе — детенты: низ (2242) и верх (2260).
    /// Пропуск верхней кромки и был «скачет между рёбрами»: стойка, ползущая
    /// вверх, перепрыгивала с низа панели сразу на следующую полку. Перемещение
    /// обязано видеть обе плоскости — как их давно видит ресайз.</summary>
    [Test]
    public void Post_DraggedHigher_CatchesFarEdgeOfPanel()
    {
        var r = SnapAt(FlushWithPanelTop - 6f * MM);

        Assert.IsTrue(r.snapped, "верхняя кромка панели — тоже детент");
        Assert.AreEqual(FlushWithPanelTop, r.position.y, Tol,
            "верх стойки встаёт заподлицо с верхом панели");
    }

    /// <summary>Между детентами деталь выбирает ближайший, а не перескакивает
    /// через один: это и делает протягивание предсказуемым.</summary>
    [Test]
    public void Post_BetweenDetents_PicksNearestNotFarther()
    {
        // Чуть выше середины (+9 мм) — ближе верхняя кромка.
        var high = SnapAt(FlushUnderPanel + 12f * MM);
        Assert.AreEqual(FlushWithPanelTop, high.position.y, Tol, "выше середины — верхняя кромка");

        // Чуть ниже середины — ближе нижняя.
        var low = SnapAt(FlushUnderPanel + 6f * MM);
        Assert.AreEqual(FlushUnderPanel, low.position.y, Tol, "ниже середины — нижняя кромка");
    }

    /// <summary>Контакт со стеной по Z при этом не теряется — добор по вертикали
    /// не имеет права его разорвать.</summary>
    [Test]
    public void Post_VerticalSnap_KeepsWallContactOnZ()
    {
        var r = SnapAt(FlushUnderPanel - 10f * MM);

        Assert.IsTrue(r.snapped);
        // Стойка прижимается к стене: её +Z грань встаёт на -3.288.
        Assert.AreEqual(-3.4535f, r.position.z, Tol, "стойка дотянута до стены по Z");
    }

    /// <summary>За порогом прилипания деталь стоит там, куда её привели.</summary>
    [Test]
    public void Post_FarBelowPanel_DoesNotSnapVertically()
    {
        float posY = FlushUnderPanel - 120f * MM;
        var r = SnapAt(posY);

        float resultY = r.snapped ? r.position.y : posY;
        Assert.AreEqual(posY, resultY, Tol, "за 50 мм по вертикали притяжения нет");
    }
}

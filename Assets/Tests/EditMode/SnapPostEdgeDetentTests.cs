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
public class SnapPostEdgeDetentTests : SnapTestBase
{
    private KitchenElement? _panel;
    private KitchenElement? _post;

    /// <summary>Y центра стойки, при котором её верх заподлицо с низом панели.</summary>
    private const float FlushUnderPanel = 1.7925f;

    /// <summary>Ставит стойку центром в posY и возвращает результат снэпа.</summary>
    private SnapResult SnapAt(float posY)
    {
        var testPos = new Vector3(1.576f, posY, -3.454f);
        return SnapSystem.TrySnap(_post!, Others(), testPos);
    }

    private List<KitchenElement> Others() =>
        new List<KitchenElement>(_spawned.ConvertAll(go => go.GetComponent<KitchenElement>()));

    [SetUp]
    public void BuildScene()
    {
        _panel = Make("Panel", new Vector3Int(1022, 18, 332), new Vector3(1.056f, 2.251f, -3.454f));
        _post = Make("Post", new Vector3Int(18, 899, 331), new Vector3(1.576f, 1.8101f, -3.454f));
        // Стена сзади: её -Z грань в 0.5 мм от +Z грани стойки (-3.2885).
        Make("Wall", new Vector3Int(4000, 2700, 100), new Vector3(1.0f, 1.35f, -3.2380f));
    }

    [Test]
    public void Post_DraggedUpUnderPanel_SnapsFlushToPanelUnderside()
    {
        // Верх стойки на 10 мм ниже низа панели — в пределах порога 50 мм.
        var r = SnapAt(FlushUnderPanel - 10f * MM);

        Assert.IsTrue(r.snapped, "стойка должна прилипнуть");
        Assert.AreEqual(FlushUnderPanel, r.position.y, Tol,
            "верх стойки встаёт заподлицо с низом панели");
    }

    /// <summary>Тот же детент ловится и издалека, и с другой стороны — снэп по
    /// вертикали работает во всём диапазоне порога, а не только вплотную.</summary>
    [Test]
    public void Post_SnapsToSameDetent_FromBothSidesAcrossThreshold()
    {
        foreach (float offsetMm in new[] { -30f, -20f, -5f, 5f, 20f, 30f })
        {
            var r = SnapAt(FlushUnderPanel + offsetMm * MM);
            Assert.IsTrue(r.snapped, $"смещение {offsetMm} мм: снэп обязан сработать");
            Assert.AreEqual(FlushUnderPanel, r.position.y, Tol,
                $"смещение {offsetMm} мм: тот же детент");
        }
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

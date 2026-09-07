using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прощение пересечения выдаётся опоре ради РЕЗЬБЫ: без неё внутри
/// хозяина крепления не бывает. Пятак — противоположное тело: это опорная
/// площадка, она обязана остаться СНАРУЖИ, под деталью.
///
/// Ядро прощало пару целиком: <c>TryScrewLegContact</c> добавляло контакт и
/// возвращало true, не спросив, где при этом главная коробка. В живом проекте
/// (Vintovaya_opora_2 в docs/example.save.json) пятак Ø14×8 целиком сидел внутри
/// 80-мм царги — и не получал ни одного замечания.
///
/// Числа сцены — из того же проекта: царга 382×80×18 стоит на 20..100 мм,
/// правильная опора держит пятак на 0..8 мм, утопленная — на 25..33 мм. Резьба
/// в обоих случаях внутри хозяина, меняется ТОЛЬКО высота опоры, поэтому
/// красным тест делает разбор тел, а не что-нибудь ещё.</summary>
public class ValidationScrewLegPadReproTests
{
    private const float MM = 0.001f;

    private const int Floor = 0;
    private const int Host = 1;
    private const int Leg = 2;

    private const string HostName = "A4_plint_drawer_L_inner";

    private static Vector3[] Corners(Vector3 center, Vector3 size)
    {
        var half = size * 0.5f;
        var verts = new Vector3[8];
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    verts[i++] = center + new Vector3(half.x * sx, half.y * sy, half.z * sz);
        return verts;
    }

    private static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm,
        ElementKind kind = ElementKind.None)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        return new ValidationElement(ElementGeometry.Box(name, center, size), Corners(center, size),
            kind, 0, null, Span.FromCenter(center.y, size.y), -1);
    }

    /// <summary>Опора ростом 58 мм (пятак 8 + резьба 50), у которой низ пятака
    /// стоит на <paramref name="padBottomMm"/>.</summary>
    private static ValidationElement ScrewLeg(float padBottomMm)
    {
        Vector3 padCentre = new Vector3(0, padBottomMm + 4f, 0) * MM;
        Vector3 padSize = new Vector3(14, 8, 14) * MM;
        var pad = ElementGeometry.Box("Vintovaya_opora", padCentre, padSize);
        var thread = ElementGeometry.Box("Vintovaya_opora/thread",
            new Vector3(0, padBottomMm + 33f, 0) * MM, new Vector3(6, 50, 6) * MM);
        return new ValidationElement(pad, Corners(padCentre, padSize), ElementKind.ScrewLeg,
            0, HostName, Span.FromCenter((padBottomMm + 29f) * MM, 58 * MM), -1,
            thread, true, Host);
    }

    private const float PadOnTheFloor = 0f;

    private const float PadSunkIntoTheHost = 25f;

    private static CoreValidationResult SceneWithPadAt(float padBottomMm) =>
        ValidationCore.Validate(new[]
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part(HostName, new Vector3(0, 60, 0), new Vector3(382, 80, 18)),
            ScrewLeg(padBottomMm),
        });

    private static int OverlapsBetween(CoreValidationResult r, int i, int j) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == ViolationKind.Overlap
            && ((d.Element == i && d.Other == j) || (d.Element == j && d.Other == i)));

    [Test]
    public void PadSunkIntoItsHost_IsOverlap()
    {
        var r = SceneWithPadAt(PadSunkIntoTheHost);

        Assert.AreEqual(1, OverlapsBetween(r, Leg, Host),
            "пятак 25..33 мм целиком внутри царги 20..100 мм — это металл в металле. "
            + "Прощение выдано резьбе, а не элементу: опорная площадка обязана быть "
            + "СНАРУЖИ, под деталью, иначе опора ничего не подпирает");
    }

    /// <summary>Противоположный вход к тесту выше на ТОЙ ЖЕ сцене: правильно
    /// поставленная опора обязана остаться без замечаний, иначе первый тест был
    /// бы зелёным на ядре, которое разучилось прощать резьбу вообще.</summary>
    [Test]
    public void PadUnderItsHost_IsStillNotOverlap()
    {
        var r = SceneWithPadAt(PadOnTheFloor);

        Assert.AreEqual(0, OverlapsBetween(r, Leg, Host),
            "пятак 0..8 мм под царгой 20..100 мм, внутри — только резьба: в неё опора "
            + "и ввинчена");
    }

    /// <summary>Контакт «опора держится резьбой» ставится ДО того, как решается
    /// вопрос о пересечении: иначе утопленная опора получила бы вдобавок COL-02
    /// «висит в воздухе» — второе сообщение об одной и той же беде.</summary>
    [Test]
    public void PadSunkIntoItsHost_StillCountsAsFaceToFaceContact()
    {
        var r = SceneWithPadAt(PadSunkIntoTheHost);

        Assert.IsTrue(r.Contacts.Any(c => c.IsFaceToFace
                && ((c.A == Leg && c.B == Host) || (c.A == Host && c.B == Leg))),
            "связность обязана видеть опору пристёгнутой к хозяину и тогда, когда "
            + "пятак утоплен: коллизия и потеря опоры — разные диагнозы");
        Assert.AreEqual(0, OverlapsBetween(r, Leg, Floor),
            "и пол тут ни при чём — пятак до него не достаёт");
    }
}

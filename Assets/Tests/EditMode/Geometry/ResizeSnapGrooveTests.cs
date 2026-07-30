using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прилипание растягиваемой грани к ПАЗУ: дно как посадочное место
/// вкладной панели и стенки как разметочные плоскости.
///
/// Эти ветки `ResizeSnap` до сих пор проверялись только сценовыми тестами
/// (`GroovePanelBoxTests`, `GrooveEdgeSnapTests`), поэтому мутационный прогон
/// их вовсе не видел: 43 мутанта без покрытия. Здесь то же самое на снимках —
/// исполняется и в Unity, и под dotnet.</summary>
public class ResizeSnapGrooveTests
{
    private const float MM = 0.001f;
    private const float Threshold = 50f * MM;

    /// <summary>Доска 800×400×18 с пазом в пласти +Z: глубина 8 мм, стенки на
    /// −50 и −34 мм от центра по Y (паз 16 мм под ДВП), сам паз идёт вдоль X.
    /// Пласть +Z детали на z = +9 мм, дно паза — на z = +1 мм.</summary>
    private static ElementGeometry Board() =>
        GroovedGeometry.Board("Board", Vector3.zero, new Vector3(800, 400, 18),
            depthMm: 8f, fromMm: -50f, toMm: -34f);

    /// <summary>Тонкая вкладная панель: важен только флаг IsPanel — дно паза
    /// предлагается ей и не предлагается толстой детали.</summary>
    private static ElementGeometry Panel(Vector3 centerMm) =>
        ElementGeometry.Box("Panel", centerMm * MM, new Vector3(600, 300, 4) * MM, isPanel: true);

    private static ElementGeometry Thick(Vector3 centerMm) =>
        ElementGeometry.Box("Thick", centerMm * MM, new Vector3(600, 300, 18) * MM);

    /// <summary>Нижняя грань детали (−Z) — та, которой её тянут вниз к пазу.</summary>
    private static Face BottomFace(in ElementGeometry g) => g.Faces[5];

    private static bool Snap(in ElementGeometry self, in Face face, ElementGeometry other,
        out float gap, float threshold = Threshold) =>
        ResizeSnap.SnapDelta(face.center, face.normal, face.rightAxis, face.upAxis, face.size,
            new List<ElementGeometry> { other }, self, threshold, out gap);

    // ── Дно паза ────────────────────────────────────────────────────────

    [Test]
    public void PanelEdge_SnapsToGrooveFloor()
    {
        // Кромка панели на z = 5 мм, дно паза на z = 1 мм → сдвиг 4 мм внутрь.
        var panel = Panel(new Vector3(0, -42, 7));
        var face = BottomFace(panel);
        Assert.AreEqual(-1f, Vector3.Dot(face.normal, Vector3.forward), 0.001f, "грань смотрит вниз по Z");

        Assert.IsTrue(Snap(panel, face, Board(), out float gap), "панель обязана ловить дно паза");
        Assert.AreEqual(4f * MM, gap, 0.1f * MM);
    }

    [Test]
    public void ThickBoard_IsNotOfferedGrooveFloor()
    {
        // Кромка на z = 3 мм: до дна паза (1) — 2 мм, до пласти (9) — 6 мм.
        // При пороге 4.5 мм посадка в паз была бы единственным доступным
        // детентом — и её не должно быть, потому что деталь не вкладная.
        var thick = Thick(new Vector3(0, -42, 12));
        var face = BottomFace(thick);   // z = 3 мм

        Assert.IsFalse(Snap(thick, face, Board(), out _, 4.5f * MM),
            "в паз садится только вкладная панель");

        // Контроль: та же поза у ВКЛАДНОЙ панели — садится.
        var panel = Panel(new Vector3(0, -42, 5));
        Assert.IsTrue(Snap(panel, BottomFace(panel), Board(), out _, 4.5f * MM));
    }

    [Test]
    public void PanelAside_DoesNotSnapToGrooveFloor()
    {
        // Панель над пластью, но мимо контура паза (паз по Y на −50..−34,
        // панель на +100..+400). Кромка на z = 3: до дна паза 2 мм, до пласти
        // 6 мм. Раз перекрытия с пазом нет, ловится ДАЛЬНЯЯ пласть.
        var panel = Panel(new Vector3(0, 250, 5));
        Assert.IsTrue(Snap(panel, BottomFace(panel), Board(), out float gap));
        Assert.AreEqual(-6f * MM, gap, 0.1f * MM,
            "дно паза притягивает только то, что стоит НАД пазом");
    }

    [Test]
    public void PanelFarFromGrooveFloor_BeyondThreshold_DoesNotSnap()
    {
        // Кромка панели на z = 70 мм: до пласти 61 мм, до дна паза 69 — оба
        // детента за порогом 50 мм.
        var panel = Panel(new Vector3(0, -42, 72));
        Assert.IsFalse(Snap(panel, BottomFace(panel), Board(), out _));
    }

    [Test]
    public void PanelBetweenFaceAndGrooveFloor_TakesNearest()
    {
        // Кромка панели на z = 11 мм: до пласти (9) — 2 мм, до дна паза (1) — 10.
        // Ближайший детент — пласть, а не дно.
        var panel = Panel(new Vector3(0, -42, 13));
        Assert.IsTrue(Snap(panel, BottomFace(panel), Board(), out float gap));
        Assert.AreEqual(2f * MM, gap, 0.1f * MM, "ближе пласть детали, а не дно паза");
    }

    // ── Стенки паза ─────────────────────────────────────────────────────

    /// <summary>Стенка паза — разметочная плоскость, доступная ЛЮБОЙ детали:
    /// «поставь полку по краю паза». Проверяем на толстой детали, которой дно
    /// паза не предлагается вовсе.</summary>
    [Test]
    public void AnyPart_SnapsToGrooveWall()
    {
        // Деталь стоит СНАРУЖИ пласти (z = 20 мм — паза не касается вовсе),
        // её −Y грань на y = −47, стенка паза на y = −50 → сдвиг 3 мм.
        var part = ElementGeometry.Box("Shelf", new Vector3(0, 103, 20) * MM,
            new Vector3(600, 300, 18) * MM);
        var face = part.Faces[3]; // −Y
        Assert.AreEqual(-1f, Vector3.Dot(face.normal, Vector3.up), 0.001f);

        Assert.IsTrue(Snap(part, face, Board(), out float gap),
            "стенка паза даёт детент любой детали");
        Assert.AreEqual(3f * MM, gap, 0.1f * MM);
    }

    [Test]
    public void GrooveWall_IsTwoSided()
    {
        // Та же стенка ловит и СО-НАПРАВЛЕННУЮ грань (+Y снизу): плоскость
        // разметочная, а не поверхность материала.
        var part = ElementGeometry.Box("Shelf", new Vector3(0, -203, 20) * MM,
            new Vector3(600, 300, 18) * MM);
        var face = part.Faces[2]; // +Y на y = −53
        Assert.IsTrue(Snap(part, face, Board(), out float gap));
        Assert.AreEqual(3f * MM, gap, 0.1f * MM, "заподлицо со стенкой паза (−50)");
    }

    [Test]
    public void GrooveWall_NoOverlapAlongLength_DoesNotSnap()
    {
        // Деталь уехала по X за пределы паза: вдоль длины паза перекрытия нет.
        var part = ElementGeometry.Box("Shelf", new Vector3(1200, 103, 20) * MM,
            new Vector3(600, 300, 18) * MM);
        Assert.IsFalse(Snap(part, part.Faces[3], Board(), out _),
            "стенка паза не тянет деталь, стоящую вне его длины");
    }

    /// <summary>По ГЛУБИНЕ перекрытия с пазом нет и быть не должно: деталь
    /// прилегает к пласти снаружи. Проверка перекрытия у стенок идёт только
    /// вдоль длины паза — иначе этот детент не срабатывал бы никогда.</summary>
    [Test]
    public void GrooveWall_IgnoresDepthOverlap()
    {
        var farOutside = ElementGeometry.Box("Shelf", new Vector3(0, 103, 500) * MM,
            new Vector3(600, 300, 18) * MM);
        Assert.IsTrue(Snap(farOutside, farOutside.Faces[3], Board(), out float gap),
            "деталь стоит далеко по Z, но детент стенки паза остаётся доступным");
        Assert.AreEqual(3f * MM, gap, 0.1f * MM);
    }

    [Test]
    public void PartWithoutGrooves_HasNoExtraDetents()
    {
        // Контроль: та же поза, но у соседа пазов нет — прилипать не к чему.
        var plain = ElementGeometry.Box("Board", Vector3.zero, new Vector3(800, 400, 18) * MM);
        var part = ElementGeometry.Box("Shelf", new Vector3(0, 103, 500) * MM,
            new Vector3(600, 300, 18) * MM);

        Assert.IsFalse(Snap(part, part.Faces[3], plain, out _));
    }
}

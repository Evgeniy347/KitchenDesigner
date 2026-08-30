using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Короб врезной техники — чаша мойки, короб выреза варочной. Габарит
/// самой техники его НЕ описывает: он равен только бортику на пласти, а тело
/// уходит вниз, в столешницу. Сама техника из парных проверок исключена
/// (ElementKind.Recessed — она по построению «пересекает» свою столешницу),
/// поэтому наезд короба на боковину под ней — единственная проверка, которая у
/// неё остаётся, и вся её точность держится на списке того, что коробу
/// разрешено проходить насквозь.
///
/// Каждый тест здесь — про ОДИН пункт этого списка, и у списка есть
/// положительный контроль <see cref="Body_OnAPlainCarcassSide_IsOverlap"/> на
/// той же геометрии: без него любой из «не пересечение» был бы зелёным и на
/// коде, который не проверяет короб вовсе.</summary>
public class ValidationRecessedBodyTests
{
    private const float MM = 0.001f;

    private const int Worktop = 1;
    private const int Obstacle = 2;
    private const int Hob = 3;

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
        ElementKind kind = ElementKind.None, bool isPanel = false, int groupId = 0)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var geometry = ElementGeometry.Box(name, center, size, isPanel);
        return new ValidationElement(geometry, Corners(center, size), kind, groupId, null,
            Span.FromCenter(center.y, size.y), -1);
    }

    /// <summary>Варочная: бортик 5 мм на пласти столешницы, короб уходит на
    /// 300 мм вниз — ровно туда, где стоит препятствие.</summary>
    private static ValidationElement HobOver(int hostIndex, bool declareBody = true)
    {
        Vector3 center = new Vector3(0, 900, 0) * MM;
        Vector3 size = new Vector3(500, 5, 400) * MM;
        var geometry = ElementGeometry.Box("Hob", center, size);
        var body = ElementGeometry.Box("Hob/body", new Vector3(0, 750, 0) * MM,
            new Vector3(500, 300, 400) * MM);
        return new ValidationElement(geometry, Corners(center, size), ElementKind.Recessed,
            0, null, Span.FromCenter(center.y, size.y), -1, body, declareBody, hostIndex);
    }

    /// <summary>Сцена «пол — столешница — препятствие под ней — варочная сверху».
    /// Препятствие стоит ровно в объёме короба; меняется только его роль.</summary>
    private static CoreValidationResult SceneWith(ValidationElement obstacle,
        int hostIndex = Worktop, bool declareBody = true)
    {
        return ValidationCore.Validate(new[]
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part("Worktop", new Vector3(0, 900, 0), new Vector3(2000, 38, 600)),
            obstacle,
            HobOver(hostIndex, declareBody),
        });
    }

    private static ValidationElement SideUnderHob(ElementKind kind = ElementKind.None,
        bool isPanel = false) =>
        Part("Obstacle", new Vector3(0, 750, 0), new Vector3(18, 300, 400), kind, isPanel);

    private static int OverlapsBetween(CoreValidationResult r, int i, int j) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == ViolationKind.Overlap
            && ((d.Element == i && d.Other == j) || (d.Element == j && d.Other == i)));

    [Test]
    public void Body_OnAPlainCarcassSide_IsOverlap()
    {
        var r = SceneWith(SideUnderHob());

        Assert.AreEqual(1, OverlapsBetween(r, Hob, Obstacle),
            "боковина под столешницей стоит в объёме короба варочной — это настоящее "
            + "пересечение, и оно единственное, что вообще проверяется у врезной техники");
    }

    [Test]
    public void Body_InsideItsOwnHost_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob());

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Worktop),
            "в столешнице для короба и режется проём — HostIndex указывает именно на неё");
    }

    [Test]
    public void Body_InsideAWorktopThatIsNotItsHost_IsOverlap()
    {
        var r = SceneWith(SideUnderHob(), hostIndex: -1);

        Assert.AreEqual(1, OverlapsBetween(r, Hob, Worktop),
            "пока техника не врезана (HostIndex = -1), столешница для короба — обычная "
            + "корпусная деталь; иначе исключение хоста молча освобождало бы любую");
    }

    [Test]
    public void Body_BehindAFacade_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Facade));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "фасад — навесная деталь: короб уходит ЗА него, и это конструкция, а не ошибка");
    }

    [Test]
    public void Body_BehindAFloatingFacade_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Facade | ElementKind.FloatingFacade));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "фасад с зазором остаётся фасадом");
    }

    [Test]
    public void Body_BehindAnInsetPanel_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(isPanel: true));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "вкладная панель (ДВП задней стенки) — тоже навесное: короб уходит за неё");
    }

    [Test]
    public void Body_ThroughADrawer_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Drawer));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "ящик — внутренность корпуса, коробом он не проверяется");
    }

    [Test]
    public void Body_ThroughAWall_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Anchor));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "якорь — не корпусная деталь: мойка у стены штатно уходит в неё коробом");
    }

    [Test]
    public void Body_ThroughDecor_IsNotOverlap()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Decor));

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "светильник физики не имеет ни с какой стороны");
    }

    [Test]
    public void UndeclaredBody_IsNotChecked()
    {
        var r = SceneWith(SideUnderHob(), declareBody: false);

        Assert.AreEqual(0, OverlapsBetween(r, Hob, Obstacle),
            "пока техника висит в воздухе и короб не задан, «вглубь детали» не значит ничего "
            + "— проверять нечего");
    }

    [Test]
    public void Recessed_ItselfNeedsNoSupport()
    {
        var r = SceneWith(SideUnderHob(ElementKind.Facade));

        CollectionAssert.DoesNotContain(r.Violations, Hob,
            "врезная техника держится бортиком на пласти, а не face-контактом");
    }
}

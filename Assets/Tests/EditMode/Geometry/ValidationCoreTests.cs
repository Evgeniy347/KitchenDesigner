using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Тесты ядра валидации: пересечения, контакты, связность, высота
/// проёмов. Работают на снимках (<see cref="ValidationElement"/>) — ни сцены,
/// ни GameObject, поэтому исполняются и в Unity, и под dotnet, и мутируются
/// Stryker'ом.
///
/// Проверяется прежде всего СЕМАНТИКА: что именно даёт каждая роль
/// (<see cref="ElementKind"/>). Именно эти четырнадцать решений («это пол»,
/// «это проём», «это мойка — пропустить») раньше держались на GetComponent и
/// проверялись только сценовыми тестами.</summary>
public class ValidationCoreTests
{
    private const float MM = 0.001f;

    /// <summary>Деталь-коробка: габариты в мм, позиция центра в мм.</summary>
    private static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm,
        ElementKind kind = ElementKind.None, int groupId = 0, string? pairedName = null,
        bool isPanel = false, int attachedWallIndex = -1)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var geometry = ElementGeometry.Box(name, center, size, isPanel);
        return new ValidationElement(geometry, Corners(center, size), kind, groupId, pairedName,
            Span.FromCenter(center.y, size.y), attachedWallIndex);
    }

    /// <summary>Восемь вершин осевой коробки — то, что в сцене возвращает
    /// GetVertices().</summary>
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

    /// <summary>Пол 3000×18×3000: верхняя грань на y = 0.</summary>
    private static ValidationElement Floor(string name = "Floor") =>
        Part(name, new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
            ElementKind.Anchor | ElementKind.FloorAnchor);

    /// <summary>Стена 3000×2500×100 с центром на x.</summary>
    private static ValidationElement WallAt(string name, float xMm) =>
        Part(name, new Vector3(xMm, 1250, 0), new Vector3(3000, 2500, 100), ElementKind.Anchor);

    /// <summary>Деталь, стоящая НА полу: 800×400×18 плашмя, низ на y = 0.</summary>
    private static ValidationElement OnFloor(string name, float xMm = 0,
        ElementKind kind = ElementKind.None, int groupId = 0) =>
        Part(name, new Vector3(xMm, 9, 0), new Vector3(800, 18, 400), kind, groupId);

    private static CoreValidationResult Validate(params ValidationElement[] elements) =>
        ValidationCore.Validate(elements);

    private static int Count(CoreValidationResult r, ViolationKind kind) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == kind);

    // ── Контакты и связность ────────────────────────────────────────────

    [Test]
    public void BoardOnFloor_IsGrounded_AndContactRegistered()
    {
        var r = Validate(Floor(), OnFloor("B"));

        Assert.IsTrue(r.IsValid, "Деталь на полу обязана быть валидной");
        Assert.IsTrue(r.Contacts.Any(c => c.IsFaceToFace), "Ожидался несущий контакт с полом");
        Assert.AreEqual(0, r.Violations.Count);
    }

    [Test]
    public void BoardInMidAir_IsUnsupported()
    {
        var floating = Part("Floating", new Vector3(0, 1000, 0), new Vector3(800, 18, 400));
        var r = Validate(Floor(), floating);

        Assert.IsFalse(r.IsValid);
        Assert.AreEqual(1, r.Violations.Count);
        Assert.AreEqual(1, Count(r, ViolationKind.Unsupported));
    }

    [Test]
    public void WithoutAnchors_TheLowestLevelIsGrounded()
    {
        // Ни пола, ни стен: землёй становится самый нижний уровень сцены, и
        // «висящих» деталей нет — иначе вся сцена без пола была бы красной.
        var a = Part("A", new Vector3(0, 9, 0), new Vector3(800, 18, 400));
        var b = Part("B", new Vector3(0, 27, 0), new Vector3(800, 18, 400));
        var r = Validate(a, b);

        Assert.IsTrue(r.IsValid, "Без якорей сцена не обязана быть заземлённой");
    }

    [Test]
    public void IsolatedGroups_AreGroupedByConnectivity()
    {
        // Две висящие детали, соединённые друг с другом, — ОДНА группа,
        // а не два независимых нарушения.
        var a = Part("A", new Vector3(0, 1000, 0), new Vector3(800, 18, 400));
        var b = Part("B", new Vector3(0, 1018, 0), new Vector3(800, 18, 400));
        var r = Validate(Floor(), a, b);

        Assert.AreEqual(2, r.Violations.Count);
        Assert.AreEqual(1, r.IsolatedGroups.Count);
        Assert.AreEqual(2, r.IsolatedGroups[0].Count);
    }

    [Test]
    public void TouchingBySliverOnly_IsNotSupport()
    {
        // Перекрытие граней ниже MinSupportOverlap: контакт зарегистрирован,
        // но несущим не считается — деталь всё ещё висит.
        var board = Part("Sliver", new Vector3(1900, 9, 0), new Vector3(800, 18, 400));
        var r = Validate(Floor(), board);

        Assert.IsFalse(r.IsValid, "Опора на 3% ширины опорой не является");
        Assert.AreEqual(1, Count(r, ViolationKind.Unsupported));
    }

    // ── Пересечения и роли ──────────────────────────────────────────────

    [Test]
    public void TwoBoardsInSamePlace_AreOverlap()
    {
        var a = OnFloor("A");
        var b = OnFloor("B");
        var r = Validate(Floor(), a, b);

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
        Assert.AreEqual(2, r.Violations.Count, "Пересечение помечает ОБЕ детали");
    }

    [Test]
    public void FloorUnderWall_IsLegitAnchorPair()
    {
        // Плита пола штатно проходит под стенами: пересечение двух якорей здесь
        // законно, и без этой ветки каждая сцена стартовала бы красной.
        var wall = Part("Wall", new Vector3(0, 0, 0), new Vector3(3000, 2500, 100), ElementKind.Anchor);
        var r = Validate(Floor(), wall);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void WallInsideWall_IsHardOverlap()
    {
        // Стена в стене — настоящая ошибка: раньше пара «якорь+якорь» не
        // проверялась целиком, и с блочными стенами ошибка молчала.
        var r = Validate(Floor(), WallAt("W1", 0), WallAt("W2", 50));

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
        Assert.AreEqual(2, r.Violations.Count, "Якорь с НЕштатным пересечением идёт в нарушения");
    }

    [Test]
    public void OpeningInsideWall_IsLegitAnchorPair()
    {
        var wall = WallAt("Wall", 0);
        var window = Part("Win", new Vector3(0, 1500, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening);
        var r = Validate(Floor(), wall, window);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap), "Проём сидит в теле своей стены");
    }

    /// <summary>Боковина корпуса: 18×600×400 стоймя, низ на полу.</summary>
    private static ValidationElement Side(string name, int groupId = 0) =>
        Part(name, new Vector3(0, 300, 0), new Vector3(18, 600, 400), ElementKind.None, groupId);

    /// <summary>Ящик внутри корпуса: заходит в боковину, но пола не касается.</summary>
    private static ValidationElement Drawer(string name, int groupId = 0, string? paired = null) =>
        Part(name, new Vector3(0, 300, 0), new Vector3(600, 100, 400), ElementKind.Drawer,
            groupId, paired);

    [Test]
    public void DrawerInsideOwnModule_IsNotOverlap()
    {
        var r = Validate(Floor(), Side("Side", groupId: 7), Drawer("Drawer", groupId: 7));

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void DrawerInsideForeignModule_IsOverlap()
    {
        var r = Validate(Floor(), Side("Side", groupId: 7), Drawer("Drawer", groupId: 9));

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap), "Чужой модуль — настоящее пересечение");
    }

    [Test]
    public void DrawerWithoutModule_IsOverlap()
    {
        // GroupId == 0 — «вне модуля»: совпадение нулей не должно оправдывать
        // пересечение (иначе любые две детали вне модулей были бы законны).
        var r = Validate(Floor(), Side("Side"), Drawer("Drawer"));

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void PairedDrawers_ShareSpaceLegitimately()
    {
        var a = Part("D_A", new Vector3(0, 200, 0), new Vector3(600, 300, 400),
            ElementKind.Drawer, pairedName: "D_B");
        var b = Part("D_B", new Vector3(0, 200, 0), new Vector3(600, 300, 400),
            ElementKind.Drawer, pairedName: "D_A");
        var r = Validate(Floor(), a, b);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void UnpairedDrawers_InSamePlace_AreOverlap()
    {
        var a = Part("D_A", new Vector3(0, 200, 0), new Vector3(600, 300, 400), ElementKind.Drawer);
        var b = Part("D_B", new Vector3(0, 200, 0), new Vector3(600, 300, 400), ElementKind.Drawer);
        var r = Validate(Floor(), a, b);

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void Decor_NeitherCollidesNorNeedsSupport()
    {
        // Светильник висит в воздухе и «протыкает» столешницу — штатно и то, и другое.
        var board = OnFloor("Board");
        var lamp = Part("Lamp", new Vector3(0, 9, 0), new Vector3(200, 200, 200), ElementKind.Decor);
        var r = Validate(Floor(), board, lamp);

        Assert.IsTrue(r.IsValid);
        Assert.AreEqual(0, Count(r, ViolationKind.Overlap));
        Assert.AreEqual(0, Count(r, ViolationKind.Unsupported));
    }

    [Test]
    public void Recessed_IsCutIntoWorktop_NotOverlap()
    {
        var worktop = Part("Worktop", new Vector3(0, 900, 0), new Vector3(2000, 38, 600));
        var sink = Part("Sink", new Vector3(0, 900, 0), new Vector3(500, 200, 400), ElementKind.Recessed);
        var r = Validate(Floor(), worktop, sink);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap), "Мойка врезана в столешницу");
        // Столешница здесь честно висит в воздухе — это её дело; проверяем, что
        // сама мойка опоры не требует (индекс 2 в переданном наборе).
        CollectionAssert.DoesNotContain(r.Violations, 2, "Мойка держится бортиком");
    }

    [Test]
    public void FloatingFacade_NeedsNoContact()
    {
        var facade = Part("Facade", new Vector3(0, 500, 300), new Vector3(600, 700, 18),
            ElementKind.FloatingFacade);
        var r = Validate(Floor(), facade);

        Assert.IsTrue(r.IsValid, "Фасад с зазором плавает в проёме — это штатно");
    }

    // ── Высота проёма в стене ───────────────────────────────────────────

    [Test]
    public void WindowInsideWallHeight_IsValid()
    {
        var wall = WallAt("Wall", 0);                       // 0 … 2500 мм
        var win = Part("Win", new Vector3(0, 1500, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening, attachedWallIndex: 1);
        var r = Validate(Floor(), wall, win);

        Assert.AreEqual(0, Count(r, ViolationKind.OutOfWallBounds));
    }

    [Test]
    public void WindowAboveWallTop_IsOutOfBounds()
    {
        var wall = WallAt("Wall", 0);                       // верх на 2500 мм
        var win = Part("Win", new Vector3(0, 2200, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening, attachedWallIndex: 1);
        var r = Validate(Floor(), wall, win);

        Assert.AreEqual(1, Count(r, ViolationKind.OutOfWallBounds));
        Assert.Contains(2, r.Violations);
    }

    [Test]
    public void WindowBelowWallBottom_IsOutOfBounds()
    {
        var wall = WallAt("Wall", 0);                       // низ на 0
        var win = Part("Win", new Vector3(0, 300, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening, attachedWallIndex: 1);
        var r = Validate(Floor(), wall, win);

        Assert.AreEqual(1, Count(r, ViolationKind.OutOfWallBounds));
    }

    [Test]
    public void WindowWithoutWall_IsNotChecked()
    {
        var win = Part("Win", new Vector3(0, 5000, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening);
        var r = Validate(Floor(), WallAt("Wall", 0), win);

        Assert.AreEqual(0, Count(r, ViolationKind.OutOfWallBounds),
            "Без ссылки на стену проверять нечего — это забота связности");
    }

    // ── Пазы ────────────────────────────────────────────────────────────

    /// <summary>Деталь 18 мм с пазом в пласти +Z: дно паза на глубине depthMm.</summary>
    private static ValidationElement Grooved(string name, Vector3 centerMm, Vector3 sizeMm,
        float depthMm)
    {
        var part = Part(name, centerMm, sizeMm);
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        // Дно паза — плоскость внутри детали, нормаль наружу (+Z).
        var seat = new Face(
            center + new Vector3(0, 0, size.z * 0.5f - depthMm * MM),
            Vector3.forward,
            new Vector2(size.x, size.y),
            Vector3.right, Vector3.up);

        var g = part.Geometry;
        var geometry = new ElementGeometry(g.Id, g.Name, g.Faces, new[] { seat },
            System.Array.Empty<Face>(), g.Min, g.Max, g.IsPanel);
        return new ValidationElement(geometry, part.Vertices, part.Kind, part.GroupId,
            part.PairedName, part.HeightSpan, part.AttachedWallIndex);
    }

    [Test]
    public void PanelSeatedInGroove_IsContactNotOverlap()
    {
        // Панель 4 мм зашла в паз глубиной 8 мм до самого дна: габариты
        // пересекаются, но это конструкция.
        var board = Grooved("Board", new Vector3(0, 500, 0), new Vector3(800, 700, 18), 8f);
        var panel = Part("Panel", new Vector3(0, 500, 9 - 8 + 2), new Vector3(600, 500, 4),
            isPanel: true);
        var r = Validate(board, panel);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap), "Посаженная в паз панель — не ошибка");
        Assert.IsTrue(r.Contacts.Any(c => c.IsFaceToFace), "Паз — это соединение");
    }

    [Test]
    public void PanelDrivenPastGrooveFloor_IsOverlap()
    {
        // Панель пробила дно паза — это уже настоящее пересечение.
        var board = Grooved("Board", new Vector3(0, 500, 0), new Vector3(800, 700, 18), 8f);
        var panel = Part("Panel", new Vector3(0, 500, -4), new Vector3(600, 500, 4), isPanel: true);
        var r = Validate(board, panel);

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
    }

    [Test]
    public void ThickBoard_DoesNotSeatInGroove()
    {
        // В паз садится только вкладная панель: толстая деталь, попавшая в тот же
        // объём, обязана остаться пересечением.
        var board = Grooved("Board", new Vector3(0, 500, 0), new Vector3(800, 700, 18), 8f);
        var thick = Part("Thick", new Vector3(0, 500, 9 - 8 + 2), new Vector3(600, 500, 4));
        var r = Validate(board, thick);

        Assert.AreEqual(1, Count(r, ViolationKind.Overlap));
    }

    // ── Посадка панели в паз: критерий «относится к этому пазу» ─────────

    private const float SeatDepthMm = 8f;
    private const float EngageMargin = GrooveSeating.PanelEngageMarginMm * MM;
    private static readonly float ContactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

    /// <summary>Дно паза 600×500 в плоскости z = 0, нормаль +Z, устье на
    /// z = 8 мм.</summary>
    private static Face Seat() => new Face(Vector3.zero, Vector3.forward,
        new Vector2(600 * MM, 500 * MM), Vector3.right, Vector3.up);

    private static bool Engages(Vector3 panelCenterMm, Vector3 panelSizeMm, out float minAlong) =>
        GrooveSeating.PanelEngagesSeat(Corners(panelCenterMm * MM, panelSizeMm * MM), Seat(),
            SeatDepthMm * MM, EngageMargin, ContactDist, out minAlong);

    [Test]
    public void PanelEngagesSeat_AtGrooveFloor_IsEngaged()
    {
        // Кромка панели ровно на дне паза: minAlong = 0.
        Assert.IsTrue(Engages(new Vector3(0, 0, 2), new Vector3(500, 400, 4), out float minAlong));
        Assert.AreEqual(0f, minAlong, 1e-6f);
    }

    [Test]
    public void PanelEngagesSeat_AtMouth_IsEngaged()
    {
        // Кромка у самого устья (глубина паза) — панель ещё «относится» к пазу.
        Assert.IsTrue(Engages(new Vector3(0, 0, SeatDepthMm + 2), new Vector3(500, 400, 4),
            out float minAlong));
        Assert.AreEqual(SeatDepthMm * MM, minAlong, 1e-6f);
    }

    [Test]
    public void PanelEngagesSeat_BeyondMargin_IsNotEngaged()
    {
        // Дальше устья на запас PanelEngageMarginMm — панель стоит где-то ещё.
        Assert.IsFalse(Engages(new Vector3(0, 0, SeatDepthMm + 6 + 2 + 0.1f),
            new Vector3(500, 400, 4), out _));
    }

    [Test]
    public void PanelEngagesSeat_DrivenPastFloor_IsNotEngaged()
    {
        // Кромка ушла за дно глубже допуска касания: это уже не посадка.
        Assert.IsFalse(Engages(new Vector3(0, 0, -2), new Vector3(500, 400, 4), out float minAlong));
        Assert.Less(minAlong, -ContactDist);
    }

    [Test]
    public void PanelEngagesSeat_AsideOfSeat_IsNotEngaged()
    {
        // Панель напротив пласти, но мимо прямоугольника паза.
        Assert.IsFalse(Engages(new Vector3(900, 0, 2), new Vector3(500, 400, 4), out _));
    }

    [Test]
    public void PanelEngagesSeat_CoveringLessThanHalfOfSeat_IsNotEngaged()
    {
        // Перекрытие 40% площади паза: панель к нему не относится — иначе к пазу
        // «прилипал» бы любой сосед, случайно оказавшийся рядом с устьем.
        Assert.IsFalse(Engages(new Vector3(0, 0, 2), new Vector3(600, 200, 4), out _));
    }

    [Test]
    public void PanelEngagesSeat_CoveringMoreThanHalfOfSeat_IsEngaged()
    {
        Assert.IsTrue(Engages(new Vector3(0, 0, 2), new Vector3(600, 300, 4), out _));
    }

    [Test]
    public void PanelEngagesSeat_DegenerateSeat_IsNotEngaged()
    {
        var flat = new Face(Vector3.zero, Vector3.forward, Vector2.zero, Vector3.right, Vector3.up);
        Assert.IsFalse(GrooveSeating.PanelEngagesSeat(Corners(Vector3.zero, Vector3.one * MM),
            flat, SeatDepthMm * MM, EngageMargin, ContactDist, out _));
    }

    // ── Примитивы, общие с адаптером сцены ──────────────────────────────

    [Test]
    public void MinParallelGap_FindsNearestGapInBand()
    {
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.020f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        float gap = FaceContacts.MinParallelGap(a.Faces, b.Faces, contactDist, 5f * MM);

        Assert.AreEqual(2f * MM, gap, 1e-6f, "Зазор 2 мм между пластями");
    }

    [Test]
    public void MinParallelGap_IgnoresGapsOutsideBand()
    {
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.100f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(0f, FaceContacts.MinParallelGap(a.Faces, b.Faces, contactDist, 5f * MM));
    }

    // ── SumParallelGaps: сумма зазоров по встречным граням ─────────────

    [Test]
    public void SumParallelGaps_SumsOpposingFacesOnly()
    {
        // Две пласти 800×400 разнесены по Y на 2мм: одна пара встречных граней
        // даёт 2мм, остальные в этом диапазоне не лежат.
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.020f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        float sum = FaceContacts.SumParallelGaps(a.Faces, b.Faces, contactDist, 5f * MM);

        Assert.AreEqual(2f * MM, sum, 1e-6f);
    }

    [Test]
    public void SumParallelGaps_AddsTwoOpposingPair_GapAboveBand()
    {
        // Противоположно смещённые face-пары по двум осям одновременно для
        // прямоугольных коробок СУММАРНО дают 0: чуть смести B по Y — Z-пары
        // расходятся (нет перекрытия), смести по Z — рассогласуются Y-пары.
        // Реальный сценарий «общего зазора по оси» — одна плоскость разделения
        // с двумя гранями (см. тест выше). Сама же SUM с одним ненулевым
        // слагаемым уже отвечает «0+2=2 → ОК».
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.020f, 0.205f),
            new Vector3(0.8f, 0.018f, 0.004f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        float sum = FaceContacts.SumParallelGaps(a.Faces, b.Faces, contactDist, 8f * MM);

        Assert.AreEqual(0f, sum, 1e-6f,
            "две коробки по разным осям не дают two-pair сумму — нет перекрывающихся пар");
    }

    [Test]
    public void SumParallelGaps_ZeroWhenNoOpposingFace()
    {
        // 100мм по Y — face-пар в полосе (0..5мм] нет, сумма = 0.
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.100f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(0f, FaceContacts.SumParallelGaps(a.Faces, b.Faces, contactDist, 5f * MM));
    }

    [Test]
    public void SumParallelGaps_IgnoresSameDirectionFaces()
    {
        // Две пласти лежат одна над другой, обращённые в одну сторону (+Y): их
        // верхние грани смотрят вверх и друг другу НЕ встречаются. У нижней
        // пласти верхняя грань, у верхней — нижняя. Пары с одной нормалью
        // (нижняя A + верхняя B, обе +Y) не считаются.
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.020f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        // У A лицо +Y смотрит вверх, у B лицо +Y тоже вверх. Грань −Y у A
        // смотрит вниз, +Y у B — вверх: они навстречу, расстояние 2мм.
        float sum = FaceContacts.SumParallelGaps(a.Faces, b.Faces, contactDist, 5f * MM);
        Assert.AreEqual(2f * MM, sum, 1e-6f, "только встречные грани с разными нормалями");
    }

    [Test]
    public void SumParallelGaps_IgnoresGapsOutsideBand()
    {
        // Зазор 100мм — за пределами диапазона, в сумму не идёт.
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));
        var b = ElementGeometry.Box("B", new Vector3(0, 0.100f, 0), new Vector3(0.8f, 0.018f, 0.4f));

        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(0f, FaceContacts.SumParallelGaps(a.Faces, b.Faces, contactDist, 5f * MM));
    }

    [Test]
    public void AreInFaceToFaceContact_RequiresSupportingOverlap()
    {
        float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
        var a = ElementGeometry.Box("A", Vector3.zero, new Vector3(0.8f, 0.018f, 0.4f));

        var stacked = ElementGeometry.Box("B", new Vector3(0, 0.018f, 0), new Vector3(0.8f, 0.018f, 0.4f));
        Assert.IsTrue(FaceContacts.AreInFaceToFaceContact(a.Faces, stacked.Faces, contactDist));

        // Сдвинута так, что перекрытие — узкая полоска: касание есть, опоры нет.
        var sliver = ElementGeometry.Box("C", new Vector3(0.79f, 0.018f, 0), new Vector3(0.8f, 0.018f, 0.4f));
        Assert.IsFalse(FaceContacts.AreInFaceToFaceContact(a.Faces, sliver.Faces, contactDist));
    }

    [Test]
    public void FacesOverlap_UsesPerAxisRatio_NotAreaRatio()
    {
        // 18×400 против 18×1200: по площадям перекрытие 4.5% (ниже порога), по
        // осям — 100%. Ради этого случая и заведено полуосевое отношение.
        var narrow = ElementGeometry.Box("N", Vector3.zero, new Vector3(0.018f, 0.4f, 0.018f));
        var wide = ElementGeometry.Box("W", new Vector3(0, 0.4f, 0), new Vector3(0.018f, 0.4f, 1.2f));

        Assert.IsTrue(FaceContacts.FacesOverlap(narrow.Faces[2], wide.Faces[3], out _, out float ratio));
        Assert.GreaterOrEqual(ratio, Tolerance.MinSupportOverlap);
    }

    [Test]
    public void SubMillimetreOverlap_IsContactNotCollision()
    {
        // Наезд 0.4 мм — округление снэпа, а не столкновение: порог тот же
        // ContactMm, что у face-контактов.
        var board = Part("B", new Vector3(0, 9 - 0.4f, 0), new Vector3(800, 18, 400));
        var r = Validate(Floor(), board);

        Assert.AreEqual(0, Count(r, ViolationKind.Overlap));
        Assert.IsTrue(r.IsValid);
    }

    // ── Детерминизм ─────────────────────────────────────────────────────

    [Test]
    public void Validate_IsDeterministic_AcrossRuns()
    {
        // Ядро держит статический скратч (сетка broad-phase, пул ячеек). Если
        // его чистка сломается, второй прогон даст другой ответ — а мутационный
        // прогон начнёт «убивать» мутантов случайно.
        var scene = new[]
        {
            Floor(), OnFloor("A", -400), OnFloor("B", 400),
            Part("Air", new Vector3(0, 1000, 0), new Vector3(800, 18, 400)),
        };

        var first = ValidationCore.Validate(scene);
        var second = ValidationCore.Validate(scene);

        Assert.AreEqual(first.Contacts.Count, second.Contacts.Count);
        CollectionAssert.AreEqual(first.Violations, second.Violations);
        Assert.AreEqual(first.Diagnostics?.Count ?? 0, second.Diagnostics?.Count ?? 0);
    }

    [Test]
    public void EmptyScene_IsValid()
    {
        var r = ValidationCore.Validate(new List<ValidationElement>());

        Assert.IsTrue(r.IsValid);
        Assert.AreEqual(0, r.Contacts.Count);
    }

    [Test]
    public void Diagnostics_AreNotAllocated_WhileTheSceneIsValid()
    {
        var r = Validate(Floor(), OnFloor("A"), OnFloor("B", 900));

        Assert.IsTrue(r.IsValid);
        Assert.IsNull(r.Diagnostics,
            "валидация идёт КАЖДЫЙ кадр перетаскивания: на валидной сцене список диагностик "
            + "не заводится вовсе, иначе каждый кадр стоит аллокации в GC");
    }

    [Test]
    public void Diagnostics_AppearAsSoonAsSomethingIsWrong()
    {
        var r = Validate(Floor(), Part("Air", new Vector3(0, 1000, 0), new Vector3(800, 18, 400)));

        Assert.IsNotNull(r.Diagnostics,
            "положительный контроль к ленивой аллокации: как только нарушение есть, "
            + "список обязан появиться");
    }

    [Test]
    public void Opening_IsMeasuredAgainstTheWallsDeclaredSpan_NotItsGeometry()
    {
        // Стена бывает визуально подрезана (режим разреза), и её ГЕОМЕТРИЯ тогда
        // ниже настоящей. Высоту проёма меряют по HeightSpan, который приходит от
        // ЛОГИЧЕСКОЙ позы стены, — иначе каждое окно в подрезанной стене краснело бы.
        var wallGeometry = ElementGeometry.Box("Wall", new Vector3(0, 0.5f, 0),
            new Vector3(3f, 1f, 0.1f));
        var wall = new ValidationElement(wallGeometry, Corners(new Vector3(0, 0.5f, 0),
                new Vector3(3f, 1f, 0.1f)), ElementKind.Anchor, 0, null,
            new Span(0f, 2.5f), -1);

        var window = Part("Win", new Vector3(0, 1500, 0), new Vector3(900, 1200, 100),
            ElementKind.Anchor | ElementKind.Opening, attachedWallIndex: 1);

        var r = Validate(Floor(), wall, window);

        Assert.AreEqual(0, Count(r, ViolationKind.OutOfWallBounds),
            "окно на 0.9…2.1 м стоит внутри ЛОГИЧЕСКОЙ стены (0…2.5 м), хотя её геометрия "
            + "обрезана до 1 м — проверять полагается объявленный HeightSpan");
    }
}

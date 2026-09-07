using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Угол помещения — это НАХЛЁСТ, и он законен.
///
/// Стены строятся по осевым линиям: <c>create_walls</c> берёт пару точек, кладёт
/// коробку длиной ровно между ними и ставит ей ус (<c>ComputeWallEndShapes</c>).
/// Две стены, сошедшиеся в общей точке, поэтому ОБЯЗАНЫ пересечься телами на
/// четверть угла — полтолщины на полтолщины, во всю высоту, — а ус убирает это
/// с глаз. Ядро же видело только AABB: два якоря, ни один не пол и не проём,
/// значит COL-01. Комната из четырёх стен давала четыре ошибки на ровном месте,
/// и глазами их не было видно никогда — стена красится своим декором раньше,
/// чем доходит до валидационного тона.
///
/// Поэтому прощение выдаётся не «стенам вообще», а ОСЕВЫМ ЛИНИЯМ: пара законна,
/// когда линии не параллельны и упираются друг в друга концами. Ровно тот же
/// признак, по которому <c>JointShift</c> решает резать ус, — иначе прощение и
/// ус разошлись бы в разные стороны.
///
/// Каждому разрешению здесь отвечает парный контроль, который обязан остаться
/// красным: тавр в тело, проскок мимо угла, дубль стены. Иначе починка была бы
/// глушением.</summary>
public class WallCornerJointTests
{
    private const float MM = 0.001f;

    private const int RoomMM = 3000;
    private const int WallHeightMM = 2500;
    private const int WallThicknessMM = 100;

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

    /// <summary>Стена без поворота: габарит в мм, центр в мм. Осевая линия
    /// выводится ТОЙ ЖЕ функцией, которой её выводит снимок сцены, — иначе тест
    /// проверял бы собственную арифметику вместо продакшена.</summary>
    private static ValidationElement Wall(string name, Vector3 centerMm, Vector3Int dimsMm,
        bool withCentreline = true)
    {
        Vector3 center = centerMm * MM;
        var size = new Vector3(dimsMm.x, dimsMm.y, dimsMm.z) * MM;
        var geometry = ElementGeometry.Box(name, center, size);
        return new ValidationElement(geometry, Corners(center, size), ElementKind.Anchor,
            ValidationElement.NoGroup, null, Span.FromCenter(center.y, size.y),
            ValidationElement.NoIndex, default, false, ValidationElement.NoIndex,
            withCentreline
                ? WallCentreline.Of(center, Quaternion.identity, dimsMm)
                : default);
    }

    /// <summary>Комната 3000×3000 из кадра IsoRoom_*: четыре стены по периметру,
    /// осевые линии сходятся в четырёх общих точках.</summary>
    private static List<ValidationElement> Room(bool withCentrelines = true)
    {
        const float half = RoomMM * 0.5f;
        const float y = WallHeightMM * 0.5f;
        var alongX = new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM);
        var alongZ = new Vector3Int(WallThicknessMM, WallHeightMM, RoomMM);
        return new List<ValidationElement>
        {
            Wall("Wall_N", new Vector3(0, y, -half), alongX, withCentrelines),
            Wall("Wall_S", new Vector3(0, y, half), alongX, withCentrelines),
            Wall("Wall_W", new Vector3(-half, y, 0), alongZ, withCentrelines),
            Wall("Wall_E", new Vector3(half, y, 0), alongZ, withCentrelines),
        };
    }

    private static int Overlaps(CoreValidationResult r) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == ViolationKind.Overlap);

    private static string Pairs(CoreValidationResult r, IReadOnlyList<ValidationElement> all)
    {
        if (r.Diagnostics == null) return "(диагностик нет)";
        var lines = new List<string>();
        foreach (var d in r.Diagnostics)
        {
            if (d.Kind != ViolationKind.Overlap) continue;
            string other = d.Other >= 0 ? all[d.Other].Name : "—";
            var a = all[d.Element].Geometry;
            string overlap = "—";
            if (d.Other >= 0)
            {
                var b = all[d.Other].Geometry;
                Vector3 o = Vector3.Min(a.Max, b.Max) - Vector3.Max(a.Min, b.Min);
                overlap = $"{o.x / MM:F0}×{o.y / MM:F0}×{o.z / MM:F0} мм";
            }
            lines.Add(all[d.Element].Name + " × " + other + " на " + overlap);
        }
        return string.Join("; ", lines);
    }

    // ── Разрешение ──────────────────────────────────────────────────────

    [Test]
    public void Room_FourWallsSharingCornerPoints_HasNoOverlapViolationAtAll()
    {
        var room = Room();

        var r = ValidationCore.Validate(room);

        Assert.AreEqual(0, Overlaps(r),
            "комната из четырёх стен обязана давать НОЛЬ COL-01: углы — это ус, а не дефект. "
            + "Получено " + Overlaps(r) + ": " + Pairs(r, room));
        Assert.AreEqual(0, r.Violations.Count,
            "и ни одна стена не попадает в список проблем: " + Pairs(r, room));
    }

    /// <summary>Отрицательный контроль к самому прощению: та же комната без
    /// осевых линий — это ядро ДО правки, и оно обязано дать все четыре COL-01.
    /// Без этого теста «ноль» выше нельзя отличить от «правило вообще не
    /// сработало» — например, если бы широкая фаза перестала сводить эти пары.</summary>
    [Test]
    public void Room_WhenTheWallsCarryNoCentreline_StillReportsFourOverlaps()
    {
        var room = Room(withCentrelines: false);

        var r = ValidationCore.Validate(room);

        Assert.AreEqual(4, Overlaps(r),
            "четыре угла — четыре пересечения; если их стало меньше, пары перестали доходить "
            + "до проверки, и «ноль» в соседнем тесте ничего не доказывает. Получено: "
            + Pairs(r, room));
        Assert.AreEqual(4, r.Violations.Count,
            "и краснели все четыре стены — по одной на угол, ровно то, что видел пользователь "
            + "в списке проблем любого замкнутого помещения");
    }

    [Test]
    public void RoomCorner_OverlapsByHalfThicknessSquared_AtFullWallHeight()
    {
        var room = Room();
        var north = room[0].Geometry;
        var west = room[2].Geometry;

        Vector3 overlap = Vector3.Min(north.Max, west.Max) - Vector3.Max(north.Min, west.Min);

        Assert.AreEqual(WallThicknessMM * 0.5f, overlap.x / MM, 0.01f,
            "нахлёст в углу — ПОЛтолщины, а не толщина: осевые сходятся в точке, "
            + "и каждая стена доходит до неё, но не дальше");
        Assert.AreEqual(WallThicknessMM * 0.5f, overlap.z / MM, 0.01f,
            "и по второй оси столько же — угол квадратный, обе стены одной толщины");
        Assert.AreEqual(WallHeightMM, overlap.y / MM, 0.01f,
            "и он идёт во всю высоту стены");
    }

    // ── Парный контроль: что обязано остаться красным ────────────────────

    /// <summary>Тавр: торец второй стены упирается не в КОНЕЦ первой, а в её
    /// середину и проходит тело насквозь. Общей точки осевых нет, прощения нет.</summary>
    [Test]
    public void Walls_MeetingAsATeeInTheMiddleOfTheOther_IsStillOverlap()
    {
        var all = new List<ValidationElement>
        {
            Wall("Wall_Long", new Vector3(0, 1350, 0), new Vector3Int(3000, 2700, 250)),
            Wall("Wall_Tee", new Vector3(0, 1350, 875), new Vector3Int(250, 2700, 2000)),
        };

        var r = ValidationCore.Validate(all);

        Assert.AreEqual(1, Overlaps(r),
            "стена, въехавшая в ТЕЛО другой, обязана остаться нарушением — иначе прощение "
            + "угла закрыло бы глаза на любой наезд. Получено: " + Pairs(r, all));
    }

    /// <summary>Проскок: угол сложен, но вторая стена длиннее нужного и уходит
    /// за общую точку на 60 мм. Осевые расходятся — значит красное.</summary>
    [Test]
    public void Walls_WhoseEndsMissTheSharedCornerBySixtyMillimetres_IsStillOverlap()
    {
        const float half = RoomMM * 0.5f;
        var all = new List<ValidationElement>
        {
            Wall("Wall_N", new Vector3(0, 1250, -half),
                new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM)),
            Wall("Wall_W", new Vector3(-half, 1250, -30f),
                new Vector3Int(WallThicknessMM, WallHeightMM, RoomMM + 60)),
        };

        var r = ValidationCore.Validate(all);

        Assert.AreEqual(1, Overlaps(r),
            "60 мм мимо угла — это уже наезд, а не ус: прощается СОВПАДЕНИЕ концов, "
            + "а не соседство. Получено: " + Pairs(r, all));
    }

    /// <summary>Дубль: две одинаковые стены на одном месте делят ОБА конца, но
    /// параллельны — угла тут нет, и прощать нечего.</summary>
    [Test]
    public void Walls_ParallelAndSharingBothEnds_IsStillOverlap()
    {
        var all = new List<ValidationElement>
        {
            Wall("Wall_A", new Vector3(0, 1250, 0),
                new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM)),
            Wall("Wall_B", new Vector3(0, 1250, 0),
                new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM)),
        };

        var r = ValidationCore.Validate(all);

        Assert.AreEqual(1, Overlaps(r),
            "дубль стены совпадает концами и прошёл бы как «угол», не спроси правило про "
            + "параллельность. Получено: " + Pairs(r, all));
    }

    // ── Допуск назван и закреплён с обеих сторон ────────────────────────

    [Test]
    public void SharedCorner_IsForgivenWithinItsNamedTolerance_AndNotBeyondIt()
    {
        var north = WallCentreline.Of(new Vector3(0, 1.25f, -1.5f), Quaternion.identity,
            new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM));

        var touching = WallCentreline.Of(
            new Vector3(-1.5f, 1.25f, -WallCentreline.CornerToleranceMm * MM * 0.5f),
            Quaternion.identity,
            new Vector3Int(WallThicknessMM, WallHeightMM, RoomMM));
        var missing = WallCentreline.Of(
            new Vector3(-1.5f, 1.25f, -WallCentreline.CornerToleranceMm * MM * 2f),
            Quaternion.identity,
            new Vector3Int(WallThicknessMM, WallHeightMM, RoomMM));

        Assert.IsTrue(WallCentreline.MeetAtSharedCorner(north, touching),
            "допуск на общую точку — " + WallCentreline.CornerToleranceMm + " мм: это запас на "
            + "арифметику координат, а не на расстановку, и в его пределах угол остаётся углом");
        Assert.IsFalse(WallCentreline.MeetAtSharedCorner(north, missing),
            "вдвое дальше допуска общей точки уже нет, и порог обязан это заметить — "
            + "иначе он не порог, а всегда-да");
    }

    // ── Ось стены: та же, по которой строится меш ────────────────────────

    [Test]
    public void Centreline_OfAWallLaidAlongZ_RunsAlongZ_NotAlongItsFirstDimension()
    {
        var line = WallCentreline.Of(new Vector3(-1.5f, 1.25f, 0f), Quaternion.identity,
            new Vector3Int(WallThicknessMM, WallHeightMM, RoomMM));

        Assert.IsTrue(line.IsDefined,
            "у стены осевая обязана получиться: неопределённая линия молча выключила бы "
            + "прощение и вернула четыре угловые ошибки");
        Assert.AreEqual(0f, line.Direction.x, 1e-4f,
            "стена 100×2500×3000 идёт вдоль Z: длина — это БОЛЬШИЙ горизонтальный габарит, "
            + "тот же выбор делает WallMeshBuilder, иначе ус и прощение смотрели бы "
            + "в разные стороны");
        Assert.AreEqual(1f, Mathf.Abs(line.Direction.z), 1e-4f,
            "направление единичное и целиком лежит в плане: по нему считается "
            + "и параллельность, и положение концов");
        Assert.AreEqual(RoomMM * MM, (line.End - line.Start).magnitude, 1e-4f,
            "длина осевой — это длина стены, иначе её концы окажутся не там, где угол");
    }

    [Test]
    public void Centreline_OfANonWall_IsUndefined_AndForgivesNothing()
    {
        var plain = Wall("Board", new Vector3(0, 1250, 0),
            new Vector3Int(RoomMM, WallHeightMM, WallThicknessMM), withCentreline: false);

        Assert.IsFalse(plain.Centreline.IsDefined,
            "осевая есть только у стены: снимок сцены заводит её, когда на элементе есть Wall");
        Assert.IsFalse(WallCentreline.MeetAtSharedCorner(plain.Centreline, plain.Centreline),
            "пустая осевая не имеет права прощать — иначе прощение получил бы каждый элемент");
    }
}

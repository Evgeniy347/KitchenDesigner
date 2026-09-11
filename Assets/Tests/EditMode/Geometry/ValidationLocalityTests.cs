using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ворота для будущей ИНКРЕМЕНТАЛЬНОЙ валидации.
///
/// Гипотеза, которую держит этот файл: правила <see cref="ValidationCore"/> —
/// чистая функция от (фактов ПАРЫ; графа контактов). Если это так, то во время
/// жеста можно один раз посчитать сцену БЕЗ движущейся детали («мувера»), а
/// каждый кадр досчитывать только пары «мувер против статического индекса» и
/// пересчитывать связность: мувер способен лишь ДОБАВИТЬ опору и лишь ДОБАВИТЬ
/// пересечение, поэтому переворачиваются ровно те детали, которых он касается,
/// и те, что достижимы от него по рёбрам опоры.
///
/// Тест сравнивает полную валидацию СО всеми деталями против полной валидации
/// БЕЗ мувера и требует, чтобы множество деталей со ИЗМЕНИВШИМСЯ статусом
/// лежало ВНУТРИ «соседи мувера ∪ достижимое от мувера». Свип гоняет разные
/// позы: внесение пересечения, подпирание висящей стопки, уход из-под соседа,
/// зависание в воздухе.
///
/// Сравнение идёт по ИМЕНАМ, а не по индексам, — и это не удобство, а
/// требование: <c>HostIndex</c> и <c>AttachedWallIndex</c> в
/// <see cref="ValidationElement"/> ссылаются на соседа по ПОРЯДКОВОМУ НОМЕРУ в
/// списке, поэтому будущая реализация обязана держать индексы стабильными
/// (мувер последним либо дырка на его месте), а не «вырезать» его из массива.
///
/// Найденное исключение живёт в
/// <see cref="SceneWithoutAnchors_ChangesStatusOfPartsTheMoverNeverTouches"/>:
/// без якоря корнем обхода становится ПЕРВАЯ деталь, у которой есть контакт, и
/// появление мувера меняет сам корень. Это единственное правило, которое
/// зависит не от пары и не от графа, а от ПОРЯДКА списка.</summary>
public class ValidationLocalityTests
{
    private const float MM = 0.001f;

    private static readonly float ContactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

    /// <summary>Статус одной детали: попала ли в нарушения и с какими кодами.
    /// Именно это видит подсветка, поэтому именно это и сравнивается.</summary>
    private readonly struct Status
    {
        public readonly bool IsViolation;
        public readonly string Kinds;

        public Status(bool isViolation, string kinds)
        {
            IsViolation = isViolation;
            Kinds = kinds;
        }

        public bool Differs(in Status other) =>
            IsViolation != other.IsViolation || Kinds != other.Kinds;

        public override string ToString() => IsViolation ? $"НАРУШЕНО[{Kinds}]" : "чисто";
    }

    private static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm,
        ElementKind kind = ElementKind.None)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var geometry = ElementGeometry.Box(name, center, size);
        return new ValidationElement(geometry, Corners(center, size), kind, 0, null,
            Span.FromCenter(center.y, size.y), ValidationElement.NoIndex);
    }

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

    /// <summary>Плотная кухня: пол-якорь и двенадцать модулей, стоящих вплотную
    /// друг к другу гранями — боковины, дно, полка, столешница. Плюс висящая
    /// стопка из двух досок, которой подпереться можно ТОЛЬКО мувером: без него
    /// она неподпёрта, с ним — встаёт на место.</summary>
    private static List<ValidationElement> DenseKitchenWithoutMover()
    {
        var parts = new List<ValidationElement>
        {
            Part("Floor", new Vector3(3000, -9, 0), new Vector3(9000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
        };

        for (int m = 0; m < Modules; m++)
        {
            float x0 = m * 600f;
            parts.Add(Part($"M{m}_SideL", new Vector3(x0 + 9, 360, 0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_SideR", new Vector3(x0 + 591, 360, 0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_Bottom", new Vector3(x0 + 300, 9, 0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Shelf", new Vector3(x0 + 300, 400, 0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Top", new Vector3(x0 + 300, 739, 0), new Vector3(600, 38, 600)));
        }

        parts.Add(Part("Hanging_Shelf", new Vector3(1200, 1009, 0), new Vector3(400, 18, 400)));
        parts.Add(Part("Hanging_Stack", new Vector3(1200, 1027, 0), new Vector3(400, 18, 400)));
        return parts;
    }

    private const int Modules = 12;

    /// <summary>Мувер — стойка 18×242×400. В «опорной» позе её низ лежит на
    /// столешнице (y = 758), верх упирается в дно висящей полки (y = 1000).</summary>
    private static ValidationElement Mover(Vector3 centerMm) =>
        Part("MOVER", centerMm, new Vector3(18, 242, 400));

    private static readonly Vector3 SupportingPose = new Vector3(1200, 879, 0);

    private static Status StatusOf(CoreValidationResult r, int index)
    {
        string kinds = r.Diagnostics == null
            ? string.Empty
            : string.Join(",", r.Diagnostics
                .Where(d => d.Element == index || d.Other == index)
                .Select(d => d.Kind.ToString())
                .Distinct()
                .OrderBy(s => s));
        return new Status(r.Violations.Contains(index), kinds);
    }

    private static Dictionary<string, Status> StatusesByName(List<ValidationElement> parts)
    {
        var r = ValidationCore.Validate(parts);
        var map = new Dictionary<string, Status>(parts.Count);
        for (int i = 0; i < parts.Count; i++) map[parts[i].Name] = StatusOf(r, i);
        return map;
    }

    /// <summary>«Соседи мувера ∪ достижимое от мувера по рёбрам опоры» — ровно
    /// та область, которую инкрементальный проход умеет пересчитать. Соседство
    /// берётся тем же критерием, которым его возьмёт статический индекс:
    /// габариты, раздутые на допуск контакта.</summary>
    private static HashSet<string> LocalRegion(List<ValidationElement> withMover, int moverIndex)
    {
        var full = ValidationCore.Validate(withMover);

        int n = withMover.Count;
        var region = new HashSet<string> { withMover[moverIndex].Name };

        var mover = withMover[moverIndex];
        for (int i = 0; i < n; i++)
        {
            if (i == moverIndex) continue;
            if (Reaches(mover.Geometry, withMover[i].Geometry)) region.Add(withMover[i].Name);
        }

        var adjacency = new List<int>[n];
        for (int i = 0; i < n; i++) adjacency[i] = new List<int>();
        foreach (var c in full.Contacts)
        {
            if (!c.IsFaceToFace) continue;
            adjacency[c.A].Add(c.B);
            adjacency[c.B].Add(c.A);
        }

        var visited = new bool[n];
        var queue = new Queue<int>();
        visited[moverIndex] = true;
        queue.Enqueue(moverIndex);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            region.Add(withMover[current].Name);
            foreach (int next in adjacency[current])
            {
                if (visited[next]) continue;
                visited[next] = true;
                queue.Enqueue(next);
            }
        }
        return region;
    }

    private static bool Reaches(in ElementGeometry a, in ElementGeometry b) =>
        a.Min.x <= b.Max.x + ContactDist && a.Max.x >= b.Min.x - ContactDist &&
        a.Min.y <= b.Max.y + ContactDist && a.Max.y >= b.Min.y - ContactDist &&
        a.Min.z <= b.Max.z + ContactDist && a.Max.z >= b.Min.z - ContactDist;

    /// <summary>Сравнивает сцену с мувером и без него и возвращает имена
    /// деталей, чей статус изменился. Мувер добавляется ПОСЛЕДНИМ, поэтому
    /// индексы остальных совпадают в обоих прогонах — иначе сравнение поймало
    /// бы сдвиг ссылок по индексу, а не работу правил.</summary>
    private static List<string> StatusDiff(Vector3 moverCenterMm, out int localSize,
        out int outsideRegion)
    {
        var without = DenseKitchenWithoutMover();
        var frozen = StatusesByName(without);

        var with = DenseKitchenWithoutMover();
        with.Add(Mover(moverCenterMm));
        int moverIndex = with.Count - 1;
        var live = StatusesByName(with);

        var region = LocalRegion(with, moverIndex);
        localSize = region.Count;

        var changed = new List<string>();
        foreach (var kv in frozen)
            if (kv.Value.Differs(live[kv.Key]))
                changed.Add(kv.Key);

        changed.Sort(System.StringComparer.Ordinal);
        outsideRegion = changed.Count(name => !region.Contains(name));
        return changed;
    }

    private static void AssertDiffStaysLocal(string pose, Vector3 moverCenterMm)
    {
        var without = DenseKitchenWithoutMover();
        var with = DenseKitchenWithoutMover();
        with.Add(Mover(moverCenterMm));
        var region = LocalRegion(with, with.Count - 1);

        var changed = StatusDiff(moverCenterMm, out int localSize, out int outside);
        var strays = changed.Where(name => !region.Contains(name)).ToList();

        TestContext.WriteLine(
            $"{pose}: деталей {without.Count}, изменилось {changed.Count}, " +
            $"область {localSize}, вне области {outside}" +
            (changed.Count > 0 ? $" — [{string.Join(", ", changed)}]" : string.Empty));

        Assert.IsEmpty(strays,
            $"{pose}: статус изменился у деталей ВНЕ области мувера — " +
            $"[{string.Join(", ", strays)}]. Значит какое-то правило зависит не от " +
            "фактов пары и не от графа контактов, и заморозка сцены на время жеста неверна.");
    }

    [Test]
    public void MoverSupportingAHangingStack_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка подпирает висящую стопку", SupportingPose);
    }

    [Test]
    public void MoverSlidOutFromUnderTheStack_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка ушла из-под соседа",
            SupportingPose + new Vector3(500, 0, 0));
    }

    [Test]
    public void MoverDrivenIntoAWorktop_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка внесена в столешницу", new Vector3(1200, 739, 0));
    }

    [Test]
    public void MoverDrivenIntoASidePanel_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка внесена в боковину", new Vector3(609, 360, 0));
    }

    [Test]
    public void MoverDrivenIntoTheFloorAnchor_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка утоплена в пол", new Vector3(1500, 100, 0));
    }

    [Test]
    public void MoverLyingOnTheFloorBetweenModules_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка лежит на полу", new Vector3(4000, 121, 900));
    }

    [Test]
    public void MoverHangingInMidAir_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка висит в воздухе", new Vector3(4000, 2000, 1200));
    }

    [Test]
    public void MoverTouchingTheStackButNotTheKitchen_ChangesNothingOutsideItsOwnReach()
    {
        AssertDiffStaysLocal("стойка держится за стопку, но сама ни на чём не стоит",
            SupportingPose + new Vector3(0, 60, 0));
    }

    /// <summary>Тот же свип обязан быть СПОСОБЕН найти разницу: если бы
    /// сравнение статусов ничего не замечало, все тесты выше были бы зелёными
    /// на пустом месте. Опорная поза переворачивает ровно висящую стопку.</summary>
    [Test]
    public void SupportingPose_ActuallyFlipsTheHangingStack()
    {
        var changed = StatusDiff(SupportingPose, out _, out _);

        CollectionAssert.Contains(changed, "Hanging_Shelf",
            "Без мувера полка обязана быть неподпёртой, с мувером — подпёртой");
        CollectionAssert.Contains(changed, "Hanging_Stack",
            "Доска НАД полкой обязана переворачиваться вместе с ней — транзитивно, по рёбрам опоры");
    }

    /// <summary>Единственное найденное правило, зависящее НЕ от пары и НЕ от
    /// графа: когда якоря в сцене нет вовсе, <c>CheckConnectivity</c> берёт
    /// корнем обхода ПЕРВУЮ по списку деталь, у которой есть хоть один контакт.
    /// Мувер, попавший в список раньше неё, забирает корень себе — и «землёй»
    /// становится его компонента, а та, что была землёй, целиком краснеет.
    /// Детали, которых мувер никогда не касался и от которых он недостижим,
    /// меняют статус.
    ///
    /// Для кухни это безвредно: пол — якорь. Поэтому тест не «падает», а
    /// ЗАКРЕПЛЯЕТ границу применимости заморозки: инкрементальный проход
    /// обязан требовать наличия якоря, либо считать корень отдельно.</summary>
    [Test]
    public void SceneWithoutAnchors_ChangesStatusOfPartsTheMoverNeverTouches()
    {
        var grounded = new List<ValidationElement>
        {
            Part("A0", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
            Part("A1", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
            Part("B0", new Vector3(5000, 9, 0), new Vector3(800, 18, 400)),
            Part("B1", new Vector3(5000, 27, 0), new Vector3(800, 18, 400)),
        };
        var frozen = StatusesByName(grounded);

        var withMover = new List<ValidationElement>
        {
            Part("MOVER", new Vector3(5000, -9, 0), new Vector3(800, 18, 400)),
        };
        withMover.AddRange(grounded);
        var live = StatusesByName(withMover);

        var region = LocalRegion(withMover, 0);

        Assert.IsFalse(region.Contains("A0"),
            "A0 не сосед мувера и от мувера недостижим — он ОБЯЗАН быть вне области");
        Assert.IsTrue(frozen["A0"].Differs(live["A0"]),
            $"Ожидалась смена статуса A0 из-за переезда корня обхода: было {frozen["A0"]}, " +
            $"стало {live["A0"]}");
        Assert.IsTrue(frozen["B0"].Differs(live["B0"]),
            $"Ожидалась смена статуса B0: было {frozen["B0"]}, стало {live["B0"]}");
    }
}

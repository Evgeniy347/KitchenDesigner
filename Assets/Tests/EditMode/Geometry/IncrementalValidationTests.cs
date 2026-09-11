using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Инкрементальная валидация на время жеста: сцена замораживается БЕЗ
/// движущихся деталей один раз, дальше каждый кадр досчитываются только пары
/// «мувер против статического индекса» и пересчитывается связность.
///
/// Ворота этой работы — <see cref="ValidationLocalityTests"/>: там доказано,
/// что правила <see cref="ValidationCore"/> зависят только от фактов пары и от
/// графа контактов. Здесь доказывается второе, без чего первое бесполезно:
/// результат инкрементального прохода СОВПАДАЕТ с полной валидацией. Не «почти»
/// — пользователь принимает решения по подсветке, и «почти правильно» здесь
/// хуже, чем медленно.
///
/// Сравнение идёт по отпечатку ВСЕГО результата: нарушения, диагностики,
/// контакты, изолированные группы, флаг валидности. Порядок внутри списков
/// каноникализуется, и это не послабление: полный проход укладывает нарушение
/// в момент обработки пары, инкрементальный — сначала замороженные, потом
/// мувера, а СОСТАВ обязан совпасть до элемента. Ни один потребитель
/// (<c>ElementHighlighter</c>, <c>SceneViolations</c>, <c>EditGate</c>) порядок
/// не читает — все три спрашивают «есть ли эта деталь с этим кодом».</summary>
public class IncrementalValidationTests
{
    private const float MM = 0.001f;

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

    /// <summary>Кухня из <paramref name="modules"/> модулей по пять деталей плюс
    /// пол-якорь: ряды по 20 модулей, ряды в 700 мм друг от друга — соседние
    /// корпуса почти касаются, как в настоящей плотной кухне. Плюс висящая
    /// стопка из двух досок, которую можно подпереть только мувером.</summary>
    private static List<ValidationElement> Kitchen(int modules)
    {
        var parts = new List<ValidationElement>
        {
            Part("Floor", new Vector3(6000, -9, 1000), new Vector3(20000, 18, 6000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
        };

        for (int m = 0; m < modules; m++)
        {
            float x0 = (m % 20) * 600f;
            float z0 = (m / 20) * 700f;
            parts.Add(Part($"M{m}_SideL", new Vector3(x0 + 9, 360, z0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_SideR", new Vector3(x0 + 591, 360, z0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_Bottom", new Vector3(x0 + 300, 9, z0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Shelf", new Vector3(x0 + 300, 400, z0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Top", new Vector3(x0 + 300, 739, z0), new Vector3(600, 38, 600)));
        }

        parts.Add(Part("Hanging_Shelf", new Vector3(1200, 1009, 0), new Vector3(400, 18, 400)));
        parts.Add(Part("Hanging_Stack", new Vector3(1200, 1027, 0), new Vector3(400, 18, 400)));
        return parts;
    }

    private static ValidationElement Mover(string name, Vector3 centerMm) =>
        Part(name, centerMm, new Vector3(18, 242, 400));

    /// <summary>Позы, через которые жест протаскивает мувер: воздух, опора под
    /// висящей стопкой, внесение в столешницу, в боковину, в пол, отход в
    /// сторону. Каждая обязана дать тот же ответ, что и полный проход.</summary>
    private static IEnumerable<Vector3> GesturePoses()
    {
        yield return new Vector3(4000, 2000, 2500);
        yield return new Vector3(1200, 879, 0);
        yield return new Vector3(1200, 739, 0);
        yield return new Vector3(609, 360, 0);
        yield return new Vector3(1500, 100, 0);
        yield return new Vector3(1700, 879, 0);
        yield return new Vector3(1200, 939, 0);
        yield return new Vector3(3000, 121, 700);
        yield return new Vector3(6000, 360, 1400);
        yield return new Vector3(300, 400, 0);
    }

    private static string Fingerprint(CoreValidationResult r, IReadOnlyList<ValidationElement> all)
    {
        var violations = r.Violations.Select(i => all[i].Name).Distinct().OrderBy(s => s,
            System.StringComparer.Ordinal);

        var diagnostics = (r.Diagnostics ?? new List<CoreViolation>())
            .Select(d => $"{all[d.Element].Name}|" +
                         $"{(d.Other >= 0 ? all[d.Other].Name : "-")}|{d.Kind}")
            .OrderBy(s => s, System.StringComparer.Ordinal);

        var contacts = r.Contacts
            .Select(c => $"{all[c.A].Name}|{all[c.B].Name}|{c.FaceA}|{c.FaceB}|" +
                         $"{c.Area.ToString("F9", System.Globalization.CultureInfo.InvariantCulture)}|" +
                         $"{c.IsFaceToFace}")
            .OrderBy(s => s, System.StringComparer.Ordinal);

        var groups = r.IsolatedGroups
            .Select(g => string.Join("+", g.Select(i => all[i].Name)
                .OrderBy(s => s, System.StringComparer.Ordinal)))
            .OrderBy(s => s, System.StringComparer.Ordinal);

        return $"valid={r.IsValid}\n" +
               $"violations[{r.Violations.Count}]:\n  {string.Join("\n  ", violations)}\n" +
               $"diagnostics:\n  {string.Join("\n  ", diagnostics)}\n" +
               $"groups:\n  {string.Join("\n  ", groups)}\n" +
               $"contacts:\n  {string.Join("\n  ", contacts)}";
    }

    private static string FullFingerprint(List<ValidationElement> parts) =>
        Fingerprint(ValidationCore.Validate(parts), parts);

    /// <summary>Свип одного мувера: заморозка ОДИН раз в стартовой позе, потом
    /// каждая поза сравнивается с полным проходом по той же сцене.</summary>
    private static void SweepOneMover(int modules)
    {
        var parts = Kitchen(modules);
        parts.Add(Mover("MOVER", new Vector3(4000, 2000, 2500)));
        int moverIndex = parts.Count - 1;

        var frozen = FrozenValidation.Freeze(parts, new[] { moverIndex });
        Assert.IsNotNull(frozen, "в сцене есть пол-якорь — заморозка обязана состояться");

        var live = new CoreValidationResult();
        foreach (var pose in GesturePoses())
        {
            parts[moverIndex] = Mover("MOVER", pose);

            frozen!.Revalidate(parts, live);
            string incremental = Fingerprint(live, parts);
            string full = FullFingerprint(parts);

            Assert.AreEqual(full, incremental,
                $"поза {pose}: инкрементальный проход разошёлся с полной валидацией");
        }
    }

    [Test]
    public void IncrementalPass_MatchesFullValidation_AcrossTheWholeGesture()
    {
        SweepOneMover(12);
    }

    [Test]
    public void IncrementalPass_MatchesFullValidation_OnAFourHundredPartScene()
    {
        SweepOneMover(80);
    }

    /// <summary>Групповое перетаскивание: замораживается сцена без ВСЕЙ группы,
    /// пары «мувер–мувер» внутри группы считаются отдельно. Группа едет целиком,
    /// поэтому её внутренние контакты обязаны появляться в результате — иначе
    /// группа сама себя «перестанет подпирать».</summary>
    [Test]
    public void GroupDrag_MatchesFullValidation_IncludingPairsInsideTheGroup()
    {
        var parts = Kitchen(12);
        parts.Add(Mover("MOVER_A", new Vector3(4000, 2000, 2500)));
        parts.Add(Mover("MOVER_B", new Vector3(4000, 2000, 2500)));
        parts.Add(Part("MOVER_C", new Vector3(4000, 2000, 2500), new Vector3(400, 18, 400)));
        int a = parts.Count - 3, b = parts.Count - 2, c = parts.Count - 1;

        var frozen = FrozenValidation.Freeze(parts, new[] { a, b, c });
        Assert.IsNotNull(frozen, "в сцене есть пол-якорь — заморозка обязана состояться");

        var live = new CoreValidationResult();
        foreach (var pose in GesturePoses())
        {
            parts[a] = Mover("MOVER_A", pose);
            parts[b] = Mover("MOVER_B", pose + new Vector3(0.4f, 0f, 0f));
            parts[c] = Part("MOVER_C", pose + new Vector3(0.2f, 0.13f, 0f),
                new Vector3(400, 18, 400));

            frozen!.Revalidate(parts, live);
            Assert.AreEqual(FullFingerprint(parts), Fingerprint(live, parts),
                $"групповая поза {pose}: инкрементальный проход разошёлся с полным");
        }
    }

    /// <summary>Свип обязан быть СПОСОБЕН заметить расхождение. Замороженный
    /// граф, посчитанный на одной сцене и применённый к другой (мувер стоит
    /// там же, а СОСЕДА подменили), даёт другой ответ — значит отпечаток
    /// сравнивает работу правил, а не сам себя.</summary>
    [Test]
    public void TheComparison_GoesRed_WhenTheFrozenGraphNoLongerDescribesTheScene()
    {
        var parts = Kitchen(4);
        parts.Add(Mover("MOVER", new Vector3(1200, 879, 0)));
        int moverIndex = parts.Count - 1;

        var frozen = FrozenValidation.Freeze(parts, new[] { moverIndex });
        Assert.IsNotNull(frozen, "в сцене есть пол-якорь — заморозка обязана состояться");

        var live = new CoreValidationResult();
        frozen!.Revalidate(parts, live);
        Assert.AreEqual(FullFingerprint(parts), Fingerprint(live, parts),
            "на неизменной сцене отпечатки обязаны совпасть");

        int stackIndex = parts.FindIndex(p => p.Name == "Hanging_Stack");
        parts[stackIndex] = Part("Hanging_Stack", new Vector3(8000, 1027, 3000),
            new Vector3(400, 18, 400));

        frozen.Revalidate(parts, live);
        Assert.AreNotEqual(FullFingerprint(parts), Fingerprint(live, parts),
            "статическая деталь уехала, а замороженный граф об этом не знает — " +
            "отпечатки ОБЯЗАНЫ разойтись, иначе сравнение ничего не проверяет");
    }

    /// <summary>Оговорка про якорь. <c>ValidationCore.CheckConnectivity</c> в
    /// ветке <c>!hasAnchor</c> берёт корнем обхода ПЕРВУЮ по списку деталь с
    /// контактом — единственное правило, зависящее от порядка списка, а не от
    /// пары и не от графа (закреплено в
    /// <c>ValidationLocalityTests.SceneWithoutAnchors_ChangesStatusOfPartsTheMoverNeverTouches</c>).
    /// Инкрементальный проход не пытается это воспроизвести: он ОТКАЗЫВАЕТСЯ
    /// замораживать сцену без якоря, и вызывающий честно платит полную
    /// валидацию. Отказ громкий — <c>null</c>, а не тихо неверный результат.</summary>
    [Test]
    public void SceneWithoutAnAnchor_RefusesToFreeze()
    {
        var grounded = new List<ValidationElement>
        {
            Part("A0", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
            Part("A1", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
            Part("B0", new Vector3(5000, 9, 0), new Vector3(800, 18, 400)),
            Part("B1", new Vector3(5000, 27, 0), new Vector3(800, 18, 400)),
        };

        Assert.IsNull(FrozenValidation.Freeze(grounded, new[] { 0 }),
            "без якоря корень обхода зависит от порядка списка — заморозка обязана отказать");
    }

    /// <summary>Та же сцена с якорем заморозку получает: отказ выше — про
    /// отсутствие якоря, а не про «заморозка вообще не работает».</summary>
    [Test]
    public void TheSameSceneWithAnAnchor_Freezes()
    {
        var grounded = new List<ValidationElement>
        {
            Part("Floor", new Vector3(2500, -9, 0), new Vector3(9000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part("A0", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
            Part("A1", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
        };

        Assert.IsNotNull(FrozenValidation.Freeze(grounded, new[] { 1 }),
            "якорь добавлен — отказ выше был именно про его отсутствие");
    }

    /// <summary>Цена кадра. Считается РАБОТА — число пар, отданных правилам, —
    /// потому что секундомер в тесте меряет ещё и джиттер планировщика. Время
    /// печатается рядом как справка.</summary>
    [Test]
    public void OnAFourHundredPartScene_TheFrameCostsTensOfPairsInsteadOfThousands()
    {
        var parts = Kitchen(80);
        parts.Add(Mover("MOVER", new Vector3(1200, 879, 0)));
        int moverIndex = parts.Count - 1;

        ValidationCore.TakePairsProcessed();
        var full = new CoreValidationResult();
        ValidationCore.Validate(parts, full);
        int fullPairs = ValidationCore.TakePairsProcessed();

        var frozen = FrozenValidation.Freeze(parts, new[] { moverIndex });
        Assert.IsNotNull(frozen, "в сцене есть пол-якорь — заморозка обязана состояться");

        var live = new CoreValidationResult();
        int incrementalPairs = 0;
        Vector3 worstPose = Vector3.zero;
        foreach (var pose in GesturePoses())
        {
            parts[moverIndex] = Mover("MOVER", pose);
            ValidationCore.TakePairsProcessed();
            frozen!.Revalidate(parts, live);
            int pairs = ValidationCore.TakePairsProcessed();
            if (pairs <= incrementalPairs) continue;
            incrementalPairs = pairs;
            worstPose = pose;
        }

        parts[moverIndex] = Mover("MOVER", worstPose);
        double fullMs = Milliseconds(() => ValidationCore.Validate(parts, full));
        double incrementalMs = Milliseconds(() => frozen!.Revalidate(parts, live));

        TestContext.WriteLine(
            $"деталей {parts.Count}: полная валидация {fullPairs} пар / {fullMs:F3} мс, " +
            $"худший кадр жеста (поза {worstPose}) {incrementalPairs} пар / " +
            $"{incrementalMs:F3} мс (в {(double)fullPairs / incrementalPairs:F0} раз меньше пар, " +
            $"в {fullMs / incrementalMs:F0} раз быстрее)");

        Assert.Greater(fullPairs, 4000, "полный проход на такой сцене обязан быть дорогим");
        Assert.Less(incrementalPairs, fullPairs / 50,
            $"кадр жеста обязан стоить десятки пар, а стоит {incrementalPairs}");
    }

    private static double Milliseconds(System.Action body)
    {
        for (int i = 0; i < 5; i++) body();

        var watch = Stopwatch.StartNew();
        const int runs = 20;
        for (int i = 0; i < runs; i++) body();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / runs;
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>Приборы к непроверенной идее из <c>docs/TODO.md</c> → P1 §1, хвост 3:
/// «для деталей, повёрнутых кратно 90°, контакт граней считается интервальной
/// арифметикой за O(1)».
///
/// Здесь не чинится ничего. Здесь отвечают числами на четыре вопроса: какова доля
/// осевых деталей в сцене пользователя, какова доля ПАР, у которых осевые обе,
/// совпадают ли множества контактов у общего перебора и у прототипа, и во что
/// обходится пара тем и другим путём.
///
/// Почему замер живёт тестом, а не скриптом: скрипт отвечает один раз и уезжает
/// вместе с сессией, а вопрос «а сейчас?» возникнет на каждой правке ядра и на
/// каждом новом файле пользователя. Дорогая часть — только замер времени, она
/// <c>[Explicit]</c>; доли и сравнение множеств стоят миллисекунды и остаются в
/// обычном прогоне, где и обязаны краснеть.</summary>
public class AxisAlignedContactFastPathTests
{
    private const string ReportFileName = "axis-aligned-contact-fast-path.md";

    /// <summary>Замер времени <c>[Explicit]</c> и потому идёт ОТДЕЛЬНЫМ прогоном.
    /// Пиши он в тот же файл — последний запуск затирал бы результат первого, и
    /// отчёт всегда состоял бы ровно из одной половины.</summary>
    private const string CostReportFileName = "axis-aligned-contact-cost.md";

    private static readonly List<string> CostReport = new List<string>();

    /// <summary>Осевых деталей в кухне «почти все» — утверждение из постановки.
    /// Ниже этой доли идея быстрого пути теряет смысл ещё до замера времени,
    /// поэтому порог стоит здесь, а не в отчёте.</summary>
    private const float ExpectedAxisAlignedShare = 0.9f;

    /// <summary>Ширина перекрытия, ниже которой «контакт» — не контакт, а знак нуля.
    /// Взят общий допуск ядра <c>Tolerance.EpsilonUnits</c> = 0,1 мм.
    ///
    /// Порог нужен потому, что общий перебор НЕ ИМЕЕТ epsilon на границе перекрытия:
    /// <c>FaceContacts.FacesOverlap</c> принимает любое строго положительное
    /// перекрытие, хоть 5e-7 м. Два способа посчитать один и тот же ноль расходятся
    /// в ЗНАКЕ — общий считает координаты прямоугольника скалярными произведениями
    /// (поворот на 90° из кватерниона даёт оси с ошибкой ~1e-7), интервальный берёт
    /// их из AABB. Это свойство ДОПУСКА, а не прототипа, и считается отдельной
    /// строкой, а не прячется в общий итог.
    ///
    /// Мерить порог площадью нельзя, и это стоило одного круга: перекрытие в
    /// полмикрона, умноженное на высоту стены 2,5 м, даёт 1,3 мм² — «приличную»
    /// площадь у контакта, которого нет.</summary>
    private const float NoiseOverlapUnits = Tolerance.EpsilonUnits;

    /// <summary>Микрон. Нужен, чтобы отделить «полоска ровно нулевой ширины» от
    /// «полоска узкая, но настоящая»: без такого деления фраза «ушёл только шум»
    /// доказывается собственным определением порога и не значит ничего.</summary>
    private const float MicronUnits = 1e-6f;

    private static readonly List<string> Report = new List<string>();

    private static List<ValidationElement>? _scene;

    private static List<ValidationElement> Scene =>
        _scene ??= SavedSceneBoxes.AsValidationElements(SavedSceneBoxes.OfTheUserScene());

    private static List<ValidationElement>? _fixtureScene;

    private static List<ValidationElement> FixtureScene =>
        _fixtureScene ??=
            SavedSceneBoxes.AsValidationElements(SavedSceneBoxes.OfTheValidationFixture());

    [OneTimeTearDown]
    public void WriteReports()
    {
        Write(Report, ReportFileName, "Осевой быстрый путь контакта граней — доли и множества");
        Write(CostReport, CostReportFileName, "Осевой быстрый путь контакта граней — цена пары");
    }

    private static void Write(List<string> lines, string fileName, string title)
    {
        if (lines.Count == 0) return;
        var text = new StringBuilder();
        text.AppendLine("# " + title);
        text.AppendLine();
        text.AppendLine("Источник сцены: `docs/example.save.json` (только чтение), "
            + "коробки развёрнуты тем же `ElementGeometry.Box`, что и в продакшене.");
        text.AppendLine();
        foreach (var line in lines) text.AppendLine(line);
        File.WriteAllText(Path.Combine(RepoPaths.Subdir("test-results"), fileName),
            text.ToString());
        lines.Clear();
    }

    [Test]
    public void ExampleScene_PartRotations_AreAxisAlignedAlmostEntirely()
    {
        var boxes = SavedSceneBoxes.OfTheUserScene();
        Assert.That(boxes.Count, Is.GreaterThan(300),
            "Сцена пользователя разобралась не целиком — дальше мерить нечего");

        var scene = SavedSceneBoxes.AsValidationElements(boxes);
        Boxes.Reset(scene.Count);
        var skewed = new List<string>();
        for (int i = 0; i < scene.Count; i++)
            if (!Boxes.Handles(i, scene[i].Geometry))
                skewed.Add(scene[i].Name);

        int aligned = boxes.Count - skewed.Count;
        float share = (float)aligned / boxes.Count;

        Report.Add($"## Детали");
        Report.Add($"- всего в файле: {boxes.Count}");
        Report.Add($"- повёрнуты кратно 90°: {aligned} ({Percent(share)})");
        Report.Add($"- повёрнуты произвольно: {skewed.Count}");
        if (skewed.Count > 0) Report.Add("- неосевые: " + string.Join(", ", skewed));
        Report.Add("");

        Assert.That(share, Is.GreaterThanOrEqualTo(ExpectedAxisAlignedShare),
            $"Осевых деталей {aligned} из {boxes.Count} — идея быстрого пути "
            + "опирается на «в кухне это почти все», и это больше не так");
    }

    [Test]
    public void ExampleScene_BroadPhasePairs_HaveAxisAlignedPartsOnBothSides()
    {
        var scene = Scene;
        var pairs = CandidatePairs(scene);
        var boxes = BoxesOf(scene);

        int bothAligned = 0;
        int aabbsApart = 0;
        foreach (var (lo, hi) in pairs)
        {
            if (boxes[lo] && boxes[hi]) bothAligned++;
            if (!FaceContacts.AABBsIntersect(scene[lo].Geometry, scene[hi].Geometry,
                    ValidationCore.ContactDistUnits))
                aabbsApart++;
        }

        float share = (float)bothAligned / pairs.Count;

        Report.Add("## Пары широкой фазы");
        Report.Add($"- пар после широкой фазы: {pairs.Count}");
        Report.Add($"- осевые обе детали: {bothAligned} ({Percent(share)})");
        Report.Add($"- коробки НЕ пересекаются (уходят в перебор 6×6): {aabbsApart}");
        Report.Add($"- сравнений граней на этих парах: {aabbsApart * 36}");
        Report.Add("- для сверки: снимок живой сцены даёт 9032 пары (`docs/TODO.md` → P1 §1),");
        Report.Add("  здесь меньше — у составных деталей одна коробка вместо нескольких тел;");
        Report.Add("  доли это не меняет, абсолютные миллисекунды пересчитываются по числу пар.");
        Report.Add("");

        Assert.That(share, Is.GreaterThanOrEqualTo(ExpectedAxisAlignedShare),
            $"Быстрый путь достанется только {bothAligned} парам из {pairs.Count} — "
            + "ради такой доли его писать незачем");
    }

    /// <summary>Цена порога 0,5 мм (<c>Tolerance.ContactUnits</c>) на живой сцене:
    /// что именно он выбрасывает. Считается разностью двух множеств интервального
    /// пути — с нулевым допуском на перекрытие и с рабочим.
    ///
    /// Несущая проверка тут одна и она не про число: НИ ОДНА деталь не должна
    /// потерять последнюю опору. Пропавший несущий контакт — это не «строка меньше
    /// в списке», это деталь, покрашенная как висящая в воздухе у пользователя,
    /// который ничего не трогал.
    ///
    /// Сцены ДВЕ, и вторая тут не для полноты. Живой файл пользователя отвечает за
    /// «как это выглядит у него», а замороженная фикстура — тот самый вход, на
    /// котором стоит baseline <c>ValidationInvariantTests</c>: объяснять сдвиг
    /// baseline числами с ДРУГОЙ сцены значит объяснять не то. Числа у них разные и
    /// обязаны быть разными — 411 деталей против 274.</summary>
    [TestCase(false, TestName = "ContactThreshold_OnTheUserScene_TakesNoPartsLastSupport")]
    [TestCase(true, TestName = "ContactThreshold_OnTheValidationFixture_TakesNoPartsLastSupport")]
    public void ContactThreshold_TakesNoPartsLastSupport(bool fixture)
    {
        var scene = fixture ? FixtureScene : Scene;
        var pairs = CandidatePairs(scene);
        var boxes = BoxesOf(scene);
        float contactDist = ValidationCore.ContactDistUnits;

        var loose = new List<CoreContact>();
        var strict = new List<CoreContact>();
        foreach (var (lo, hi) in pairs)
        {
            if (!boxes[lo] || !boxes[hi]) continue;
            Boxes.TryAppendContacts(lo, hi, scene[lo].Geometry,
                scene[hi].Geometry, contactDist, 0f, loose);
            Boxes.TryAppendContacts(lo, hi, scene[lo].Geometry,
                scene[hi].Geometry, contactDist, Tolerance.ContactUnits, strict);
        }

        var supportedLoose = SupportedParts(loose, scene.Count);
        var supportedStrict = SupportedParts(strict, scene.Count);
        var orphaned = new List<string>();
        for (int i = 0; i < scene.Count; i++)
            if (supportedLoose[i] > 0 && supportedStrict[i] == 0)
                orphaned.Add($"{scene[i].Name} (опор было {supportedLoose[i]}, стало 0)");

        int droppedSupporting = 0;
        foreach (var contact in loose) if (contact.IsFaceToFace) droppedSupporting++;
        foreach (var contact in strict) if (contact.IsFaceToFace) droppedSupporting--;

        var kept = new HashSet<(int, int, int, int)>();
        foreach (var contact in strict)
            kept.Add((contact.A, contact.B, contact.FaceA, contact.FaceB));

        int flat = 0, subTenth = 0, upToHalf = 0;
        float widest = 0f;
        foreach (var contact in loose)
        {
            if (kept.Contains((contact.A, contact.B, contact.FaceA, contact.FaceB))) continue;
            float width = Overlap(scene[contact.A].Geometry, scene[contact.B].Geometry, contact);
            widest = Mathf.Max(widest, width);
            if (width <= MicronUnits) flat++;
            else if (width < Tolerance.EpsilonUnits) subTenth++;
            else upToHalf++;
        }

        Report.Add(fixture
            ? "## Цена порога 0,5 мм на фикстуре ValidationInvariantTests"
            : "## Цена порога 0,5 мм на сцене пользователя");
        Report.Add($"- деталей в сцене: {scene.Count}");
        Report.Add($"- контактов без порога: {loose.Count}");
        Report.Add($"- контактов с порогом: {strict.Count}");
        Report.Add($"- выброшено: {loose.Count - strict.Count}, из них несущих: "
            + $"{droppedSupporting}");
        Report.Add($"  - из них полоска нулевой ширины (<= 1 мкм): {flat}");
        Report.Add($"  - полоска 1 мкм … 0,1 мм: {subTenth}");
        Report.Add($"  - полоска 0,1 … 0,5 мм: {upToHalf}");
        Report.Add($"  - самая широкая выброшенная полоска: {widest * 1000f:G4} мм");
        Report.Add($"- деталей, потерявших ПОСЛЕДНЮЮ опору: {orphaned.Count}");
        foreach (var line in First(orphaned, 10)) Report.Add("  - " + line);
        Report.Add("");

        Assert.That(orphaned, Is.Empty,
            "Порог перекрытия отобрал у детали последнюю опору — на живой сцене это "
            + "деталь, ставшая красной без единого действия пользователя:\n"
            + string.Join("\n", First(orphaned, 10)));

        Assert.That(droppedSupporting, Is.Zero,
            $"Порог выбросил {droppedSupporting} НЕСУЩИХ контактов. Последнюю опору пока "
            + "никто не потерял, но связность читает именно несущие: следующая правка "
            + "геометрии сделает из этого красную деталь, и искать будут не здесь");

        Assert.That(widest, Is.LessThan(Tolerance.ContactUnits),
            $"Самая широкая выброшенная полоска {widest * 1000f:G4} мм не уже порога "
            + "0,5 мм — значит фильтр отработал не по тому числу, по которому заявлен");
    }

    private static int[] SupportedParts(List<CoreContact> contacts, int partCount)
    {
        var supports = new int[partCount];
        foreach (var contact in contacts)
        {
            if (!contact.IsFaceToFace) continue;
            supports[contact.A]++;
            supports[contact.B]++;
        }
        return supports;
    }

    [Test]
    public void IntervalContacts_OnEveryScenePair_MatchTheGeneralFaceScan()
    {
        var scene = Scene;
        var pairs = CandidatePairs(scene);
        var boxes = BoxesOf(scene);
        float contactDist = ValidationCore.ContactDistUnits;

        var general = new List<CoreContact>();
        var interval = new List<CoreContact>();
        var missed = new List<(float overlap, bool supporting, string line)>();
        var extra = new List<(float overlap, bool supporting, string line)>();
        var flagged = new List<(float overlap, bool supporting, string line)>();
        int compared = 0;
        float worstAreaDelta = 0f;

        foreach (var (lo, hi) in pairs)
        {
            if (!boxes[lo] || !boxes[hi]) continue;
            compared++;

            general.Clear();
            interval.Clear();
            GeneralContacts(lo, hi, scene[lo].Faces, scene[hi].Faces, contactDist, general);
            Boxes.TryAppendContacts(lo, hi, scene[lo].Geometry,
                scene[hi].Geometry, contactDist, Tolerance.ContactUnits, interval);

            var byFaces = new Dictionary<int, CoreContact>(general.Count);
            foreach (var contact in general) byFaces[Key(contact)] = contact;

            var boxA = scene[lo].Geometry;
            var boxB = scene[hi].Geometry;

            foreach (var contact in interval)
            {
                if (!byFaces.TryGetValue(Key(contact), out var twin))
                {
                    extra.Add((Overlap(boxA, boxB, contact), contact.IsFaceToFace,
                        Describe("лишний у интервального", scene, contact, lo, hi)));
                    continue;
                }
                if (twin.IsFaceToFace != contact.IsFaceToFace)
                    flagged.Add((Overlap(boxA, boxB, contact), true, Describe(
                        $"признак опоры разошёлся (общий {twin.IsFaceToFace}, "
                        + $"интервальный {contact.IsFaceToFace})", scene, contact, lo, hi)));
                worstAreaDelta = Mathf.Max(worstAreaDelta, Mathf.Abs(twin.Area - contact.Area));
                byFaces.Remove(Key(contact));
            }

            foreach (var leftover in byFaces.Values)
                missed.Add((Overlap(boxA, boxB, leftover), leftover.IsFaceToFace,
                    Describe("потерян интервальным", scene, leftover, lo, hi)));
        }

        var real = Above(NoiseOverlapUnits, missed, extra, flagged);
        var noise = Below(NoiseOverlapUnits, missed, extra, flagged);
        var supporting = Supporting(missed, extra, flagged);

        Report.Add("## Сравнение множеств контактов");
        Report.Add($"- пар сравнено: {compared}");
        Report.Add($"- потеряно интервальным: {missed.Count}");
        Report.Add($"- лишних у интервального: {extra.Count}");
        Report.Add($"- разошёлся признак опоры: {flagged.Count}");
        Report.Add($"- худшее расхождение площади у общих контактов: {worstAreaDelta:G4} ед²");
        Report.Add($"- расхождений с перекрытием шире {NoiseOverlapUnits * 1000f:F1} мм: "
            + $"{real.Count}");
        Report.Add($"- расхождений в шумовой полосе (перекрытие уже порога): {noise.Count}");
        Report.Add($"- среди расхождений НЕСУЩИХ контактов (влияют на связность): "
            + $"{supporting.Count}");
        foreach (var line in First(real, 5)) Report.Add("  - " + line);
        foreach (var line in First(noise, 5)) Report.Add("  - шум: " + line);
        Report.Add("");

        Assert.That(supporting, Is.Empty,
            "Расхождение пришлось на НЕСУЩИЙ контакт: связность читает именно такие, "
            + "и цена этого — деталь, покрашенная как неподпёртая:\n"
            + string.Join("\n", First(supporting, 5)));

        Assert.That(real.Count, Is.Zero,
            "Прототип интервального пути разошёлся с общим перебором граней на контактах "
            + "с НЕнулевой шириной перекрытия — это дефект прототипа, а не допуска:\n"
            + string.Join("\n", First(real, 5)));
    }

    /// <summary>Цена пары. <c>[Explicit]</c>, потому что честный замер — это прогрев
    /// и сотни повторов по всей сцене, то есть секунды, а обычный прогон ядра стоит
    /// две. Запускать: <c>dotnet test --filter CostPerPair</c>.</summary>
    [Test, Explicit]
    public void IntervalContacts_CostPerPair_IsCheaperThanTheGeneralScan()
    {
        var scene = Scene;
        var pairs = CandidatePairs(scene);
        var boxes = BoxesOf(scene);
        float contactDist = ValidationCore.ContactDistUnits;

        var apart = new List<(int lo, int hi)>();
        foreach (var (lo, hi) in pairs)
        {
            if (!boxes[lo] || !boxes[hi]) continue;
            if (FaceContacts.AABBsIntersect(scene[lo].Geometry, scene[hi].Geometry, contactDist))
                continue;
            apart.Add((lo, hi));
        }
        Assert.That(apart.Count, Is.GreaterThan(100), "Мерить нечего: таких пар почти нет");

        const int WarmUpRounds = 5;
        const int MeasuredRounds = 50;
        var sink = new List<CoreContact>(64);

        void GeneralRound()
        {
            foreach (var (lo, hi) in apart)
            {
                sink.Clear();
                GeneralContacts(lo, hi, scene[lo].Faces, scene[hi].Faces, contactDist, sink);
            }
        }

        void IntervalRound()
        {
            foreach (var (lo, hi) in apart)
            {
                sink.Clear();
                Boxes.TryAppendContacts(lo, hi, scene[lo].Geometry,
                    scene[hi].Geometry, contactDist, Tolerance.ContactUnits, sink);
            }
        }

        BestOfInterleaved(WarmUpRounds, MeasuredRounds, apart.Count,
            GeneralRound, IntervalRound, out double generalUs, out double intervalUs);

        var validationResult = new CoreValidationResult();
        double validationUs = BestRound(WarmUpRounds, MeasuredRounds, 1, () =>
            ValidationCore.Validate(scene, validationResult));

        CostReport.Add("## Цена пары (лучший круг из " + MeasuredRounds + ", после прогрева)");
        CostReport.Add($"- полная валидация сцены целиком: {validationUs / 1000.0:F2} мс "
            + "(знаменатель, без которого доля ничего не значит)");
        CostReport.Add($"- пар в замере (коробки не пересекаются, обе осевые): {apart.Count}");
        CostReport.Add($"- общий перебор 6×6: {generalUs:F3} мкс/пара, "
            + $"{generalUs * apart.Count / 1000.0:F2} мс на сцену");
        CostReport.Add($"- интервальный путь: {intervalUs:F3} мкс/пара, "
            + $"{intervalUs * apart.Count / 1000.0:F2} мс на сцену");
        double savedMs = (generalUs - intervalUs) * apart.Count / 1000.0;
        CostReport.Add($"- выигрыш: {savedMs:F2} мс (в {generalUs / intervalUs:F1} раза)");
        CostReport.Add("- " + LastSpread);
        CostReport.Add($"- выигрыш от полной валидации: "
            + $"{Percent((float)(savedMs * 1000.0 / validationUs))}");
        CostReport.Add("");

        Assert.That(intervalUs, Is.LessThan(generalUs),
            $"Интервальный путь не дешевле общего ({intervalUs:F3} против {generalUs:F3} мкс) — "
            + "идея быстрого пути не окупается, и это результат замера, а не сбой");
    }

    /// <summary>Тело <c>ValidationCore.AddFaceContacts</c>, которое там приватно.
    /// Копия строчка в строчку: любое расхождение параметров скана сделало бы
    /// сравнение множеств проверкой копии, а не продакшена.</summary>
    private static void GeneralContacts(int aIdx, int bIdx, Face[] facesA, Face[] facesB,
        float contactDist, List<CoreContact> into)
    {
        foreach (var hit in new FaceContactScan(facesA, facesB, FaceAlignment.ParallelEitherWay,
                     FaceContactScan.NoLowerGapBound, contactDist, 0f))
        {
            into.Add(new CoreContact(aIdx, bIdx, hit.IndexA, hit.IndexB,
                hit.OverlapArea, hit.OverlapRatio >= Tolerance.MinSupportOverlap));
        }
    }

    /// <summary>Два пути меряются ЧЕРЕДУЯСЬ, круг за кругом, и каждому берётся его
    /// лучший круг. Машина общая: на ней в это же время идут прогоны других агентов,
    /// и «сначала пятьдесят кругов одного, потом пятьдесят кругов другого» ловит
    /// чужую нагрузку целиком в одно из двух измерений. Два прогона подряд разошлись
    /// так вдвое — 0,248 и 0,523 мкс на одном и том же коде.</summary>
    private static void BestOfInterleaved(int warmUp, int rounds, int pairs,
        System.Action first, System.Action second, out double firstUs, out double secondUs)
    {
        for (int i = 0; i < warmUp; i++) { first(); second(); }

        var firstRounds = new List<double>(rounds);
        var secondRounds = new List<double>(rounds);
        var watch = new Stopwatch();
        for (int i = 0; i < rounds; i++)
        {
            firstRounds.Add(Round(watch, first, pairs));
            secondRounds.Add(Round(watch, second, pairs));
        }

        firstRounds.Sort();
        secondRounds.Sort();
        firstUs = firstRounds[0];
        secondUs = secondRounds[0];
        LastSpread = $"разброс кругов: первый путь {firstRounds[0]:F3}…"
            + $"{firstRounds[rounds / 2]:F3}…{firstRounds[rounds - 1]:F3}, "
            + $"второй {secondRounds[0]:F3}…{secondRounds[rounds / 2]:F3}…"
            + $"{secondRounds[rounds - 1]:F3} мкс (мин…медиана…макс)";
    }

    private static string LastSpread = "";

    private static double Round(Stopwatch watch, System.Action body, int pairs)
    {
        watch.Restart();
        body();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds * 1000.0 / pairs;
    }

    private static double BestRound(int warmUp, int rounds, int pairs, System.Action body)
    {
        for (int i = 0; i < warmUp; i++) body();

        double best = double.MaxValue;
        var watch = new Stopwatch();
        for (int i = 0; i < rounds; i++)
        {
            watch.Restart();
            body();
            watch.Stop();
            double us = watch.Elapsed.TotalMilliseconds * 1000.0 / pairs;
            if (us < best) best = us;
        }
        return best;
    }

    private static List<(int lo, int hi)> CandidatePairs(IReadOnlyList<ValidationElement> scene)
    {
        ValidationBroadPhase.Clear();
        var pairs = new List<(int lo, int hi)>(
            ValidationBroadPhase.CandidatePairsInNestedLoopOrder(
                scene, ValidationCore.ContactDistUnits));
        ValidationBroadPhase.Clear();
        return pairs;
    }

    private static readonly AxisAlignedBoxIndex Boxes = new AxisAlignedBoxIndex();

    private static List<bool> BoxesOf(IReadOnlyList<ValidationElement> scene)
    {
        Boxes.Reset(scene.Count);
        var handled = new List<bool>(scene.Count);
        for (int i = 0; i < scene.Count; i++)
            handled.Add(Boxes.Handles(i, scene[i].Geometry));
        return handled;
    }

    private static int Key(in CoreContact contact) => contact.FaceA * Face.BoxFaceCount
        + contact.FaceB;

    private static string Describe(string what, IReadOnlyList<ValidationElement> scene,
        in CoreContact contact, int lo, int hi) =>
        $"{what}: {scene[lo].Name}[грань {contact.FaceA}] — {scene[hi].Name}"
        + $"[грань {contact.FaceB}], площадь {contact.Area:G4} ед², "
        + $"опора {contact.IsFaceToFace}";

    private static IEnumerable<string> First(List<string> lines, int count)
    {
        for (int i = 0; i < lines.Count && i < count; i++) yield return lines[i];
    }

    /// <summary>Меньшее из двух перекрытий сечения — размер, по которому пара стоит
    /// на границе «перекрытие есть / перекрытия нет». Им расхождение двух путей
    /// отделяется от настоящего; площадь для этого не годится, потому что
    /// перекрытие в полмикрона, умноженное на высоту стены 2,5 м, даёт 1,3 мм².</summary>
    private static float Overlap(in ElementGeometry a, in ElementGeometry b,
        in CoreContact contact)
    {
        int axis = DominantAxis(a.Faces[contact.FaceA].normal);
        int u = axis == 2 ? 0 : axis + 1;
        int v = axis == 0 ? 2 : axis - 1;
        return Mathf.Min(
            Mathf.Min(a.Max[u], b.Max[u]) - Mathf.Max(a.Min[u], b.Min[u]),
            Mathf.Min(a.Max[v], b.Max[v]) - Mathf.Max(a.Min[v], b.Min[v]));
    }

    private static int DominantAxis(Vector3 normal)
    {
        float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }

    private static List<string> Above(float overlap,
        params List<(float overlap, bool supporting, string line)>[] groups) => Split(overlap, true, groups);

    private static List<string> Below(float overlap,
        params List<(float overlap, bool supporting, string line)>[] groups) => Split(overlap, false, groups);

    /// <summary>Расхождения, где потерянный или лишний контакт НЕСУЩИЙ. Именно их
    /// цена — не «лишняя строка в списке», а другой ответ валидации: связность
    /// (<c>ValidationCore.CheckConnectivity</c>) читает только контакты с
    /// <c>IsFaceToFace</c>, и пропажа одного такого красит деталь как
    /// неподпёртую.</summary>
    private static List<string> Supporting(
        params List<(float overlap, bool supporting, string line)>[] groups)
    {
        var picked = new List<string>();
        foreach (var group in groups)
            foreach (var entry in group)
                if (entry.supporting)
                    picked.Add(entry.line);
        return picked;
    }

    private static List<string> Split(float threshold, bool above,
        List<(float overlap, bool supporting, string line)>[] groups)
    {
        var picked = new List<string>();
        foreach (var group in groups)
            foreach (var entry in group)
                if (entry.overlap >= threshold == above)
                    picked.Add($"{entry.line}, перекрытие {entry.overlap * 1000f:G4} мм");
        return picked;
    }

    private static string Percent(float share) =>
        (share * 100f).ToString("F1", CultureInfo.InvariantCulture) + " %";
}

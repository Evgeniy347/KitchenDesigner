using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using static ValidationTestScene;

/// <summary>Инкрементальная валидация на время жеста: сцена замораживается БЕЗ
/// движущихся деталей один раз, дальше каждый кадр досчитываются только пары
/// «мувер против статического индекса» и пересчитывается связность.
///
/// Ворота этой работы — <see cref="ValidationLocalityTests"/>: там доказано,
/// что правила <see cref="ValidationCore"/> зависят только от фактов пары и от
/// графа контактов. Здесь доказывается второе, без чего первое бесполезно:
/// результат инкрементального прохода СОВПАДАЕТ с полной валидацией. Не «почти»
/// — пользователь принимает решения по подсветке, и «почти правильно» здесь
/// хуже, чем медленно.</summary>
public class IncrementalValidationTests
{
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
            Assert.AreEqual(FullFingerprint(parts), Fingerprint(live, parts),
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
    /// группа перестанет подпирать сама себя и покраснеет на ровном месте.</summary>
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
    /// ветке <c>!HasAnchor</c> берёт корнем обхода ПЕРВУЮ по списку деталь с
    /// контактом — единственное правило, зависящее от порядка списка, а не от
    /// пары и не от графа (закреплено в
    /// <c>ValidationLocalityTests.SceneWithoutAnchors_ChangesStatusOfPartsTheMoverNeverTouches</c>).
    /// Инкрементальный проход не пытается это воспроизвести: он ОТКАЗЫВАЕТСЯ
    /// замораживать сцену без якоря, и вызывающий честно платит полную
    /// валидацию. Отказ громкий — <c>null</c>, а не тихо неверный результат.
    /// Вопрос «есть ли якорь» при этом задаётся одной функцией, той же, что
    /// выбирает корень обхода, — иначе два ответа разъехались бы.</summary>
    [Test]
    public void SceneWithoutAnAnchor_RefusesToFreeze()
    {
        Assert.IsNull(FrozenValidation.Freeze(Anchorless(), new[] { 0 }),
            "без якоря корень обхода зависит от порядка списка — заморозка обязана отказать");
    }

    /// <summary>Та же сцена с якорем заморозку получает: отказ выше — про
    /// отсутствие якоря, а не про «заморозка вообще не работает».</summary>
    [Test]
    public void TheSameSceneWithAnAnchor_Freezes()
    {
        var grounded = Anchorless();
        grounded.Insert(0, Part("Floor", new Vector3(2500, -9, 0), new Vector3(9000, 18, 3000),
            ElementKind.Anchor | ElementKind.FloorAnchor));

        Assert.IsNotNull(FrozenValidation.Freeze(grounded, new[] { 1 }),
            "якорь добавлен — отказ выше был именно про его отсутствие");
    }

    private static List<ValidationElement> Anchorless() => new List<ValidationElement>
    {
        Part("A0", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
        Part("A1", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
        Part("B0", new Vector3(5000, 9, 0), new Vector3(800, 18, 400)),
        Part("B1", new Vector3(5000, 27, 0), new Vector3(800, 18, 400)),
    };

    /// <summary>Цена кадра. Считается РАБОТА — число пар, отданных правилам, —
    /// потому что секундомер в тесте меряет ещё и джиттер планировщика. Время
    /// печатается рядом как справка.</summary>
    [Test]
    public void OnAFourHundredPartScene_TheFrameCostsAHandfulOfPairsInsteadOfThousands()
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

        Assert.Greater(fullPairs, 4000,
            "полный проход на такой сцене обязан быть дорогим — иначе мерить нечего");
        Assert.Less(incrementalPairs, fullPairs / 50,
            $"кадр жеста обязан стоить единицы пар, а стоит {incrementalPairs}");
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

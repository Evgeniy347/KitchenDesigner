using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using static ValidationTestScene;

/// <summary>Неявная земля: плоскость, которая подпирает детали с низом на её уровне и не
/// входит в список элементов. Вместо плиты-якоря (<c>BasePlate</c>) в сцене без пола и стен.
/// Выключена по умолчанию — тогда действуют прежние правила сцены без якоря
/// (<see cref="ConnectivityWithoutAnchorTests"/>); включает её приложение.</summary>
public class ImpliedGroundTests
{
    private static readonly ImpliedGround Ground = ImpliedGround.At(0f);

    private static ValidationElement Board(string name, float x, float bottomMm) =>
        Part(name, new Vector3(x, bottomMm + 9, 0), new Vector3(800, 18, 400));

    private static HashSet<string> UnsupportedNames(List<ValidationElement> parts, ImpliedGround ground) =>
        new(ValidationCore.Validate(parts, ground).Diagnostics?
            .Where(d => d.Kind == ViolationKind.Unsupported)
            .Select(d => parts[d.Element].Name) ?? Enumerable.Empty<string>());

    [Test]
    public void LoneBoardOnTheGround_IsSupported_WithGround_AndUnsupported_Without()
    {
        var parts = new List<ValidationElement> { Board("Lone", 0, 0) };

        Assert.IsTrue(ValidationCore.Validate(parts, Ground).IsValid,
            "деталь стоит на земле — подпёрта");
        Assert.IsFalse(ValidationCore.Validate(parts).IsValid,
            "без земли одинокая деталь без контактов неподпёрта: флаг и есть разница");
    }

    [Test]
    public void BoardInTheAir_IsStillUnsupported_EvenWithGround()
    {
        var parts = new List<ValidationElement> { Board("Floating", 0, 1000) };

        CollectionAssert.AreEquivalent(new[] { "Floating" }, UnsupportedNames(parts, Ground),
            "висящая над землёй деталь землёй не подпирается: флаг не делает сцену валидной целиком");
    }

    [Test]
    public void StackOnAGroundedBoard_IsSupportedTransitively_AndASeparateFloatingPairIsNot()
    {
        var parts = new List<ValidationElement>
        {
            Board("Base", 0, 0),
            Board("OnBase", 0, 18),
            Board("HighA", 3000, 1000),
            Board("HighB", 3000, 1018),
        };

        CollectionAssert.AreEquivalent(new[] { "HighA", "HighB" }, UnsupportedNames(parts, Ground),
            "стопка на подпёртой доске держится транзитивно, а пара над землёй висит вся");
    }

    [Test]
    public void BoardSunkBelowTheGround_IsNotHeldByIt()
    {
        var parts = new List<ValidationElement> { Board("Sunk", 0, -5) };

        CollectionAssert.AreEquivalent(new[] { "Sunk" }, UnsupportedNames(parts, Ground),
            "утопленная глубже контактного допуска деталь землёй не подпирается: это нарушение, как с плитой");
    }

    [Test]
    public void Ground_IsAPlaneTest_NotABox_SoAPartFarAwayStandsOnItToo()
    {
        var parts = new List<ValidationElement>
        {
            Board("Near", 0, 0),
            Board("TenKilometresAway", 10_000_000, 0),
        };

        var result = ValidationCore.Validate(parts, Ground);

        Assert.IsTrue(result.IsValid, "земля — проверка высоты, а не коробка конечного размера");
        Assert.AreEqual(0, result.Contacts.Count, "земля не порождает контактов с элементами");
        Assert.IsTrue(result.Violations.All(i => i < parts.Count), "индексы остаются индексами деталей");
    }

    [Test]
    public void Ground_AtAnotherLevel_HoldsOnlyWhatStandsAtThatLevel()
    {
        var raised = ImpliedGround.At(3f);
        var parts = new List<ValidationElement>
        {
            Board("AtZero", 0, 0),
            Board("AtThreeMetres", 3000, 3000),
        };

        CollectionAssert.AreEquivalent(new[] { "AtZero" }, UnsupportedNames(parts, raised),
            "земля на трёх метрах держит деталь на трёх метрах, а деталь на нуле остаётся в воздухе");
    }

    [Test]
    public void GroundDoesNotChange_TheVerdictOfASceneThatAlreadyHasAFloorAnchor()
    {
        var withFloor = new List<ValidationElement>
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Board("OnFloor", 0, 0),
            Board("Hanging", 0, 1000),
        };

        CollectionAssert.AreEquivalent(new[] { "Hanging" }, UnsupportedNames(withFloor, ImpliedGround.None),
            "контроль: без земли висит только висящая");
        CollectionAssert.AreEquivalent(new[] { "Hanging" }, UnsupportedNames(withFloor, Ground),
            "с землёй вердикт сцены, где пол-якорь уже есть, не меняется");
    }

    [Test]
    public void Presence_AnswersHasAnchor_SoTheGestureMayFreeze()
    {
        var parts = new List<ValidationElement> { Board("Lone", 0, 0) };

        Assert.IsFalse(ValidationCore.HasAnchor(parts), "контроль: без земли якоря в сцене нет");
        Assert.IsTrue(ValidationCore.HasAnchor(parts, Ground), "земля отвечает на вопрос «есть ли якорь»");
        Assert.IsNull(FrozenValidation.Freeze(parts, new[] { 0 }), "без якоря заморозка отказывает");
        Assert.IsNotNull(FrozenValidation.Freeze(parts, new[] { 0 }, Ground), "с землёй заморозка состоится");
    }

    [Test]
    public void FrozenPass_OnGround_MatchesFullValidation_AcrossTheWholeGesture()
    {
        var parts = Kitchen(12);
        parts.RemoveAt(0);
        parts.Add(Mover("MOVER", new Vector3(4000, 2000, 2500)));
        int moverIndex = parts.Count - 1;

        var frozen = FrozenValidation.Freeze(parts, new[] { moverIndex }, Ground);
        Assert.IsNotNull(frozen, "земля играет роль якоря — заморозка обязана состояться");

        var live = new CoreValidationResult();
        foreach (var pose in GesturePoses())
        {
            parts[moverIndex] = Mover("MOVER", pose);

            frozen!.Revalidate(parts, live);
            Assert.AreEqual(Fingerprint(ValidationCore.Validate(parts, Ground), parts),
                Fingerprint(live, parts),
                $"поза {pose}: инкрементальный проход с землёй разошёлся с полной валидацией");
        }
    }

    [Test]
    public void GestureValidation_RefreezesWhenTheGroundChanges()
    {
        var parts = new List<ValidationElement> { Board("Lone", 0, 0) };
        var gesture = new GestureValidation();
        var result = new CoreValidationResult();

        Assert.IsFalse(gesture.TryValidate(parts, result), "без земли и якоря заморозка отказывает");

        gesture.Reset();
        Assert.IsTrue(gesture.TryValidate(parts, result, Ground), "с землёй заморозка берётся за кадр");
        Assert.IsTrue(result.IsValid, "с землёй деталь на ней подпёрта и в заморозке");

        Assert.IsFalse(gesture.TryValidate(parts, result),
            "земля убрана посреди жеста — старая заморозка не должна отвечать за сцену без неё");
    }

    [Test]
    public void ValidationReuse_DoesNotServeAnAnswerCountedUnderAnotherGround()
    {
        var parts = new List<ValidationElement> { Board("Lone", 0, 0) };
        var reuse = new ValidationReuse();

        Assert.IsTrue(reuse.NeedsAFreshAnswer(parts, ImpliedGround.None), "первый вопрос всегда требует свежего ответа");
        Assert.IsFalse(reuse.NeedsAFreshAnswer(parts, ImpliedGround.None), "та же сцена и земля — ответ стоит");
        Assert.IsTrue(reuse.NeedsAFreshAnswer(parts, Ground), "земля появилась — ответ устарел");
        Assert.IsFalse(reuse.NeedsAFreshAnswer(parts, Ground), "та же сцена и та же земля — ответ стоит");
        Assert.IsTrue(reuse.NeedsAFreshAnswer(parts, ImpliedGround.At(1f)), "земля поднялась — ответ устарел");
    }

    [Test]
    public void ImpliedGround_EqualityFollowsPresenceAndLevel()
    {
        Assert.AreEqual(ImpliedGround.None, default(ImpliedGround), "None — это default");
        Assert.AreEqual(ImpliedGround.At(0f), ImpliedGround.At(0f), "один уровень — равные земли");
        Assert.AreNotEqual(ImpliedGround.None, ImpliedGround.At(0f), "земля на нуле — не отсутствие земли");
        Assert.AreNotEqual(ImpliedGround.At(0f), ImpliedGround.At(1f), "разные уровни — разные земли");
        Assert.IsTrue(ImpliedGround.At(0f) == ImpliedGround.At(0f), "оператор равенства");
        Assert.IsTrue(ImpliedGround.At(0f) != ImpliedGround.None, "оператор неравенства");
        Assert.AreEqual(ImpliedGround.At(2f).GetHashCode(), ImpliedGround.At(2f).GetHashCode(),
            "равные земли дают равный хэш");
    }
}

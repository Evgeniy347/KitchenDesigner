using NUnit.Framework;

/// <summary>Юнит-покрытие review-perf-tests-tooling.md #2: SceneLeakGuard.IsKnown раньше
/// проверял только имя теста, поэтому запись одной известной утечки (например,
/// __TextureOverlays у ContextMenuLayoutTests) прощала ЛЮБУЮ другую утечку того же
/// теста — ровно то, что спрятало бы, например, ScrewLeg с MeshCollider, инцидент, ради
/// которого сторож построен.</summary>
public class SceneLeakGuardIsKnownTests
{
    private const string ContextMenuLayoutTest =
        "ContextMenuLayoutTests.Wall_AddRowHover_ShowsFutureOverlay_AndRemovesItOnExit";

    [Test]
    public void IsKnown_KnownTestAndKnownObject_IsTrue()
    {
        Assert.IsTrue(SceneLeakGuard.IsKnown(ContextMenuLayoutTest, "__TextureOverlays"));
    }

    [Test]
    public void IsKnown_KnownTestButDifferentObject_IsFalse()
    {
        Assert.IsFalse(SceneLeakGuard.IsKnown(ContextMenuLayoutTest, "ScrewLeg"),
            "запись для __TextureOverlays не должна прощать ЛЮБУЮ утечку того же теста — "
            + "иначе спрятан именно тот инцидент (ScrewLeg с MeshCollider), ради которого "
            + "сторож построен");
    }

    [Test]
    public void IsKnown_PrefixPattern_MatchesOnlyThatPrefix()
    {
        Assert.IsTrue(SceneLeakGuard.IsKnown(
            "PipePanelChoiceUndoGuardTests.EveryChoiceRowOfThePropertiesPanel_IsUndoableInOneStep", "P_DoorElement"));
        Assert.IsFalse(SceneLeakGuard.IsKnown(
            "PipePanelChoiceUndoGuardTests.EveryChoiceRowOfThePropertiesPanel_IsUndoableInOneStep", "SomethingElse"));
    }

    [Test]
    public void IsKnown_KnownWholeSceneLeakerTest_AcceptsAnyObjectName()
    {
        Assert.IsTrue(SceneLeakGuard.IsKnown(
            "ValidationInvariantTests.Validation_ExampleSave_IsDeterministic", "BasePlate"));
        Assert.IsTrue(SceneLeakGuard.IsKnown(
            "ValidationInvariantTests.Validation_ExampleSave_IsDeterministic", "Vintovaya_opora_7"));
    }

    [Test]
    public void IsKnown_UnknownTest_IsFalse()
    {
        Assert.IsFalse(SceneLeakGuard.IsKnown("SomeBrandNewTests.SomeMethod", "AnyObject"));
    }
}

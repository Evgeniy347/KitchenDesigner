using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class InputMapMuteTests
{
    private GameObject? _ownerGo;

    [TearDown]
    public void TearDown()
    {
        InputMap.ReleaseAnyMuteForTests();
        if (_ownerGo != null) UnityEngine.Object.DestroyImmediate(_ownerGo);
        _ownerGo = null;
    }

    private GameObject NewOwner(string name)
    {
        _ownerGo = new GameObject(name);
        return _ownerGo;
    }

    [Test]
    public void MuteSceneInput_MakesSceneInputMutedTrue_SoDownHeldUpAllShortCircuit()
    {
        var owner = NewOwner("Owner");

        Assert.IsFalse(InputMap.SceneInputMuted, "предпосылка: до Mute сцена не глушится");

        InputMap.MuteSceneInput(owner);

        Assert.IsTrue(InputMap.SceneInputMuted,
            "Down/Held/Up все определены как `!SceneInputMuted && ...` - как только этот флаг "
            + "истинен, ни один из них не дойдёт до ActionFiring вообще, поэтому доказательство "
            + "срабатывания самого сопоставителя аккордов живёт отдельно, в быстром наборе "
            + "(ActionFiringTests), а здесь проверяется только то, что этот флаг реально встаёт "
            + "и реально снимается");
    }

    [Test]
    public void UnmuteSceneInput_ByTheOwner_RestoresHotkeys()
    {
        var owner = NewOwner("Owner");
        InputMap.MuteSceneInput(owner);

        InputMap.UnmuteSceneInput(owner);

        Assert.IsFalse(InputMap.SceneInputMuted);
    }

    [Test]
    public void UnmuteSceneInput_ByAForeignCaller_DoesNothing()
    {
        var owner = NewOwner("Owner");
        var stranger = new GameObject("Stranger");
        try
        {
            InputMap.MuteSceneInput(owner);

            InputMap.UnmuteSceneInput(stranger);

            Assert.IsTrue(InputMap.SceneInputMuted,
                "снять глушение обязан тот, кто его включил - посторонний вызов не в счёт");
            Assert.IsTrue(InputMap.IsMutedBy(owner));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stranger);
        }
    }

    [Test]
    public void MuteSceneInput_BySecondOwner_WhileAlreadyMuted_KeepsTheFirstOwner()
    {
        var first = NewOwner("First");
        var second = new GameObject("Second");
        try
        {
            InputMap.MuteSceneInput(first);

            LogAssert.Expect(LogType.Error, new Regex(@"\[InputMap\]"));
            InputMap.MuteSceneInput(second);

            Assert.IsTrue(InputMap.IsMutedBy(first),
                "второй захват поверх первого не должен молча украсть владение");
            Assert.IsFalse(InputMap.IsMutedBy(second));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void MuteSceneInput_CalledAgainByTheSameOwner_DoesNotLoseOwnership()
    {
        var owner = NewOwner("Owner");
        InputMap.MuteSceneInput(owner);

        InputMap.MuteSceneInput(owner);

        Assert.IsTrue(InputMap.IsMutedBy(owner));
        InputMap.UnmuteSceneInput(owner);
        Assert.IsFalse(InputMap.SceneInputMuted);
    }

    [Test]
    public void MuteSceneInput_NullOwner_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => InputMap.MuteSceneInput(null!));
    }

    [Test]
    public void OwnerDestroyed_WithoutUnmuting_SelfHeals()
    {
        var owner = NewOwner("DyingOwner");
        InputMap.MuteSceneInput(owner);
        Assert.IsTrue(InputMap.SceneInputMuted);

        UnityEngine.Object.DestroyImmediate(owner);
        _ownerGo = null;

        Assert.IsFalse(InputMap.SceneInputMuted,
            "владелец умер, не сняв глушение сам - приложение не должно остаться с мёртвой "
            + "клавиатурой сцены навсегда");
    }
}

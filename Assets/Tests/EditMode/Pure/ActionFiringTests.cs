using System;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

/// <summary>
/// The fast door for "does a keypress reach an action": ActionFiring.Down/Held/Up are exactly
/// what InputMap.Down/Held/Up call (InputMap only adds the SceneInputMuted short-circuit and a
/// real UnityEngine.Input-backed IKeyState), so a test here IS a test of the production route,
/// not a bypass of it - and because it needs nothing but KeyBindings (Pure) and a fake IKeyState
/// (Pure), it runs in the dotnet suite instead of EditMode/PlayMode.
///
/// Rule under test: a chord's own modifiers are always required (RequiredModifiersOnly's
/// subset check). Beyond that, an EXTRA held modifier blocks the action only if the resulting
/// key+modifier combination is the chord of a DIFFERENT action - so Shift+W still moves the
/// camera forward (nobody binds Shift+W), but Ctrl+S does not move it backward (Ctrl+S is
/// SaveProject). The table below is hand-verified against KeyBindingDefaults, not derived by
/// the test re-running the same search the production code does - re-deriving it would just be
/// ActionFiring testing itself.
/// </summary>
public class ActionFiringTests
{
    public readonly struct Case
    {
        public readonly InputAction Action;
        public readonly Action<FakeKeyState> PressTrigger;
        public readonly KeyCode? OwnModifier;
        public readonly KeyCode FreeExtraModifier;
        public readonly KeyCode? ClaimedExtraModifier;
        public readonly InputAction? ClaimedBy;

        public Case(
            InputAction action, Action<FakeKeyState> pressTrigger, KeyCode? ownModifier, KeyCode freeExtraModifier,
            KeyCode? claimedExtraModifier = null, InputAction? claimedBy = null)
        {
            Action = action;
            PressTrigger = pressTrigger;
            OwnModifier = ownModifier;
            FreeExtraModifier = freeExtraModifier;
            ClaimedExtraModifier = claimedExtraModifier;
            ClaimedBy = claimedBy;
        }

        public override string ToString() => Action.ToString();
    }

    private static Action<FakeKeyState> Key(KeyCode key) => s => s.Press(key);
    private static Action<FakeKeyState> Button(MouseButtonKind button) => s => s.PressButton(button);
    private static readonly Action<FakeKeyState> WheelTrigger = s => s.MoveWheel();

    private static readonly Case[] Cases =
    {
        new Case(InputAction.CameraMoveForward, Key(KeyCode.W), null, KeyCode.LeftShift),
        new Case(InputAction.CameraMoveBack, Key(KeyCode.S), null, KeyCode.LeftShift,
            KeyCode.LeftControl, InputAction.SaveProject),
        new Case(InputAction.CameraMoveLeft, Key(KeyCode.A), null, KeyCode.LeftShift,
            KeyCode.LeftControl, InputAction.ErrorPanelSelectAll),
        new Case(InputAction.CameraMoveRight, Key(KeyCode.D), null, KeyCode.LeftShift,
            KeyCode.LeftControl, InputAction.DuplicateSelected),
        new Case(InputAction.CameraRotateLeft, Key(KeyCode.LeftArrow), null, KeyCode.LeftShift),
        new Case(InputAction.CameraRotateRight, Key(KeyCode.RightArrow), null, KeyCode.LeftShift),
        new Case(InputAction.CameraRotateUp, Key(KeyCode.UpArrow), null, KeyCode.LeftShift),
        new Case(InputAction.CameraRotateDown, Key(KeyCode.DownArrow), null, KeyCode.LeftShift),
        new Case(InputAction.CameraZoomIn, Key(KeyCode.Equals), null, KeyCode.LeftShift),
        new Case(InputAction.CameraZoomOut, Key(KeyCode.Minus), null, KeyCode.LeftShift),
        new Case(InputAction.CameraFocusSelection, Key(KeyCode.F), null, KeyCode.LeftControl),
        new Case(InputAction.DeleteSelected, Key(KeyCode.Delete), null, KeyCode.LeftControl),
        new Case(InputAction.ActivateSelected, Key(KeyCode.E), null, KeyCode.LeftControl),
        new Case(InputAction.CatalogOpenSearch, Key(KeyCode.Slash), null, KeyCode.LeftControl),
        new Case(InputAction.ViewTop, Key(KeyCode.Alpha1), null, KeyCode.LeftControl),
        new Case(InputAction.ViewSide, Key(KeyCode.Alpha2), null, KeyCode.LeftControl),
        new Case(InputAction.ViewFront, Key(KeyCode.Alpha3), null, KeyCode.LeftControl),
        new Case(InputAction.ToggleHelp, Key(KeyCode.F1), null, KeyCode.LeftControl),
        new Case(InputAction.TogglePhotoMode, Key(KeyCode.F10), null, KeyCode.LeftControl),
        new Case(InputAction.ToggleDevConsole, Key(KeyCode.BackQuote), null, KeyCode.LeftControl),
        new Case(InputAction.SaveProject, Key(KeyCode.S), KeyCode.LeftControl, KeyCode.LeftAlt),
        new Case(InputAction.DuplicateSelected, Key(KeyCode.D), KeyCode.LeftControl, KeyCode.LeftAlt),
        new Case(InputAction.ErrorPanelCopy, Key(KeyCode.C), KeyCode.LeftControl, KeyCode.LeftAlt),
        new Case(InputAction.ErrorPanelSelectAll, Key(KeyCode.A), KeyCode.LeftControl, KeyCode.LeftAlt),
        new Case(InputAction.Redo, Key(KeyCode.Y), KeyCode.LeftControl, KeyCode.LeftAlt),
        new Case(InputAction.Undo, Key(KeyCode.Z), KeyCode.LeftControl, KeyCode.LeftAlt,
            KeyCode.LeftShift, InputAction.Redo),
        new Case(InputAction.PerfMonitorToggle, Key(KeyCode.F9), null, KeyCode.LeftControl,
            KeyCode.LeftShift, InputAction.PerfMonitorToggleRecording),
        new Case(InputAction.PerfMonitorToggleRecording, Key(KeyCode.F9), KeyCode.LeftShift, KeyCode.LeftControl),

        new Case(InputAction.CameraOrbit, Button(MouseButtonKind.Right), null, KeyCode.LeftControl),
        new Case(InputAction.CameraPan, Button(MouseButtonKind.Middle), null, KeyCode.LeftControl),
        new Case(InputAction.CameraZoomWheel, WheelTrigger, null, KeyCode.LeftControl),
        new Case(InputAction.SelectClick, Button(MouseButtonKind.Left), null, KeyCode.LeftShift,
            KeyCode.LeftControl, InputAction.SelectMultiClick),
        new Case(InputAction.SelectMultiClick, Button(MouseButtonKind.Left), KeyCode.LeftControl, KeyCode.LeftAlt),
    };

    private static KeyBindings DefaultBindings() => new KeyBindings();

    [TestCaseSource(nameof(Cases))]
    public void ActionFiring_Down_FiresWithItsOwnChord(Case c)
    {
        var state = new FakeKeyState();
        c.PressTrigger(state);
        if (c.OwnModifier != null) state.Hold(c.OwnModifier.Value);

        Assert.IsTrue(ActionFiring.Down(DefaultBindings(), state, c.Action),
            $"{c.Action}: собственный аккорд/жест обязан срабатывать без единого лишнего модификатора");
    }

    [TestCaseSource(nameof(Cases))]
    public void ActionFiring_Down_FiresWithAnUnclaimedExtraModifierHeld(Case c)
    {
        var state = new FakeKeyState();
        c.PressTrigger(state);
        state.Hold(c.FreeExtraModifier);
        if (c.OwnModifier != null) state.Hold(c.OwnModifier.Value);

        Assert.IsTrue(ActionFiring.Down(DefaultBindings(), state, c.Action),
            $"{c.Action}: лишний {c.FreeExtraModifier} не занят ни одним соседним действием - "
            + "не должен блокировать");
    }

    [TestCaseSource(nameof(Cases))]
    public void ActionFiring_Down_DoesNotFireWhenExtraModifierIsClaimedByAnotherAction(Case c)
    {
        if (c.ClaimedExtraModifier == null)
        {
            Assert.Pass($"{c.Action}: для этого триггера нет занятого соседями сочетания - "
                + "нечего проверять на блокировку");
            return;
        }

        var state = new FakeKeyState();
        c.PressTrigger(state);
        state.Hold(c.ClaimedExtraModifier.Value);
        if (c.OwnModifier != null) state.Hold(c.OwnModifier.Value);

        Assert.IsFalse(ActionFiring.Down(DefaultBindings(), state, c.Action),
            $"{c.Action}: добавленный {c.ClaimedExtraModifier} занят действием {c.ClaimedBy} - "
            + $"{c.Action} не должен срабатывать вместе с ним");
    }

    [Test]
    public void Undo_UnderCtrlShiftZ_DoesNotFire_AndRedoAltDoes()
    {
        var bindings = DefaultBindings();
        var state = new FakeKeyState().Press(KeyCode.Z).Hold(KeyCode.LeftControl).Hold(KeyCode.LeftShift);

        Assert.IsFalse(ActionFiring.Down(bindings, state, InputAction.Undo),
            "Ctrl+Shift+Z обязан остаться Redo (через альтернативную привязку), а не наполовину Undo");
        Assert.IsTrue(ActionFiring.Down(bindings, state, InputAction.Redo),
            "и при этом реально срабатывать как Redo через свой альтернативный аккорд");
    }

    [Test]
    public void DragAxisLockZ_UnderOccupancyAware_WouldBeBlockedByCtrl_ThatIsWhyItOptsOut()
    {
        var bindings = DefaultBindings();
        var state = new FakeKeyState().Press(KeyCode.Z).Hold(KeyCode.LeftControl);

        Assert.IsFalse(ActionFiring.Down(bindings, state, InputAction.DragAxisLockZ),
            "документирует ПОЧЕМУ ось Z не может остаться на дефолтном OccupancyAware: "
            + "Ctrl+Z данными занят под Undo, хотя во время перетаскивания Undo фактически "
            + "не сработает (UndoHandler сам проверяет ElementMover.IsDragging) - разбор в "
            + "ElementMover.ApplyDragFrame");

        Assert.IsTrue(
            ActionFiring.Down(bindings, state, InputAction.DragAxisLockZ, ChordResolution.RequiredModifiersOnly),
            "с явным RequiredModifiersOnly (то, что ElementMover реально использует через "
            + "InputMap.DownIgnoringOccupancy) ось Z под Ctrl обязана продолжать работать");
    }

    [Test]
    public void DragAxisLockX_RequiredModifiersOnly_FiresUnderCtrl()
    {
        var bindings = DefaultBindings();
        var state = new FakeKeyState().Press(KeyCode.X).Hold(KeyCode.LeftControl);

        Assert.IsTrue(
            ActionFiring.Down(bindings, state, InputAction.DragAxisLockX, ChordResolution.RequiredModifiersOnly));
    }

    [Test]
    public void UserRebindsAnActionOntoAModifiedChord_ThatModifierBecomesRequired()
    {
        var bindings = new KeyBindings();
        bindings.SetPrimary(InputAction.CameraMoveForward, new KeyChord(KeyCode.W, ctrl: true));

        var withoutCtrl = new FakeKeyState().Press(KeyCode.W);
        var withCtrl = new FakeKeyState().Press(KeyCode.W).Hold(KeyCode.LeftControl);

        Assert.IsFalse(ActionFiring.Down(bindings, withoutCtrl, InputAction.CameraMoveForward),
            "пользователь явно попросил Ctrl+W - без Ctrl это другой аккорд");
        Assert.IsTrue(ActionFiring.Down(bindings, withCtrl, InputAction.CameraMoveForward));
    }

    [Test]
    public void ClaimTable_IsRebuiltFromTheLiveBindings_NotFrozenAtStartup()
    {
        var bindings = new KeyBindings();
        var state = new FakeKeyState().Press(KeyCode.W).Hold(KeyCode.LeftControl);

        Assert.IsTrue(ActionFiring.Down(bindings, state, InputAction.CameraMoveForward),
            "предпосылка: по умолчанию Ctrl+W никем не занят");

        bindings.SetPrimary(InputAction.ToggleHelp, new KeyChord(KeyCode.W, ctrl: true));

        Assert.IsFalse(ActionFiring.Down(bindings, state, InputAction.CameraMoveForward),
            "как только пользователь переназначил ToggleHelp на Ctrl+W, то же самое "
            + "нажатие обязано перестать двигать камеру - список занятости не заморожен, "
            + "а читается из текущих KeyBindings при каждом вызове");
    }

    [Test]
    public void CameraMoveBack_UnderCtrl_DoesNotFire_ProvenRedOn84d6c8f7()
    {
        var s = new KeyChord(KeyCode.S);

        bool onCommit84d6c8f7 = ChordMatch.ModifiersMatch(
            s, ctrlHeld: true, altHeld: false, shiftHeld: false, ChordMatchMode.RequiredModifiersOnly);
        Assert.IsTrue(onCommit84d6c8f7,
            "воспроизводит ровно то вычисление, которое делал InputMap.Held на 84d6c8f7: тогда "
            + "Held по умолчанию передавал сюда RequiredModifiersOnly без учёта занятости, и "
            + "формула `!chord.Ctrl || ctrlHeld` истинна для ЛЮБОГО ctrlHeld, раз chord.Ctrl "
            + "ложен - S+Ctrl срабатывал(о) как CameraMoveBack наравне с голым S, то есть "
            + "удержание Ctrl+S двигало камеру назад, что и было доложено багом");

        var bindings = DefaultBindings();
        var state = new FakeKeyState().Press(KeyCode.S).Hold(KeyCode.LeftControl);
        Assert.IsFalse(ActionFiring.Down(bindings, state, InputAction.CameraMoveBack),
            "с занятостью (текущее поведение) то же нажатие обязано молчать - S+Ctrl занят "
            + "SaveProject");
    }

    [Test]
    public void UserRebindsAGesture_ChangesWhatTheFacadeReactsTo()
    {
        var bindings = new KeyBindings();
        var lmb = new FakeKeyState().PressButton(MouseButtonKind.Left);
        var rmbClick = new FakeKeyState().PressButton(MouseButtonKind.Right);

        Assert.IsTrue(ActionFiring.Down(bindings, lmb, InputAction.SelectClick),
            "предпосылка: по умолчанию SelectClick - это ЛКМ");
        Assert.IsFalse(ActionFiring.Down(bindings, rmbClick, InputAction.SelectClick),
            "предпосылка: ПКМ пока не назначен на выделение");

        bindings.SetPrimaryBinding(InputAction.SelectClick,
            InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Right)));

        Assert.IsFalse(ActionFiring.Down(bindings, lmb, InputAction.SelectClick),
            "после переназначения старый жест (ЛКМ) обязан замолчать - фасад реагирует на "
            + "привязку, а не запомнил исходную клавишу");
        Assert.IsTrue(ActionFiring.Down(bindings, rmbClick, InputAction.SelectClick),
            "и новый жест (ПКМ) обязан реально включить выделение - переназначение меняет то, "
            + "на что реагирует фасад, а не только то, что показывает вкладка «Управление»");
    }
}

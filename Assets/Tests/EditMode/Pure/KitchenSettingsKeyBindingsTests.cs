using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;

public class KitchenSettingsKeyBindingsTests
{
    [Test]
    public void KitchenSettings_ToData_AllDefaultBindings_ProducesAnEmptyOverrideArray()
    {
        var settings = new KitchenSettings();

        var data = settings.ToData();

        Assert.IsEmpty(data.keyBindings,
            "пока пользователь ничего не менял, в файл не должно попадать ни одной записи "
            + "про горячие клавиши");
    }

    [Test]
    public void KitchenSettings_ToData_OneOverriddenAction_WritesExactlyOneRecord()
    {
        var settings = new KitchenSettings();
        settings.KeyBindings.SetPrimaryBinding(InputAction.CameraFocusSelection, InputBinding.FromKey(new KeyChord(KeyCode.K)));

        var data = settings.ToData();

        Assert.AreEqual(1, data.keyBindings.Length,
            "переопределено одно действие — записей в файле обязана быть ровно одна, а не "
            + "по одной на всё множество действий");
        Assert.AreEqual(nameof(InputAction.CameraFocusSelection), data.keyBindings[0].action);

        var otherActionNames = InputActionCatalog.All
            .Where(a => a != InputAction.CameraFocusSelection)
            .Select(a => a.ToString());
        CollectionAssert.IsNotSubsetOf(otherActionNames, data.keyBindings.Select(o => o.action).ToArray(),
            "имя ни одного другого (дефолтного) действия не должно попасть в файл");
    }

    [Test]
    public void KitchenSettings_ApplyFrom_RestoresTheOverriddenBinding_AfterARoundTrip()
    {
        var settings = new KitchenSettings();
        var action = InputAction.Undo;
        var primary = new KeyChord(KeyCode.U);
        var alt = new KeyChord(KeyCode.Z, ctrl: true, alt: true);
        settings.KeyBindings.SetPrimaryBinding(action, InputBinding.FromKey(primary));
        settings.KeyBindings.SetAltBinding(action, InputBinding.FromKey(alt));

        var data = settings.ToData();
        var restored = new KitchenSettings();
        restored.ApplyFrom(data);

        Assert.AreEqual(InputBinding.FromKey(primary), restored.KeyBindings.PrimaryBinding(action),
            "переопределённая основная привязка обязана пережить save -> load");
        Assert.AreEqual(InputBinding.FromKey(alt), restored.KeyBindings.AltBinding(action),
            "переопределённая альтернативная привязка обязана пережить save -> load");
    }

    [Test]
    public void KitchenSettings_ApplyFrom_RestoresAMouseGestureOverride_AfterARoundTrip()
    {
        var settings = new KitchenSettings();
        var action = InputAction.CameraPan;
        var gesture = InputBinding.FromGesture(new MouseGesture(MouseButtonKind.XButton1, withMotion: true));
        settings.KeyBindings.SetPrimaryBinding(action, gesture);

        var data = settings.ToData();
        var restored = new KitchenSettings();
        restored.ApplyFrom(data);

        Assert.AreEqual(gesture, restored.KeyBindings.PrimaryBinding(action),
            "жест мыши, назначенный вместо привязки по умолчанию, обязан пережить save -> load "
            + "так же, как клавиатурный аккорд");
    }

    [Test]
    public void KitchenSettings_ApplyFrom_UntouchedActions_StayAtTheirDefaults_AfterARoundTrip()
    {
        var settings = new KitchenSettings();
        settings.KeyBindings.SetPrimaryBinding(InputAction.Undo, InputBinding.FromKey(new KeyChord(KeyCode.U)));

        var data = settings.ToData();
        var restored = new KitchenSettings();
        restored.ApplyFrom(data);

        Assert.IsTrue(restored.KeyBindings.IsDefault(InputAction.Redo),
            "действие, которое никто не переопределял, обязано остаться на дефолте и после load");
    }

    [Test]
    public void KitchenSettings_ApplyFrom_DataWithoutTheKeyBindingsField_YieldsDefaultsForEveryAction()
    {
        var oldProjectData = new KitchenSettingsData();
        var settings = new KitchenSettings();
        settings.KeyBindings.SetPrimaryBinding(InputAction.Undo, InputBinding.FromKey(new KeyChord(KeyCode.U)));

        settings.ApplyFrom(oldProjectData);

        foreach (var action in InputActionCatalog.All)
            Assert.IsTrue(settings.KeyBindings.IsDefault(action),
                $"файл без поля keyBindings (сохранённый до этой фичи) обязан читаться и "
                + $"давать привязку по умолчанию для {action}, а не падать и не подставлять мусор");
    }

    [Test]
    public void KitchenSettings_ResetToDefaults_ClearsEveryKeyBindingOverride()
    {
        var settings = new KitchenSettings();
        settings.KeyBindings.SetPrimaryBinding(InputAction.Undo, InputBinding.FromKey(new KeyChord(KeyCode.U)));

        settings.ResetToDefaults();

        Assert.IsTrue(settings.KeyBindings.IsDefault(InputAction.Undo));
    }
}

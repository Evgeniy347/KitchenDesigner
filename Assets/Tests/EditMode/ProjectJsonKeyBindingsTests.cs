using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;

public class ProjectJsonKeyBindingsTests
{
    [Test]
    public void ProjectJson_Serialize_DefaultKeyBindings_DoesNotContainTheKeyBindingsSubstring()
    {
        var settings = new KitchenSettings();
        var data = new ProjectData { settings = settings.ToData() };

        var json = ProjectJson.Serialize(data);

        StringAssert.DoesNotContain("keyBindings", json,
            "пока ни одна клавиша не переназначена, поле не должно попадать в файл вовсе");
    }

    [Test]
    public void ProjectJson_Serialize_Deserialize_OneOverride_SurvivesTheRoundTrip()
    {
        var settings = new KitchenSettings();
        var action = InputAction.Undo;
        var primary = new KeyChord(KeyCode.U);
        settings.KeyBindings.SetPrimaryBinding(action, InputBinding.FromKey(primary));

        var data = new ProjectData { settings = settings.ToData() };
        var json = ProjectJson.Serialize(data);

        StringAssert.Contains("keyBindings", json,
            "переопределённая привязка обязана попасть в файл");
        StringAssert.Contains(nameof(InputAction.Undo), json);

        var restored = ProjectJson.Deserialize(json);
        Assert.IsNotNull(restored);
        Assert.IsNotNull(restored!.settings);

        var restoredSettings = new KitchenSettings();
        restoredSettings.ApplyFrom(restored.settings);

        Assert.AreEqual(InputBinding.FromKey(primary), restoredSettings.KeyBindings.PrimaryBinding(action),
            "переопределение обязано пережить serialize -> deserialize через ProjectJson");
    }

    [Test]
    public void ProjectJson_Deserialize_ProjectWithoutTheKeyBindingsField_YieldsDefaultBindings()
    {
        var settings = new KitchenSettings();
        var oldFormatJson = ProjectJson.Serialize(new ProjectData { settings = settings.ToData() });

        var restored = ProjectJson.Deserialize(oldFormatJson);
        Assert.IsNotNull(restored);

        var restoredSettings = new KitchenSettings();
        restoredSettings.ApplyFrom(restored!.settings);

        foreach (var action in InputActionCatalog.All)
            Assert.IsTrue(restoredSettings.KeyBindings.IsDefault(action),
                $"файл без поля keyBindings обязан давать дефолт для {action}");
    }
}

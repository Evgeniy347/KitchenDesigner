using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ProjectInstructionsTests
{
    [TearDown]
    public void TearDown() => ProjectInstructions.Reset();

    [Test]
    public void TryGetPositiveMm_ReadsExplicitKeyValueLine()
    {
        ProjectInstructions.Text = "Свободный текст\n# comment\nbearing_wall_thickness_mm: 200";
        Assert.IsTrue(ProjectInstructions.TryGetPositiveMm("bearing_wall_thickness_mm", out int mm));
        Assert.AreEqual(200, mm);
    }

    [TestCase("bearing_wall_thickness_mm: nope")]
    [TestCase("bearing_wall_thickness_mm: -1")]
    [TestCase("Несущая 200 мм")]
    public void TryGetPositiveMm_RejectsAmbiguousOrInvalidValue(string text)
    {
        ProjectInstructions.Text = text;
        Assert.IsFalse(ProjectInstructions.TryGetPositiveMm("bearing_wall_thickness_mm", out _));
    }

    [Test]
    public void Panel_SaveWritesProjectInstructions()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        var ui = go.AddComponent<ProjectInstructionsPanelUI>();
        ui.Build(canvas.transform);

        var panel = canvas.transform.Find("ProjectInstructionsPanel");
        ui.SetVisible(true);
        var input = Node(panel, "PiText").GetComponent<TMP_InputField>();
        var unsaved = Node(panel, "PiUnsaved").gameObject;
        Assert.IsFalse(unsaved.activeSelf, "пока текст не тронут, футер не пугает «несохранёнными изменениями»");
        input.text = "partition_wall_thickness_mm: 100";
        Assert.IsTrue(unsaved.activeSelf, "правка видна в футере слева, пока её не сохранили (dialogs.md)");
        Node(panel, "PiSave").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual("partition_wall_thickness_mm: 100", ProjectInstructions.Text);
        Assert.IsFalse(panel.gameObject.activeSelf);
        Object.DestroyImmediate(go);
    }

    private static Transform Node(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
}

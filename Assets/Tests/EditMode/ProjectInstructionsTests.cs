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
        var input = panel.Find("PiText").GetComponent<TMP_InputField>();
        input.text = "partition_wall_thickness_mm: 100";
        panel.Find("PiSave").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual("partition_wall_thickness_mm: 100", ProjectInstructions.Text);
        Assert.IsFalse(panel.gameObject.activeSelf);
        Object.DestroyImmediate(go);
    }
}

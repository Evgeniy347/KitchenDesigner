using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class FloorSettingsLayoutTests
{
    [TearDown]
    public void TearDown()
    {
        var go = GameObject.Find("FloorSettingsCanvas");
        if (go != null) Object.DestroyImmediate(go);
    }

    [Test]
    public void ApplyButton_Y_IsBelowLastRow()
    {
        var canvasGo = new GameObject("FloorSettingsCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();

        var ui = canvasGo.AddComponent<FloorSettingsUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("FloorSettings");
        Assert.IsNotNull(root, "panel root не найден");

        var applyBtn = root.Find("FlrApply");
        Assert.IsNotNull(applyBtn, "кнопка FlrApply не найдена");
        float applyY = applyBtn.GetComponent<RectTransform>().anchoredPosition.y;

        var zField = root.Find("F_Z, м");
        Assert.IsNotNull(zField, "поле Z не найдено");
        float zFieldY = zField.GetComponent<RectTransform>().anchoredPosition.y;

        Assert.IsTrue(applyY < zFieldY,
            $"Apply Y ({applyY}) должен быть ниже поля Z ({zFieldY})");
    }

    [Test]
    public void ApplyButton_AndDelete_AreBelowLastRow_Respectively()
    {
        var canvasGo = new GameObject("ContextMenuCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();

        var ui = canvasGo.AddComponent<ContextMenuUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("ContextMenu");
        Assert.IsNotNull(root, "panel root не найден");

        var lastRow = root.Find("F_Поворот Z°");
        Assert.IsNotNull(lastRow, "последний ряд Z° не найден");
        float lastRowY = lastRow.GetComponent<RectTransform>().anchoredPosition.y;

        var applyBtn = root.Find("CtxApply");
        Assert.IsNotNull(applyBtn, "CtxApply не найдена");
        float applyY = applyBtn.GetComponent<RectTransform>().anchoredPosition.y;

        Assert.IsTrue(applyY < lastRowY,
            $"Apply ({applyY}) должен быть ниже последнего ряда ({lastRowY})");
    }
}

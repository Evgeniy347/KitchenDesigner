using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class FloorSettingsLayoutTests
{
    private static void DestroyCanvas(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        return go.AddComponent<Canvas>();
    }

    /// <summary>Все RectTransform-ы среди потомков parent, чьи имена не содержат "_".</summary>
    private static List<RectTransform> CollectLeaves(Transform parent)
    {
        var result = new List<RectTransform>();
        foreach (Transform child in parent)
        {
            // Пропускаем служебные ноды (лейблы кнопок, etc.)
            if (child.name.Contains("_")) continue;
            var rt = child.GetComponent<RectTransform>();
            if (rt != null) result.Add(rt);
            result.AddRange(CollectLeaves(child));
        }
        return result;
    }

    /// <summary>Y-координата верхнего края rect в anchor-пространстве панели.</summary>
    private static float ChildTopY(RectTransform child, float panelHeight)
    {
        if (child.anchorMin.y == 1f) // top-right anchored
            return child.anchoredPosition.y;
        // center anchored
        return -panelHeight * 0.5f + child.anchoredPosition.y + child.rect.height * 0.5f;
    }

    /// <summary>Y-координата нижнего края rect в anchor-пространстве панели.</summary>
    private static float ChildBottomY(RectTransform child, float panelHeight)
    {
        if (child.anchorMin.y == 1f)
            return child.anchoredPosition.y - child.rect.height;
        return -panelHeight * 0.5f + child.anchoredPosition.y - child.rect.height * 0.5f;
    }

    [TearDown]
    public void TearDown()
    {
        DestroyCanvas("FloorSettingsCanvas");
        DestroyCanvas("ContextMenuCanvas");
    }

    [Test]
    public void ApplyButton_Y_IsBelowLastRow()
    {
        var canvas = CreateCanvas("FloorSettingsCanvas");
        var ui = canvas.gameObject.AddComponent<FloorSettingsUI>();
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
        var canvas = CreateCanvas("ContextMenuCanvas");
        var ui = canvas.gameObject.AddComponent<ContextMenuUI>();
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

    [Test]
    public void PanelHeight_FloorSettings_FitsAllContent()
    {
        var canvas = CreateCanvas("FloorSettingsCanvas");
        var ui = canvas.gameObject.AddComponent<FloorSettingsUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("FloorSettings");
        Assert.IsNotNull(root);
        var panel = root.GetComponent<RectTransform>();
        float panelHeight = panel.sizeDelta.y;
        float panelTop = 0f;
        float panelBottom = -panelHeight;

        foreach (var child in CollectLeaves(root))
        {
            float topY = ChildTopY(child, panelHeight);
            float bottomY = ChildBottomY(child, panelHeight);
            Assert.IsTrue(topY <= panelTop + 0.01f,
                $"{child.name}: top Y ({topY:F1}) выше панели ({panelTop})");
            Assert.IsTrue(bottomY >= panelBottom - 0.01f,
                $"{child.name}: bottom Y ({bottomY:F1}) ниже панели ({panelBottom})");
        }
    }

    [Test]
    public void PanelHeight_ContextMenu_FitsAllContent()
    {
        var canvas = CreateCanvas("ContextMenuCanvas");
        var ui = canvas.gameObject.AddComponent<ContextMenuUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("ContextMenu");
        Assert.IsNotNull(root);
        var panel = root.GetComponent<RectTransform>();
        float panelHeight = panel.sizeDelta.y;
        float panelTop = 0f;
        float panelBottom = -panelHeight;

        foreach (var child in CollectLeaves(root))
        {
            float topY = ChildTopY(child, panelHeight);
            float bottomY = ChildBottomY(child, panelHeight);
            Assert.IsTrue(topY <= panelTop + 0.01f,
                $"{child.name}: top Y ({topY:F1}) выше панели ({panelTop})");
            Assert.IsTrue(bottomY >= panelBottom - 0.01f,
                $"{child.name}: bottom Y ({bottomY:F1}) ниже панели ({panelBottom})");
        }
    }
}

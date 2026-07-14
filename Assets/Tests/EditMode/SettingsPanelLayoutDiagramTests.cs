using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class SettingsPanelLayoutDiagramTests
{
    private Canvas? _canvas;

    [SetUp]
    public void Setup()
    {
        var go = new GameObject("TestCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas!.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
    }

    [Test]
    public void GenerateLayoutDiagram_Ascii_LogsToConsole()
    {
        var ui = _canvas!.gameObject.AddComponent<SettingsPanelUI>();
        ui.Build(_canvas!.transform);
        ui.SetVisible(true);

        var panel = _canvas!.transform.Find("SettingsPanel");
        var panelRt = panel.GetComponent<RectTransform>();
        float w = panelRt.sizeDelta.x;
        float h = panelRt.sizeDelta.y;

        var sb = new StringBuilder();
        string line = new string('-', 58);
        sb.AppendLine($"+{line}+");
        sb.AppendLine($"| SettingsPanelUI layout  {w}x{h}");
        sb.AppendLine($"+{line}+");

        float cx = w * 0.5f;
        float cy = h * 0.5f;

        foreach (Transform child in panel)
            DumpNode(sb, child, cx, cy, 0);

        sb.AppendLine($"+{line}+");
        Debug.Log(sb.ToString());

        Assert.IsNotNull(panel);
    }

    // ── ASCII dump ──────────────────────────────────────────

    private static void DumpNode(StringBuilder sb, Transform node, float parentCx, float parentCy, int depth)
    {
        var rt = node.GetComponent<RectTransform>();
        if (rt != null)
        {
            DumpRect(sb, rt, parentCx, parentCy, depth);
            float ncX = parentCx + rt.anchoredPosition.x;
            float ncY = parentCy + rt.anchoredPosition.y;
            foreach (Transform child in node)
                DumpNode(sb, child, ncX, ncY, depth + 1);
        }
        else
        {
            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}{node.name}: (group)");
            foreach (Transform child in node)
                DumpNode(sb, child, parentCx, parentCy, depth + 1);
        }
    }

    private static void DumpRect(StringBuilder sb, RectTransform rt, float parentCx, float parentCy, int depth)
    {
        if (!rt.gameObject.activeSelf) return;

        float topY = parentCy + rt.anchoredPosition.y + rt.sizeDelta.y * 0.5f;
        float botY = parentCy + rt.anchoredPosition.y - rt.sizeDelta.y * 0.5f;
        float leftX = parentCx + rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f;
        float rightX = parentCx + rt.anchoredPosition.x + rt.sizeDelta.x * 0.5f;

        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}{rt.name}: left={leftX:F0} right={rightX:F0} top={topY:F0} bot={botY:F0} w={rt.sizeDelta.x:F0}h={rt.sizeDelta.y:F0}");

        float ncX = parentCx + rt.anchoredPosition.x;
        float ncY = parentCy + rt.anchoredPosition.y;
        foreach (Transform child in rt)
        {
            var childRt = child.GetComponent<RectTransform>();
            if (childRt != null)
                DumpRect(sb, childRt, ncX, ncY, depth + 1);
        }
    }
}

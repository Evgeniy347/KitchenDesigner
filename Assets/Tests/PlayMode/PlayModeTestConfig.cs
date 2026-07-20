using System.Collections.Generic;
using KitchenDesigner.Core.MCP;
using UnityEngine;

/// <summary>
/// Shared configuration for PlayMode tests. Call <see cref="ConfigureForTests"/>
/// at the very beginning of each [UnitySetUp] to avoid conflicts with the
/// running editor instance (e.g. separate MCP port, disabled autosave, etc.).
/// </summary>
public static class PlayModeTestConfig
{
    /// <summary>
    /// Non-default TCP port used by the in-game MCP bridge during PlayMode tests.
    /// This keeps tests isolated from the editor instance that usually owns port 9337.
    /// </summary>
    public const int TestMcpPort = 19337;

    /// <summary>
    /// Must be called before any Bootstrap is created so that the auto-created
    /// <see cref="UnityTcpBridge"/> picks the test port in its Awake().
    /// </summary>
    public static void ConfigureForTests()
    {
#if !UNITY_WEBGL
        UnityTcpBridge.TestPort = TestMcpPort;
#endif
    }
}

/// <summary>
/// Помощники для скриншот-тестов UI.
///
/// UIManager кладёт перетаскиваемые окна (ContextMenu, DayNightPanel, GroupMenu,
/// HierarchyPanel) не в корень канвы, а в промежуточный контейнер «WindowLayer»
/// — он задаёт порядок отрисовки окон. Поэтому <c>Transform.Find(name)</c>,
/// который смотрит ТОЛЬКО прямых детей, такие панели не находит, а «спрятать
/// соседей», перебирая прямых детей канвы, прячет весь WindowLayer целиком
/// (вместе с искомой панелью) либо не прячет её соседей вовсе.
/// </summary>
public static class UiTestTree
{
    /// <summary>Рекурсивный поиск потомка по имени (поиск в ширину — ближайший к корню).</summary>
    public static Transform? FindDeep(Transform root, string name)
    {
        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (Transform child in t)
            {
                if (child.name == name) return child;
                queue.Enqueue(child);
            }
        }
        return null;
    }

    /// <summary>
    /// Спрятать всё, кроме поддерева <paramref name="panel"/>: на каждом уровне
    /// от корня канвы до панели гасятся её соседи, а сами промежуточные
    /// контейнеры (WindowLayer) остаются активными. Возвращает список
    /// погашенных объектов — восстановить через <see cref="Restore"/>.
    /// </summary>
    public static List<GameObject> HideAllExcept(Transform canvas, Transform panel)
    {
        var hidden = new List<GameObject>();
        // Цепочка предков панели вплоть до канвы: их гасить нельзя.
        var keep = new HashSet<Transform>();
        for (var t = panel; t != null && t != canvas; t = t.parent)
            keep.Add(t);

        for (var t = panel.parent; t != null; t = t.parent)
        {
            foreach (Transform sibling in t)
            {
                if (keep.Contains(sibling)) continue;
                if (!sibling.gameObject.activeSelf) continue;
                sibling.gameObject.SetActive(false);
                hidden.Add(sibling.gameObject);
            }
            if (t == canvas) break;
        }
        return hidden;
    }

    public static void Restore(List<GameObject> hidden)
    {
        foreach (var go in hidden)
            if (go != null) go.SetActive(true);
    }
}

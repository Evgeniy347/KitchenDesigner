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
        KitchenDesigner.Core.McpBridgeStatus.TestPort = TestMcpPort;
        // Даже если тест случайно создаст UpdateService — проверка обновлений
        // (сеть на GitHub) не должна запускаться.
        KitchenDesigner.Core.Update.UpdateService.StartupCheckEnabled = false;
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

/// <summary>
/// Окна проекта в скриншот-тестах.
///
/// Файл проекта хранит, какие окна были открыты, и <c>RestoreScene</c> их
/// восстанавливает (<c>ProjectWindows.Apply</c>). Для эталонного рендера это
/// недопустимый источник недетерминизма: <c>docs/example.save.json</c>
/// перезаписывается автосохранением десктопа, и вместе с ним в кадр приезжает
/// тот набор окон, который был открыт в момент сохранения — тест краснеет с
/// диффом вида «Кухня → Ошибка». Состав окон задаёт САМ тест, сразу после
/// загрузки проекта.
/// </summary>
public static class ProjectWindowsTestState
{
    /// <summary>Закрыть все окна проекта, оставив открытым только
    /// <paramref name="windowId"/>. null — закрыть все.</summary>
    public static void ShowOnly(string? windowId)
    {
        foreach (var w in KitchenDesigner.Core.UI.ProjectWindows.All)
            w.SetVisible(w.WindowId == windowId);
    }
}

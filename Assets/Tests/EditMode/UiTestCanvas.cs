using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiTestCanvas
{
    private static bool _eventSystemExistedBefore;

    public static GameObject Create(string name)
    {
        _eventSystemExistedBefore = Object.FindAnyObjectByType<EventSystem>() != null;
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    public static void Release(GameObject? canvas)
    {
        if (canvas != null) Object.DestroyImmediate(canvas);
        if (_eventSystemExistedBefore) return;
        var created = Object.FindAnyObjectByType<EventSystem>();
        if (created != null) Object.DestroyImmediate(created.gameObject);
    }
}

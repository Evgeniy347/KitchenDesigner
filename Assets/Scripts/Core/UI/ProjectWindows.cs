using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public interface IProjectWindow
    {
        string WindowId { get; }

        RectTransform? WindowRect { get; }

        bool HeightAdjustable { get; }

        bool IsVisible { get; }

        void SetVisible(bool visible);
    }

    public static class ProjectWindows
    {
        private static readonly List<IProjectWindow> Registered = new List<IProjectWindow>();

        public static void Register(IProjectWindow window)
        {
            DropDestroyedWindows();
            if (window != null && !Registered.Contains(window)) Registered.Add(window);
        }

        public static void Unregister(IProjectWindow window) => Registered.Remove(window);

        public static void Clear() => Registered.Clear();

        public static IReadOnlyList<IProjectWindow> All
        {
            get { DropDestroyedWindows(); return Registered; }
        }

        public static WindowStateData[] Capture()
        {
            DropDestroyedWindows();
            var built = new List<WindowStateData>();
            foreach (var w in Registered)
            {
                var rect = w.WindowRect;
                if (rect == null) continue;
                var pos = rect.anchoredPosition;
                built.Add(new WindowStateData
                {
                    id = w.WindowId,
                    visible = w.IsVisible,
                    x = pos.x,
                    y = pos.y,
                    height = w.HeightAdjustable ? rect.sizeDelta.y : 0f,
                });
            }
            built.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return built.ToArray();
        }

        public static void Apply(WindowStateData[]? states)
        {
            if (states == null || states.Length == 0) return;
            DropDestroyedWindows();

            foreach (var state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.id)) continue;
                var window = Find(state.id);
                if (window == null) continue;

                RestorePlacement(window, state);
                window.SetVisible(state.visible);
            }
        }

        private static void RestorePlacement(IProjectWindow window, WindowStateData state)
        {
            var rect = window.WindowRect;
            if (rect == null) return;

            rect.anchoredPosition = new Vector2(state.x, state.y);
            if (window.HeightAdjustable && state.height > 0f)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, state.height);
        }

        private static IProjectWindow? Find(string id)
        {
            foreach (var w in Registered)
                if (w.WindowId == id) return w;
            return null;
        }

        private static void DropDestroyedWindows()
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var w = Registered[i];
                if (w == null || (w is Object obj && obj == null)) Registered.RemoveAt(i);
            }
        }
    }
}

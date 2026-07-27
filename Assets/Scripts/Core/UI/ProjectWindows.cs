using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Окно проекта: его положение на экране и состояние
    /// «открыто/закрыто» сохраняются в файл проекта и восстанавливаются при
    /// загрузке. Контекстные меню (детали, группа) сюда НЕ входят — они живут
    /// на выделении, а не на проекте.</summary>
    public interface IProjectWindow
    {
        /// <summary>Стабильный ключ в файле проекта. Переименование ключа
        /// обесценивает уже сохранённые проекты — окно вернётся к дефолту.</summary>
        string WindowId { get; }

        /// <summary>Корень окна; null, пока окно не построено (Build).</summary>
        RectTransform? WindowRect { get; }

        /// <summary>Высоту окна задаёт пользователь (нижний хэндл) — её тоже
        /// нужно сохранять. У окон фиксированного размера — false.</summary>
        bool HeightAdjustable { get; }

        bool IsVisible { get; }

        void SetVisible(bool visible);
    }

    /// <summary>Реестр окон проекта: снимает их состояние в ProjectData и
    /// возвращает обратно при загрузке.</summary>
    public static class ProjectWindows
    {
        private static readonly List<IProjectWindow> Registered = new List<IProjectWindow>();

        public static void Register(IProjectWindow window)
        {
            Prune();
            if (window != null && !Registered.Contains(window)) Registered.Add(window);
        }

        public static void Unregister(IProjectWindow window) => Registered.Remove(window);

        /// <summary>Только для тестов: реестр — статик и переживает смену сцены.</summary>
        public static void Clear() => Registered.Clear();

        public static IReadOnlyList<IProjectWindow> All
        {
            get { Prune(); return Registered; }
        }

        /// <summary>Снять состояние всех построенных окон. Порядок — по id,
        /// чтобы JSON проекта не зависел от порядка построения UI.</summary>
        public static WindowStateData[] Capture()
        {
            Prune();
            var list = new List<WindowStateData>();
            foreach (var w in Registered)
            {
                var rect = w.WindowRect;
                if (rect == null) continue; // окно ещё не построено — нечего сохранять
                var pos = rect.anchoredPosition;
                list.Add(new WindowStateData
                {
                    id = w.WindowId,
                    visible = w.IsVisible,
                    x = pos.x,
                    y = pos.y,
                    height = w.HeightAdjustable ? rect.sizeDelta.y : 0f,
                });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list.ToArray();
        }

        /// <summary>Вернуть состояние окон из файла проекта. Незнакомые id
        /// игнорируются (сейв нового приложения в старом), окна без записи
        /// остаются как есть — у старых сейвов массива просто нет.</summary>
        public static void Apply(WindowStateData[]? states)
        {
            if (states == null || states.Length == 0) return;
            Prune();

            foreach (var state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.id)) continue;
                var window = Find(state.id);
                if (window == null) continue;

                var rect = window.WindowRect;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(state.x, state.y);
                    if (window.HeightAdjustable && state.height > 0f)
                        rect.sizeDelta = new Vector2(rect.sizeDelta.x, state.height);
                }

                // Позицию ставим ДО показа: WindowScreenGuard клампит окно в
                // LateUpdate после OnEnable и уже по восстановленным координатам.
                window.SetVisible(state.visible);
            }
        }

        private static IProjectWindow? Find(string id)
        {
            foreach (var w in Registered)
                if (w.WindowId == id) return w;
            return null;
        }

        // Панели живут на сцене: после её выгрузки в реестре остаются
        // уничтоженные MonoBehaviour (fake-null), обращение к ним бросает.
        private static void Prune()
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var w = Registered[i];
                if (w == null || (w is Object obj && obj == null)) Registered.RemoveAt(i);
            }
        }
    }
}

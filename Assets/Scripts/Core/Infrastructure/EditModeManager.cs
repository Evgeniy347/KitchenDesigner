using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Три режима работы редактора.</summary>
    public enum EditMode
    {
        /// <summary>Обычный: стены и пол выделять нельзя, остальное — можно.</summary>
        Normal,
        /// <summary>Помещение: доступны только «помещение» (стена, пол); детали,
        /// фасады, ящики, мебель заблокированы.</summary>
        Room,
        /// <summary>Фоторежим: ограничения как в «обычном» + включён PhotoMode
        /// (рендер: потолок, тени, качество).</summary>
        Photo,
    }

    /// <summary>
    /// Режим редактора и правила «что можно выделять/редактировать мышью».
    /// Ограничивается ТОЛЬКО пользовательский ввод (клик, drag, ПКМ-меню):
    /// программное выделение через <see cref="SelectionManager.Select"/> не
    /// трогаем — оно нужно MCP, окну «Ошибки» и тестам. Короб, окно и дверь
    /// редактируются в любом режиме. Состояние рантайм-только, в проект не
    /// сохраняется (как <see cref="PhotoMode.Active"/>).
    /// </summary>
    public static class EditModeManager
    {
        public static EditMode Mode { get; private set; } = EditMode.Normal;

        /// <summary>Смена режима — для тулбара и сайдбара.</summary>
        public static event Action? Changed;

        private static bool _subscribed;

        static EditModeManager() => EnsureSubscribed();

        // Тумблер «Фоторежим» в настройках и F10 меняют PhotoMode напрямую —
        // держим Mode согласованным с ним в обе стороны без рекурсии
        // (SetActive/SetMode рано выходят, если значение не изменилось).
        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            PhotoMode.Changed += OnPhotoChanged;
        }

        private static void OnPhotoChanged()
        {
            if (PhotoMode.Active && Mode != EditMode.Photo) SetMode(EditMode.Photo);
            else if (!PhotoMode.Active && Mode == EditMode.Photo) SetMode(EditMode.Normal);
        }

        /// <summary>Цикл кнопки: фоторежим → помещение → обычный → фоторежим.</summary>
        public static void Cycle()
        {
            SetMode(Mode switch
            {
                EditMode.Photo => EditMode.Room,
                EditMode.Room => EditMode.Normal,
                _ => EditMode.Photo,
            });
        }

        public static void SetMode(EditMode mode)
        {
            EnsureSubscribed();
            if (mode == Mode) return;
            Mode = mode;

            bool wantPhoto = mode == EditMode.Photo;
            if (PhotoMode.Active != wantPhoto) PhotoMode.SetActive(wantPhoto);

            // Часть объектов в новом режиме недоступна — снимаем выделение,
            // чтобы у скрытых ручек/меню не осталось «залипшей» цели.
            SelectionManager.Instance?.DeselectAll();

            Changed?.Invoke();
        }

        /// <summary>Сброс к исходному режиму (изоляция тестов, глобальное
        /// состояние — см. правила снапшотов в AGENTS.md).</summary>
        public static void Reset() => Mode = EditMode.Normal;

        /// <summary>Читаемое название текущего режима для подписи кнопки.</summary>
        public static string Label(EditMode mode) => mode switch
        {
            EditMode.Room => "Режим: помещение",
            EditMode.Photo => "Режим: фото",
            _ => "Режим: обычный",
        };

        // ── Классификация объектов ───────────────────────────────────────

        public enum Category
        {
            /// <summary>Детали, фасады, ящики, мебель, свет — «рабочие» объекты.</summary>
            Regular,
            /// <summary>Стена и пол — конструкция помещения.</summary>
            Room,
            /// <summary>Короб, окно, дверь — редактируются в любом режиме.</summary>
            Always,
        }

        /// <summary>Короб — единственный «всегда редактируемый» объект без
        /// собственного типа: спавнится как базовая деталь, отличаем по имени.</summary>
        public const string KorobName = "Короб";

        public static Category Categorize(KitchenElement e)
        {
            if (e == null) return Category.Regular;
            if (e is WindowElement || e is DoorElement) return Category.Always;
            if (e.PartName == KorobName) return Category.Always;
            if (e is FloorElement || e.GetComponent<Wall>() != null || e.GetComponent<BasePlate>() != null)
                return Category.Room;
            return Category.Regular;
        }

        /// <summary>Доступна ли категория для выделения/редактирования сейчас.</summary>
        public static bool IsCategoryActive(Category cat)
        {
            if (cat == Category.Always) return true;
            return Mode == EditMode.Room ? cat == Category.Room : cat == Category.Regular;
        }

        /// <summary>Можно ли выделять/редактировать объект мышью в текущем режиме.</summary>
        public static bool IsInteractable(KitchenElement e) => IsCategoryActive(Categorize(e));
    }
}

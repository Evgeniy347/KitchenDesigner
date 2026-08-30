using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum EditMode
    {
        Normal,
        Room,
        Photo,
    }

    public static class EditModeManager
    {
        public static EditMode Mode { get; private set; } = EditMode.Normal;

        public static event Action? Changed;

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
            if (mode == Mode) return;
            bool wasPhoto = Mode == EditMode.Photo;
            bool isPhoto = mode == EditMode.Photo;

            Mode = mode;

            if (isPhoto && !wasPhoto) PhotoMode.Enter();
            else if (!isPhoto && wasPhoto) PhotoMode.Exit();

            SelectionManager.Instance?.DeselectAll();

            SceneVisibilityManager.Invalidate();

            if (isPhoto != wasPhoto) PhotoMode.RaiseChanged();
            Changed?.Invoke();
        }

        public static void Reset() => SetMode(EditMode.Normal);

        public static string Label(EditMode mode) => mode switch
        {
            EditMode.Room => "Режим: помещение",
            EditMode.Photo => "Режим: фото",
            _ => "Режим: обычный",
        };

        public enum Category
        {
            Regular,
            Room,
            Always,
        }

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

        public static bool IsCategoryActive(Category cat)
        {
            if (cat == Category.Always) return true;
            return Mode == EditMode.Room ? cat == Category.Room : cat == Category.Regular;
        }

        public static bool IsInteractable(KitchenElement e) => IsCategoryActive(Categorize(e));
    }
}

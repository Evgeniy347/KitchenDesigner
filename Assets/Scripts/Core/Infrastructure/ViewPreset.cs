using System;

namespace KitchenDesigner.Core
{
    /// <summary>Тумблер вида — адресация для UI и таблицы блокировок.</summary>
    public enum ViewField
    {
        Walls,
        WallOutline,
        LowerNearWalls,
        HideOpeningsOnLoweredWalls,
        Objects,
        ObjectOutline,
        HideLightSources,
    }

    /// <summary>Набор «что показывать» для одного режима работы. Пресетов два —
    /// «обычный» и «помещение»; в каком режиме пользователь погасил стены, там
    /// они и остаются погашенными, и переключение режима ничего не затирает.
    /// Инициализаторы полей задают значения «из коробки» и одновременно служат
    /// дефолтами для старых проектов: JsonUtility создаёт объект (инициализаторы
    /// срабатывают), а потом перезаписывает только присутствующие в JSON поля.</summary>
    [Serializable]
    public class ViewPreset
    {
        public bool wallsEnabled = true;
        public bool wallOutline = true;
        public bool lowerNearWalls = true;
        public bool hideOpeningsOnLoweredWalls = false;
        public bool objectsVisible = true;
        public bool edgeOutline = true;
        public bool hideLightSources = false;

        public bool Get(ViewField field) => field switch
        {
            ViewField.Walls => wallsEnabled,
            ViewField.WallOutline => wallOutline,
            ViewField.LowerNearWalls => lowerNearWalls,
            ViewField.HideOpeningsOnLoweredWalls => hideOpeningsOnLoweredWalls,
            ViewField.Objects => objectsVisible,
            ViewField.ObjectOutline => edgeOutline,
            _ => hideLightSources,
        };

        public void Set(ViewField field, bool value)
        {
            switch (field)
            {
                case ViewField.Walls: wallsEnabled = value; break;
                case ViewField.WallOutline: wallOutline = value; break;
                case ViewField.LowerNearWalls: lowerNearWalls = value; break;
                case ViewField.HideOpeningsOnLoweredWalls: hideOpeningsOnLoweredWalls = value; break;
                case ViewField.Objects: objectsVisible = value; break;
                case ViewField.ObjectOutline: edgeOutline = value; break;
                default: hideLightSources = value; break;
            }
        }

        public void CopyFrom(ViewPreset? other)
        {
            if (other == null) return;
            wallsEnabled = other.wallsEnabled;
            wallOutline = other.wallOutline;
            lowerNearWalls = other.lowerNearWalls;
            hideOpeningsOnLoweredWalls = other.hideOpeningsOnLoweredWalls;
            objectsVisible = other.objectsVisible;
            edgeOutline = other.edgeOutline;
            hideLightSources = other.hideLightSources;
        }

        public ViewPreset Clone()
        {
            var copy = new ViewPreset();
            copy.CopyFrom(this);
            return copy;
        }

        public void ResetToDefaults() => CopyFrom(new ViewPreset());
    }

    /// <summary>Что реально применяется к сцене прямо сейчас: значения плюс
    /// пометка «поле форсировано режимом и не редактируется». Значения и
    /// блокировки считаются одной таблицей (<see cref="ViewResolver"/>), чтобы
    /// UI не мог разрешить то, что рендер всё равно проигнорирует.</summary>
    public readonly struct ViewState
    {
        public readonly bool WallsEnabled;
        public readonly bool WallOutline;
        public readonly bool LowerNearWalls;
        public readonly bool HideOpeningsOnLoweredWalls;
        public readonly bool ObjectsVisible;
        public readonly bool EdgeOutline;
        public readonly bool HideLightSources;

        private readonly int _locked;

        public ViewState(bool wallsEnabled, bool wallOutline, bool lowerNearWalls,
            bool hideOpeningsOnLoweredWalls, bool objectsVisible, bool edgeOutline,
            bool hideLightSources, int locked)
        {
            WallsEnabled = wallsEnabled;
            WallOutline = wallOutline;
            LowerNearWalls = lowerNearWalls;
            HideOpeningsOnLoweredWalls = hideOpeningsOnLoweredWalls;
            ObjectsVisible = objectsVisible;
            EdgeOutline = edgeOutline;
            HideLightSources = hideLightSources;
            _locked = locked;
        }

        public bool Get(ViewField field) => field switch
        {
            ViewField.Walls => WallsEnabled,
            ViewField.WallOutline => WallOutline,
            ViewField.LowerNearWalls => LowerNearWalls,
            ViewField.HideOpeningsOnLoweredWalls => HideOpeningsOnLoweredWalls,
            ViewField.Objects => ObjectsVisible,
            ViewField.ObjectOutline => EdgeOutline,
            _ => HideLightSources,
        };

        /// <summary>Поле форсировано режимом: показываем значение, но менять нельзя.</summary>
        public bool IsLocked(ViewField field) => (_locked & (1 << (int)field)) != 0;

        /// <summary>Биты видимости для дешёвой проверки «настройки не менялись».</summary>
        public int VisibilityHash =>
            (WallsEnabled ? 1 : 0) | (LowerNearWalls ? 2 : 0)
            | (HideOpeningsOnLoweredWalls ? 4 : 0) | (ObjectsVisible ? 8 : 0)
            | (EdgeOutline ? 16 : 0) | (WallOutline ? 32 : 0) | (HideLightSources ? 64 : 0);
    }

    /// <summary>Единственное место, где закодировано «какой режим что показывает».
    /// И рендер (<see cref="WallManager"/>, <see cref="SceneVisibilityManager"/>,
    /// <see cref="EdgeOutlineRenderer"/>), и панель настроек читают отсюда —
    /// раньше эта таблица была размазана по трём файлам и они разъезжались.
    ///
    /// Фоторежим собственного пресета не имеет: комната цельная, всё видно,
    /// прятать в кадре нечего. Контуры — исключение: это чисто визуальная
    /// опция, а не сокрытие геометрии, поэтому в фото они берутся из пресета
    /// обычного режима (менять их на ходу всё равно нельзя — в фото
    /// заблокировано всё).</summary>
    public static class ViewResolver
    {
        // Настройки могут быть не загружены (тесты без Resources) — тогда
        // работаем по значениям «из коробки». Мутировать этот экземпляр нельзя.
        private static readonly ViewPreset Fallback = new ViewPreset();

        private const int RoomLocked =
            (1 << (int)ViewField.Walls)
            | (1 << (int)ViewField.LowerNearWalls)
            | (1 << (int)ViewField.HideOpeningsOnLoweredWalls);

        private const int PhotoLocked =
            RoomLocked
            | (1 << (int)ViewField.Objects)
            | (1 << (int)ViewField.HideLightSources);

        /// <summary>Пресет, который редактируется в этом режиме. Фоторежим правит
        /// пресет обычного — он и есть «обычный + фоторендер».</summary>
        public static ViewPreset PresetFor(EditMode mode, KitchenSettings? s)
        {
            if (s == null) return Fallback;
            return mode == EditMode.Room ? s.RoomView : s.NormalView;
        }

        public static ViewState Current => Resolve(EditModeManager.Mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode) => Resolve(mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode, KitchenSettings? s)
        {
            var p = PresetFor(mode, s);
            switch (mode)
            {
                case EditMode.Room:
                    // Правят саму конструкцию: стены целые и на месте, обрезанные
                    // мешали бы. Объекты и свет — на усмотрение пользователя.
                    return new ViewState(
                        wallsEnabled: true,
                        wallOutline: p.wallOutline,
                        lowerNearWalls: false,
                        hideOpeningsOnLoweredWalls: false,
                        objectsVisible: p.objectsVisible,
                        edgeOutline: p.edgeOutline,
                        hideLightSources: p.hideLightSources,
                        locked: RoomLocked);

                case EditMode.Photo:
                    return new ViewState(
                        wallsEnabled: true,
                        wallOutline: p.wallOutline,
                        lowerNearWalls: false,
                        hideOpeningsOnLoweredWalls: false,
                        objectsVisible: true,
                        edgeOutline: p.edgeOutline,
                        hideLightSources: false,
                        locked: PhotoLocked);

                default:
                    return new ViewState(
                        wallsEnabled: p.wallsEnabled,
                        wallOutline: p.wallOutline,
                        lowerNearWalls: p.lowerNearWalls,
                        hideOpeningsOnLoweredWalls: p.hideOpeningsOnLoweredWalls,
                        objectsVisible: p.objectsVisible,
                        edgeOutline: p.edgeOutline,
                        hideLightSources: p.hideLightSources,
                        locked: 0);
            }
        }

        /// <summary>Можно ли менять тумблер: пресет режима <paramref name="presetMode"/>
        /// открыт на вкладке, а редактор сейчас в режиме <paramref name="currentMode"/>.
        /// В фоторежиме заблокировано всё — там смотрят картинку, а не правят сцену.</summary>
        public static bool IsEditable(EditMode currentMode, EditMode presetMode, ViewField field)
        {
            if (currentMode == EditMode.Photo) return false;
            return !Resolve(presetMode).IsLocked(field);
        }

        /// <summary>Подопция без включённого родителя бессмысленна (правило дерева
        /// из UI-GUIDELINES): контур стен — при выключенных стенах, «скрывать окна
        /// и двери» — при выключенном опускании, контур объектов — при выключенных
        /// объектах.</summary>
        public static ViewField? ParentOf(ViewField field) => field switch
        {
            ViewField.WallOutline => ViewField.Walls,
            ViewField.LowerNearWalls => ViewField.Walls,
            ViewField.HideOpeningsOnLoweredWalls => ViewField.LowerNearWalls,
            ViewField.ObjectOutline => ViewField.Objects,
            _ => null,
        };
    }
}

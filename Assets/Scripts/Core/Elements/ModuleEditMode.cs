using System;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Режим редактирования модуля (именованной группы, см. <see cref="GroupManager"/>).
    /// Инверсия обычного поведения группы: вне режима группа выделяется/двигается
    /// целиком и её состав менять нельзя; внутри режима детали АКТИВНОГО модуля
    /// редактируются поштучно, а всё остальное в сцене заблокировано (нельзя
    /// выделить, сдвинуть, ресайзить) и визуально затемнено.
    /// Вход: двойной клик по модулю или кнопка в меню группы / MCP. Выход: Esc,
    /// кнопка «Готово» или MCP.
    /// </summary>
    public static class ModuleEditMode
    {
        /// <summary>Редактируемый сейчас модуль; null — обычный режим.</summary>
        public static LinkGroup? Active { get; private set; }

        public static bool IsActive => Active != null;

        /// <summary>Вход/выход/смена активного модуля.</summary>
        public static event Action Changed = null!;

        public static void Enter(LinkGroup module)
        {
            if (module == null || Active == module) return;
            Active = module;
            Changed?.Invoke();
        }

        public static void Exit()
        {
            if (Active == null) return;
            Active = null;
            Changed?.Invoke();
        }

        /// <summary>Можно ли сейчас взаимодействовать с элементом (выделение,
        /// перемещение, ресайз, контекстное меню). Вне режима — можно со всеми;
        /// в режиме — только с деталями активного модуля.</summary>
        public static bool IsEditable(KitchenElement e)
        {
            if (!IsActive) return true;
            return e != null && e.GroupId == Active!.id;
        }
    }
}

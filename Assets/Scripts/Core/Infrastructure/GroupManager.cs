using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    /// <summary>Группа связанных объектов («замок»): общий id у элементов.</summary>
    public class LinkGroup
    {
        public int id;
        public string name = "Группа";
        public bool movable = true;
        public string widthAxis = "x";
    }

    /// <summary>Статический фасад над <see cref="IGroupService"/> (паттерн PartRegistry/
    /// CommandStack): реализация — <see cref="GroupServiceInstance"/> из GameContext
    /// либо fallback. Существующие вызовы GroupManager.* по проекту остаются валидными.</summary>
    public static class GroupManager
    {
        internal static IGroupService Instance
        {
            get
            {
                if (GameContext.Services != null && GameContext.Services.GroupService != null)
                    return GameContext.Services.GroupService;
                if (_fallback == null)
                    _fallback = new GroupServiceInstance();
                return _fallback!;
            }
            set => _fallback = value;
        }
        private static IGroupService? _fallback;

        /// <summary>Любое изменение групп (создание/роспуск/состав/имя/подвижность).
        /// Подписка идёт на ТЕКУЩИЙ Instance — подписывайтесь после инициализации
        /// GameContext (в UI это Start(), безопасно).</summary>
        public static event Action? Changed
        {
            add { Instance.Changed += value; }
            remove { Instance.Changed -= value; }
        }

        /// <summary>Создать ПУСТУЮ группу — элементы добавляются позже (AddTo/MoveTo).</summary>
        public static LinkGroup Create(string name) => Instance.Create(name);

        public static LinkGroup? Link(IList<KitchenElement> members) => Instance.Link(members);

        public static void Unlink(LinkGroup g) => Instance.Unlink(g);

        public static LinkGroup? GroupOf(KitchenElement e) => Instance.GroupOf(e);

        public static List<KitchenElement> MembersOf(LinkGroup g) => Instance.MembersOf(g);

        public static IEnumerable<LinkGroup> AllGroups() => Instance.AllGroups();

        public static void AddTo(LinkGroup g, KitchenElement e) => Instance.AddTo(g, e);

        public static void RemoveFrom(KitchenElement e) => Instance.RemoveFrom(e);

        /// <summary>Переместить элемент в группу; null = «вне групп».</summary>
        public static void MoveTo(KitchenElement e, LinkGroup? g) => Instance.MoveTo(e, g);

        public static void Rename(LinkGroup g, string name) => Instance.Rename(g, name);

        /// <summary>Применить подвижность группы ко всем её элементам.</summary>
        public static void SetMovable(LinkGroup g, bool movable) => Instance.SetMovable(g, movable);

        public static void Clear() => Instance.Clear();

        /// <summary>Восстановление группы из сохранения.</summary>
        public static LinkGroup Register(int id, string name, bool movable, string widthAxis = "x") =>
            Instance.Register(id, name, movable, widthAxis);
    }
}

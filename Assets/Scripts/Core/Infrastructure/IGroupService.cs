using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Единая точка работы с группами (модулями): создание/роспуск, состав,
    /// перемещение элементов между группами, подвижность, переименование.
    ///
    /// Зачем интерфейс, а не только статический GroupManager:
    /// - событие <see cref="Changed"/> — UI (панель иерархии) и другие подписчики
    ///   узнают о любом изменении групп без поллинга;
    /// - операции уровня «переместить элемент из группы в группу» с валидацией
    ///   живут в одном месте (панель, MCP и меню группы используют один код);
    /// - реализацию можно подменить в тестах (паттерн IPartRegistry/ICommandStack).
    ///
    /// Статический фасад <see cref="GroupManager"/> делегирует сюда — существующие
    /// вызовы по всему проекту остаются валидными.
    /// </summary>
    public interface IGroupService
    {
        /// <summary>Любое изменение групп: создание, роспуск, состав, имя, подвижность.</summary>
        event Action? Changed;

        /// <summary>Создать ПУСТУЮ группу (элементы добавляются позже через AddTo/MoveTo).</summary>
        LinkGroup Create(string name);

        /// <summary>Связать 2+ элементов в новую группу (классический «замок»).</summary>
        LinkGroup? Link(IList<KitchenElement> members);

        /// <summary>Распустить группу; элементы остаются в сцене.</summary>
        void Unlink(LinkGroup g);

        LinkGroup? GroupOf(KitchenElement e);
        List<KitchenElement> MembersOf(LinkGroup g);
        IEnumerable<LinkGroup> AllGroups();

        /// <summary>Добавить элемент в группу (из другой группы — с переносом).</summary>
        void AddTo(LinkGroup g, KitchenElement e);

        /// <summary>Убрать элемент из его группы (вне группы — no-op).</summary>
        void RemoveFrom(KitchenElement e);

        /// <summary>Переместить элемент в группу; null = «вне групп».</summary>
        void MoveTo(KitchenElement e, LinkGroup? g);

        void Rename(LinkGroup g, string name);

        /// <summary>Подвижность группы применяется ко всем её элементам.</summary>
        void SetMovable(LinkGroup g, bool movable);

        /// <summary>Восстановление группы из сохранения (id из файла).</summary>
        LinkGroup Register(int id, string name, bool movable);

        void Clear();
    }
}

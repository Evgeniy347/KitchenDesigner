namespace KitchenDesigner.Core
{
    /// <summary>
    /// Встраиваемая техника готовой модели: габариты заданы производителем и не
    /// редактируются — ни в окне свойств (поля Ш/В/Г серые), ни мышью
    /// (ресайз-ручки не строятся), ни из MCP.
    ///
    /// Свойство, а не пустой маркер: у варочной поверхности ОДИН класс обслуживает
    /// и свободный элемент «Варочная поверхность» из группы «Мебель» (размеры
    /// правятся), и фиксированный пресет из группы «Техника» (размеры залочены) —
    /// маркер-интерфейс различить их не смог бы. Духовке и посудомойке, у которых
    /// фиксирована вся модель, достаточно вернуть <c>true</c> константой.
    /// </summary>
    public interface IFixedSizeElement
    {
        /// <summary>Габариты этого экземпляра заданы производителем.</summary>
        bool HasFixedSize { get; }
    }

    /// <summary>Один вопрос «размеры залочены?» для UI, ручек и сериализации —
    /// чтобы условие не расползлось по вызовам разными формулировками.</summary>
    public static class FixedSize
    {
        public static bool IsFixed(object? element) =>
            element is IFixedSizeElement fixedSize && fixedSize.HasFixedSize;

        /// <summary>У встраиваемой техники из поворотов осмыслен ТОЛЬКО разворот
        /// вокруг вертикали: прибор стоит в нише корпуса, «на боку» и «вверх
        /// ногами» его не ставят. Поля X/Z в окне свойств у таких элементов не
        /// показываются вовсе, а MCP отказывает в <c>rot_x</c>/<c>rot_z</c>.
        ///
        /// Признак стоит на самом <see cref="IFixedSizeElement"/>, а не на списке
        /// из трёх <c>is</c>: этот интерфейс реализует ВСЯ техника группы
        /// «Техника» (варочная, духовка, посудомойка), поэтому четвёртый прибор
        /// подхватит правило одной строкой объявления, ничего не дописывая сюда.
        /// Условие — сам ТИП, а не <see cref="IFixedSizeElement.HasFixedSize"/>:
        /// свободная варочная из группы «Мебель» размеры правит, но лежит она всё
        /// равно в пласти столешницы, и «на бок» её тоже не кладут.</summary>
        public static bool IsYawOnly(object? element) => element is IFixedSizeElement;
    }

    /// <summary>Реестр готовых моделей встраиваемой техники — ОДНО место, где
    /// строка модели (из MCP или из сейва) проверяется на «такой прибор у нас
    /// есть». Новый прибор дописывает сюда одну строку; без этого его модель
    /// молча отвергалась бы как чужая.</summary>
    public static class ApplianceModels
    {
        /// <summary>Все модели из группы «Техника» одним списком — на нём же
        /// стоит перечисление в MCP-контракте (<c>CreateItem.model</c>).</summary>
        public static readonly string[] All =
        {
            CooktopElement.MODEL_BOSCH_PUE611BB5E,
            OvenElement.MODEL,
            DishwasherElement.MODEL,
        };

        public static bool IsKnown(string? model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            foreach (var known in All)
                if (known == model) return true;
            return false;
        }
    }
}

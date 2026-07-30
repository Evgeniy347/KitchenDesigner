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

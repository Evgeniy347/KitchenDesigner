using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class HintText
    {
        public static readonly IReadOnlyDictionary<string, string> All =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["settings.view.preset"] =
                    "Набор настроек показа хранится отдельно для каждого режима. Переключатель "
                    + "выбирает, чей набор вы сейчас правите.",
                ["settings.view.walls"] =
                    "Стены можно убрать целиком, чтобы работать с расстановкой сверху и не "
                    + "смотреть сквозь них.",
                ["settings.view.wallOutline"] =
                    "Тонкая линия по краю стены. Разделяет смежные стены одного цвета, которые "
                    + "иначе сливаются в одно пятно.",
                ["settings.view.lowerNearWalls"] =
                    "Стены между камерой и кухней опускаются до столешницы. Обзор открывается, "
                    + "а план помещения остаётся на месте.",
                ["settings.view.lowerAllWalls"] =
                    "Опускаются все стены сразу, а не только ближние к камере. Нужно для вида "
                    + "сверху на всё помещение.",
                ["settings.view.hideOpeningsOnLoweredWalls"] =
                    "Окна и двери уходят вместе с опущенной частью стены. Иначе они висят в "
                    + "воздухе над срезом.",
                ["settings.view.objects"] =
                    "Мебель и технику можно убрать, оставив только стены и проёмы. Удобно, когда "
                    + "правите само помещение.",
                ["settings.view.objectOutline"] =
                    "Тонкая линия по краю каждого объекта. Разделяет соседние шкафы с одинаковым "
                    + "декором.",
                ["settings.view.hideLightSources"] =
                    "Из сцены пропадают сами лампы и светильники, а их свет продолжает работать. "
                    + "Нужно для чистого кадра.",
            };

        public static bool Has(string key) => All.ContainsKey(key);

        public static string Of(string key)
        {
            if (All.TryGetValue(key, out var text)) return text;
            throw new KeyNotFoundException(
                "Нет текста подсказки для ключа «" + key + "» — заполните HintText.All");
        }
    }
}

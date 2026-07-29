using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Высота ПУНКТА выпадающего списка под самое длинное название.
    ///
    /// Названия декоров бывают длинными («Дуб давенпорт натуральный светлый
    /// (H3359 ST32)»), а пункт в одну строку их обрезал: список показывал
    /// «Дуб давенпорт натур…», и отличить два соседних декора было нельзя.
    /// Пункт должен стать выше ровно настолько, сколько строк займёт перенос.
    ///
    /// Считаем БЕЗ метрик шрифта: раскладка обязана быть одинаковой в тестах,
    /// в батч-сборке и в игре, а атлас TMP в этих трёх местах разный. Средняя
    /// ширина глифа кириллицы в LiberationSans ≈ 0,52 кегля — этого хватает,
    /// чтобы выбрать 1/2/3 строки; сам перенос делает TMP, и небольшая ошибка
    /// оценки съедается вертикальными полями пункта.</summary>
    public static class DropdownItemFit
    {
        /// <summary>Средняя ширина глифа в долях кегля.</summary>
        public const float GlyphWidthFactor = 0.52f;

        /// <summary>Межстрочный шаг в долях кегля.</summary>
        public const float LineHeightFactor = 1.2f;

        /// <summary>Вертикальные поля пункта (сверху + снизу). Подобраны так,
        /// чтобы однострочный пункт остался ровно <see cref="UIStyle.DropdownItemH"/>:
        /// список коротких названий выглядит как раньше.</summary>
        public const float PadV = 6f;

        /// <summary>Больше трёх строк пункт не растёт: список из таких пунктов
        /// перестаёт быть списком, а название всё равно читается по началу.</summary>
        public const int MaxLines = 3;

        /// <summary>Сколько строк займёт текст при переносе ПО СЛОВАМ в заданную
        /// ширину. Слово длиннее строки рвётся по символам — так же поступает TMP.
        /// Чистая функция.</summary>
        public static int LinesFor(string? text, float textWidth, int fontSize,
            int maxLines = MaxLines)
        {
            if (string.IsNullOrEmpty(text) || textWidth <= 0f || fontSize <= 0) return 1;

            int perLine = Mathf.Max(1, Mathf.FloorToInt(textWidth / (fontSize * GlyphWidthFactor)));
            int lines = 1;
            int used = 0;

            foreach (var word in text!.Split(' '))
            {
                int len = word.Length;
                if (len == 0) { used += 1; continue; } // двойной пробел

                // Слово не влезает в остаток строки — переносим его целиком.
                if (used > 0 && used + 1 + len > perLine)
                {
                    lines++;
                    used = 0;
                }
                else if (used > 0)
                {
                    used += 1; // пробел перед словом
                }

                // Слово длиннее целой строки — рвётся по символам.
                while (used + len > perLine)
                {
                    len -= perLine - used;
                    lines++;
                    used = 0;
                }
                used += len;
            }

            return Mathf.Clamp(lines, 1, Mathf.Max(1, maxLines));
        }

        /// <summary>Ширина текста, при которой самое длинное название влезает в
        /// ОДНУ строку. Список — попап, он не обязан быть шириной со свёрнутый
        /// контрол: расширить список дешевле, чем сделать двухстрочными все
        /// пункты подряд (высота у пункта одна на весь список). Чистая функция.</summary>
        public static float WidthFor(IEnumerable<string>? options, int fontSize)
        {
            int longest = 0;
            if (options != null)
                foreach (var o in options)
                    if (!string.IsNullOrEmpty(o)) longest = Mathf.Max(longest, o!.Length);
            // Запас в два глифа: ширина считается по средней букве, а у названия
            // из широких («Ш», «Ж») реальная строка чуть длиннее оценки. Без
            // запаса такое название переносится, а вторая строка в однострочном
            // пункте просто обрезается.
            return longest > 0 ? Mathf.Ceil((longest + 2) * fontSize * GlyphWidthFactor) : 0f;
        }

        /// <summary>Высота пункта, в который помещается самое длинное название.
        /// Чистая функция.</summary>
        public static float HeightFor(IEnumerable<string>? options, float textWidth,
            int fontSize, int maxLines = MaxLines)
        {
            int lines = 1;
            if (options != null)
                foreach (var o in options)
                    lines = Mathf.Max(lines, LinesFor(o, textWidth, fontSize, maxLines));

            float h = lines * fontSize * LineHeightFactor + PadV;
            return Mathf.Max(UIStyle.DropdownItemH, Mathf.Ceil(h));
        }
    }
}

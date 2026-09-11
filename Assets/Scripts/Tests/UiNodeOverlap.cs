using System;
using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Tests
{
    /// <summary>Недостающий сенсор золотых UI-снимков: «никакие два узла не стоят
    /// в одной точке».
    ///
    /// Снимок сличается БЕЗ координат — UiSnapshotEngine.StripPositions вырезает
    /// строку "position" перед сравнением, потому что вёрстка дрейфует на
    /// пиксель-другой при любой правке шрифта или отступа, и сравнение координат
    /// напрямую делало бы эталоны одноразовыми. Плата за это была ровно одна и
    /// дорогая: пятнадцать значков подсказки «i», свалившихся в одну точку, не
    /// поймал НИ ОДИН тест — поймал глаз на PNG. Набор узлов, их тексты и
    /// состояния при этом не менялись, так что сличению нечего было показать.
    ///
    /// Инвариант выбран так, чтобы дрейф его не тревожил: сравниваются не сами
    /// координаты, а только СОВПАДЕНИЕ двух узлов с точностью до пары пикселей.
    /// Сдвиг всей панели — хоть на пиксель, хоть на треть экрана — не значит
    /// ничего, а «всё свалилось в угол» видно сразу и с именами.
    ///
    /// Точка узла — ЦЕНТР его прямоугольника в общем для холста пространстве, а
    /// не anchoredPosition из тела снимка: anchoredPosition локальна, и у восьми
    /// переключателей одной вкладки она законно одинакова — каждый сидит в своей
    /// строке. Сенсор, построенный на локальной координате, краснел бы на
    /// здоровой панели и был бы отключён в тот же день.</summary>
    public static class UiNodeOverlap
    {
        /// <summary>Два узла считаются стоящими в одной точке, когда их центры
        /// расходятся меньше чем на столько пикселей по КАЖДОЙ оси.</summary>
        public const float CellPixels = 2f;

        public readonly struct Placed
        {
            public readonly string Id;
            public readonly float CentreX;
            public readonly float CentreY;
            public readonly float Width;
            public readonly float Height;

            public Placed(string id, float centreX, float centreY, float width, float height)
            {
                Id = id ?? "";
                CentreX = centreX;
                CentreY = centreY;
                Width = width;
                Height = height;
            }
        }

        /// <summary>Узел без площади не рисуется, а значит ничего собой не
        /// закрывает и ни от кого не прячется: свёрнутая строка, ещё не
        /// раскрытая группа, распорка нулевого размера. Это ЕДИНСТВЕННОЕ
        /// исключение — список имён-исключений здесь заводить нельзя, он
        /// протухает ровно так же, как протух бы список проверяемых панелей.</summary>
        public static bool Draws(Placed node) => node.Width > 0f && node.Height > 0f;

        /// <summary>Сравнение ПОПАРНОЕ, а не по клеткам фиксированной сетки, и
        /// это не деталь реализации. Сетка привязана к началу координат: два
        /// узла в полутора пикселях друг от друга попадают то в одну клетку, то
        /// в разные — в зависимости от того, где прошла граница. Тогда сдвиг
        /// всей панели на пиксель менял бы вердикт, а это ровно тот дрейф,
        /// ради невосприимчивости к которому координаты и перестали сличать.
        /// Расстояние между центрами при сдвиге не меняется вовсе.</summary>
        private static bool SamePoint(Placed a, Placed b) =>
            Math.Abs(a.CentreX - b.CentreX) < CellPixels
            && Math.Abs(a.CentreY - b.CentreY) < CellPixels;

        private static string Where(Placed node) =>
            "@" + node.CentreX.ToString("F0", CultureInfo.InvariantCulture)
            + "," + node.CentreY.ToString("F0", CultureInfo.InvariantCulture);

        /// <summary>Группы узлов, стоящих в одной точке. Порядок и внутри
        /// группы, и между группами — ординальный, чтобы сообщение об ошибке не
        /// зависело от порядка обхода сцены.</summary>
        public static List<string> Collisions(IEnumerable<Placed> nodes)
        {
            var drawn = new List<Placed>();
            foreach (var node in nodes)
                if (Draws(node)) drawn.Add(node);

            drawn.Sort((a, b) =>
            {
                int c = a.CentreY.CompareTo(b.CentreY);
                if (c != 0) return c;
                c = a.CentreX.CompareTo(b.CentreX);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });

            var taken = new bool[drawn.Count];
            var groups = new List<string>();

            for (int i = 0; i < drawn.Count; i++)
            {
                if (taken[i]) continue;
                var ids = new List<string> { drawn[i].Id };
                for (int j = i + 1; j < drawn.Count; j++)
                {
                    if (taken[j] || !SamePoint(drawn[i], drawn[j])) continue;
                    taken[j] = true;
                    ids.Add(drawn[j].Id);
                }

                if (ids.Count < 2) continue;
                ids.Sort(StringComparer.Ordinal);
                groups.Add(Where(drawn[i]) + " ×" + ids.Count + ": " + string.Join(", ", ids));
            }

            groups.Sort(StringComparer.Ordinal);
            return groups;
        }

        /// <summary>Пустая строка — инвариант держится. Иначе текст, который
        /// целиком отвечает на вопрос «что и куда свалилось», без второго
        /// прогона.</summary>
        public static string Report(string snapshotName, IEnumerable<Placed> nodes)
        {
            var groups = Collisions(nodes);
            if (groups.Count == 0) return "";

            return "[UISNAPSHOT] Узлы в одной точке: " + snapshotName + "\n"
                + "  Сличение снимка идёт БЕЗ координат, поэтому «всё свалилось в одну точку» "
                + "проходит мимо эталона — держит только этот инвариант.\n"
                + "  Допуск " + CellPixels.ToString(CultureInfo.InvariantCulture)
                + " px по каждой оси, координата — центр узла.\n  " + string.Join("\n  ", groups);
        }
    }
}

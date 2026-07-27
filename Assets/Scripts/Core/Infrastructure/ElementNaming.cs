using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Единая политика имён элементов: допустимый алфавит и глобальная
    /// уникальность в пределах проекта.
    ///
    /// Имя разрешено только из латиницы, цифр, дефиса и подчёркивания:
    ///     ^[A-Za-z0-9_-]+$
    ///
    /// Имя — это ещё и КЛЮЧ СВЯЗЕЙ (DrawerElement.PairedDrawerName /
    /// AttachedFacadeName, WindowElement/DoorElement.AttachedWallName), поэтому
    /// нормализация никогда не делается «молча в сеттере» PartName: тот, кто
    /// переименовывает, обязан либо провести правку через DrawerLinks.Rename,
    /// либо (при загрузке) перенести ссылки по карте старое→новое имя.
    /// </summary>
    public static class ElementNaming
    {
        /// <summary>Регулярка допустимого имени — для сообщений об ошибках и схемы MCP.</summary>
        public const string Pattern = "^[A-Za-z0-9_-]+$";

        /// <summary>Человекочитаемое правило — идёт в текст ошибки MCP.</summary>
        public const string Rule =
            "only latin letters, digits, '-' and '_' are allowed (regex: " + Pattern + "); no spaces, no cyrillic";

        /// <summary>Имя для элемента, у которого после чистки не осталось ни одного
        /// допустимого символа (например, имя из одних пробелов).</summary>
        public const string Fallback = "Element";

        // Имена сравниваются БЕЗ учёта регистра: «Facade» и «facade» на глаз
        // неразличимы, а MCP уже ищет элементы OrdinalIgnoreCase — коллизия
        // регистров приводила бы к тому, что инструмент находит не тот элемент.
        private static readonly System.StringComparer Cmp = System.StringComparer.OrdinalIgnoreCase;

        public static bool IsValid(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (char c in name!)
                if (!IsAllowed(c)) return false;
            return true;
        }

        private static bool IsAllowed(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9') || c == '_' || c == '-';

        /// <summary>
        /// Привести имя к допустимому алфавиту. Кириллица ТРАНСЛИТЕРИРУЕТСЯ
        /// («Фасад_600x400» → «Fasad_600x400»), а не выбрасывается: в проектах
        /// имена русские и осмысленные, а глухая замена на «_» схлопнула бы их
        /// в неразличимые «______600x400». Всё остальное недопустимое (пробелы,
        /// скобки, точки…) становится «_», повторы «_» схлопываются.
        /// </summary>
        public static string Sanitize(string? name)
        {
            if (string.IsNullOrEmpty(name)) return Fallback;

            var sb = new StringBuilder(name!.Length + 8);
            foreach (char c in name)
            {
                if (IsAllowed(c)) { sb.Append(c); continue; }

                var latin = Transliterate(c);
                if (latin.Length > 0) { sb.Append(latin); continue; }

                // Мягкий/твёрдый знак транслитерируются в пустоту — «_» за них
                // не ставим, иначе «Дверь» дала бы «Dver_».
                if (IsDroppedCyrillic(c)) continue;

                if (sb.Length > 0 && sb[sb.Length - 1] == '_') continue; // схлопываем повторы
                sb.Append('_');
            }

            // Ведущие/замыкающие «_» — мусор от отброшенных символов по краям.
            var result = sb.ToString().Trim('_');
            return result.Length == 0 ? Fallback : result;
        }

        /// <summary>
        /// Свободное имя: если <paramref name="desired"/> занято — добавляется
        /// суффикс «_1», «_2», … до первого свободного.
        /// </summary>
        /// <param name="except">Элемент, чьё собственное имя занятым не считается
        /// (переименование самого себя в тот же регистр).</param>
        /// <param name="reserved">Дополнительно занятые имена — используется при
        /// пакетной загрузке, когда элементы ещё не созданы и в реестре их нет.</param>
        public static string MakeUnique(string desired, KitchenElement? except = null,
            ICollection<string>? reserved = null)
        {
            if (string.IsNullOrEmpty(desired)) desired = Fallback;
            if (!IsTaken(desired, except, reserved)) return desired;

            var (baseName, startNum) = TryExtractTrailingNumber(desired);
            if (baseName != null)
            {
                for (int i = startNum + 1; ; i++)
                {
                    var candidate = baseName + "_" + i;
                    if (!IsTaken(candidate, except, reserved)) return candidate;
                }
            }

            for (int i = 1; ; i++)
            {
                var candidate = desired + "_" + i;
                if (!IsTaken(candidate, except, reserved)) return candidate;
            }
        }

        private static (string? baseName, int num) TryExtractTrailingNumber(string name)
        {
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore <= 0 || lastUnderscore >= name.Length - 1)
                return (null, 0);

            string suffix = name.Substring(lastUnderscore + 1);
            if (suffix.Length == 0 || suffix[0] == '0')
                return (null, 0);

            foreach (char c in suffix)
                if (c < '0' || c > '9')
                    return (null, 0);

            if (!int.TryParse(suffix, out int num) || num < 1)
                return (null, 0);

            return (name.Substring(0, lastUnderscore), num);
        }

        /// <summary>Чистка + уникальность одним вызовом — основная точка входа.</summary>
        public static string Normalize(string? desired, KitchenElement? except = null,
            ICollection<string>? reserved = null) =>
            MakeUnique(Sanitize(desired), except, reserved);

        private static bool IsTaken(string name, KitchenElement? except, ICollection<string>? reserved)
        {
            if (reserved != null)
                foreach (var r in reserved)
                    if (Cmp.Equals(r, name)) return true;

            var all = PartRegistry.All;
            if (all == null) return false;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e == except) continue;
                if (Cmp.Equals(e.PartName, name)) return true;
            }
            return false;
        }

        /// <summary>Набор имён для пакетной нормализации, посеянный текущей сценой.</summary>
        public static HashSet<string> ReservedFromScene()
        {
            var set = new HashSet<string>(Cmp);
            var all = PartRegistry.All;
            if (all == null) return set;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !string.IsNullOrEmpty(all[i].PartName))
                    set.Add(all[i].PartName);
            return set;
        }

        private static bool IsDroppedCyrillic(char c) => c == 'ь' || c == 'ъ' || c == 'Ь' || c == 'Ъ';

        /// <summary>Транслитерация одного символа; пустая строка — символ не кириллица
        /// (или это ь/ъ, которые исчезают).</summary>
        private static string Transliterate(char c)
        {
            switch (c)
            {
                case 'а': return "a";  case 'А': return "A";
                case 'б': return "b";  case 'Б': return "B";
                case 'в': return "v";  case 'В': return "V";
                case 'г': return "g";  case 'Г': return "G";
                case 'д': return "d";  case 'Д': return "D";
                case 'е': return "e";  case 'Е': return "E";
                case 'ё': return "e";  case 'Ё': return "E";
                case 'ж': return "zh"; case 'Ж': return "Zh";
                case 'з': return "z";  case 'З': return "Z";
                case 'и': return "i";  case 'И': return "I";
                case 'й': return "y";  case 'Й': return "Y";
                case 'к': return "k";  case 'К': return "K";
                case 'л': return "l";  case 'Л': return "L";
                case 'м': return "m";  case 'М': return "M";
                case 'н': return "n";  case 'Н': return "N";
                case 'о': return "o";  case 'О': return "O";
                case 'п': return "p";  case 'П': return "P";
                case 'р': return "r";  case 'Р': return "R";
                case 'с': return "s";  case 'С': return "S";
                case 'т': return "t";  case 'Т': return "T";
                case 'у': return "u";  case 'У': return "U";
                case 'ф': return "f";  case 'Ф': return "F";
                case 'х': return "h";  case 'Х': return "H";
                case 'ц': return "ts"; case 'Ц': return "Ts";
                case 'ч': return "ch"; case 'Ч': return "Ch";
                case 'ш': return "sh"; case 'Ш': return "Sh";
                case 'щ': return "sch";case 'Щ': return "Sch";
                case 'ы': return "y";  case 'Ы': return "Y";
                case 'э': return "e";  case 'Э': return "E";
                case 'ю': return "yu"; case 'Ю': return "Yu";
                case 'я': return "ya"; case 'Я': return "Ya";
                default: return "";
            }
        }
    }
}

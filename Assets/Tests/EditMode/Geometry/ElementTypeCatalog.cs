using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Список типов элементов, выведенный ИЗ ИСХОДНИКОВ: всякий класс,
    /// чья цепочка базовых доходит до KitchenElement. Сама база в список не
    /// входит — «доска» это она и есть.
    ///
    /// АБСТРАКТНЫЕ классы тоже не входят, хотя цепочка у них та же. Регистрируют
    /// не класс, а ТИП, который можно создать: фабрика, дубликатор, восстановление
    /// сцены и MCP работают с конкретным элементом. Промежуточная база вроде
    /// WallOpeningElement (дверь и окно) или PartCutoutElement (варочная и мойка)
    /// зарегистрирована быть не может — но в цепочке наследования остаётся, иначе
    /// её потомки перестанут доходить до KitchenElement и выпадут из сторожа
    /// молча. Отсюда два разных списка: baseOf знает про неё, ответ — нет.
    ///
    /// Живёт отдельным классом, а не внутри одного теста, потому что служит
    /// ТРЕТЬИМ ИСТОЧНИКОМ для парных сторожей. Два списка, сверяемые друг с
    /// другом, проходят проверку на согласованность и при СОГЛАСОВАННОМ
    /// НЕЗНАНИИ: мойка выпала из create_elements ровно так — контракт и реестр
    /// ElementSpawners одинаково её не знали, и тест их равенства был зелёным.
    /// Третий источник не участвует в этом сговоре: он читает объявления
    /// классов и не знает ни про контракт, ни про реестр.
    ///
    /// Класс читает только файлы и потому собирается вторым проходом под
    /// dotnet вместе со всем каталогом.</summary>
    public static class ElementTypeCatalog
    {
        public const string BaseType = "KitchenElement";

        public const string Suffix = "Element";

        private static readonly Regex ClassDeclaration =
            new Regex(@"\bclass\s+(\w+)\s*:\s*([A-Za-z_]\w*)", RegexOptions.Compiled);

        private static readonly Regex AbstractDeclaration =
            new Regex(@"\babstract\s+class\s+(\w+)\b", RegexOptions.Compiled);

        /// <summary>Имя типа без суффикса: по нему зовётся фабричный метод
        /// (CreateXxx) — инвариант, который соблюдают все типы проекта.</summary>
        public static string ShortNameOf(string type) =>
            type.EndsWith(Suffix, StringComparison.Ordinal)
                ? type.Substring(0, type.Length - Suffix.Length)
                : type;

        public static string[] ProductionSources() =>
            Directory.GetFiles(RepoPaths.Subdir("Assets", "Scripts"), "*.cs", SearchOption.AllDirectories);

        public static List<string> FromSources()
        {
            var baseOf = new Dictionary<string, string>(StringComparer.Ordinal);
            var abstractTypes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in ProductionSources())
                foreach (var line in SourceLines.CodeOnly(File.ReadAllLines(file)))
                {
                    var m = ClassDeclaration.Match(line);
                    if (m.Success) baseOf[m.Groups[1].Value] = m.Groups[2].Value;

                    var a = AbstractDeclaration.Match(line);
                    if (a.Success) abstractTypes.Add(a.Groups[1].Value);
                }

            var types = new List<string>();
            foreach (var name in baseOf.Keys)
                if (!abstractTypes.Contains(name) && DescendsFromBase(name, baseOf)) types.Add(name);

            types.Sort(StringComparer.Ordinal);
            return types;
        }

        private static bool DescendsFromBase(string name, Dictionary<string, string> baseOf)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { name };
            var current = name;

            while (baseOf.TryGetValue(current, out var parent))
            {
                if (string.Equals(parent, BaseType, StringComparison.Ordinal)) return true;
                if (!seen.Add(parent)) return false;
                current = parent;
            }

            return false;
        }
    }
}

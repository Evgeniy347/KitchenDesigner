using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    public class DropdownRefitTests
    {
        private const string Refit = "FitDropdownItems";

        private static readonly Regex OptionsChanged =
            new Regex(@"\.options\s*=|\bAddOptions\s*\(|\bClearOptions\s*\(");

        private static readonly (string file, string why)[] KnownWithoutRefit =
        {
            ("ElementTypeConverter.cs",
                "список типов элемента фиксирован и коротк: длиннее «Столешница» там ничего нет"),
            ("HierarchyPanelUI.cs",
                "список модулей для переноса: имена модулей задаёт пользователь, длинное имя "
                + "обрежется — долг, а не решение"),
            ("NameDropdownBinder.cs",
                "привязка по имени элемента: та же природа, что у HierarchyPanelUI — долг"),
        };

        private static string UiDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "UI");

        public static bool ChangesOptions(IEnumerable<string> lines) =>
            SourceLines.CodeOnly(lines).Any(line => OptionsChanged.IsMatch(line));

        private static List<string> FilesChangingOptionsWithoutRefit()
        {
            var offenders = new List<string>();

            foreach (var file in Directory.GetFiles(UiDir(), "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                if (!ChangesOptions(lines)) continue;
                if (SourceLines.CodeOnly(lines).Any(line => line.Contains(Refit))) continue;
                offenders.Add(Path.GetFileName(file));
            }

            return offenders;
        }

        [Test]
        public void EveryOptionsChange_IsFollowedByARefit()
        {
            var known = KnownWithoutRefit.Select(k => k.file).ToHashSet(StringComparer.Ordinal);
            var offenders = FilesChangingOptionsWithoutRefit();

            CollectionAssert.IsSubsetOf(offenders, known,
                "Раскладка раскрытого списка считается по САМОМУ ДЛИННОМУ названию, а набор "
                + "меняется в рантайме (декоры приезжают из внешней папки текстур). Меняешь "
                + "options — зови UIFactory.FitDropdownItems, иначе список останется по ширине "
                + "предыдущего набора. Без записи в KnownWithoutRefit: "
                + string.Join(", ", offenders.Except(known)));
        }

        [Test]
        public void EveryKnownException_StillChangesOptions_AndStillSkipsTheRefit()
        {
            var offenders = FilesChangingOptionsWithoutRefit();

            foreach (var (file, why) in KnownWithoutRefit)
                CollectionAssert.Contains(offenders, file,
                    "Запись пережила свой файл: " + file + " (" + why + ") больше не меняет "
                    + "options мимо рефита — убери её, иначе она молча освободит следующий файл");
        }

        [Test]
        public void TheScan_SeesTheFactoryThatDoesRefit()
        {
            var factory = Path.Combine(UiDir(), "UIFactory.cs");

            Assert.IsTrue(File.Exists(factory), "скан смотрит не туда — по пустому пути он зеленеет");
            Assert.IsTrue(ChangesOptions(File.ReadAllLines(factory)),
                "UIFactory назначает options при сборке списка");
            CollectionAssert.DoesNotContain(FilesChangingOptionsWithoutRefit(), "UIFactory.cs",
                "и тут же зовёт FitDropdownItems — сканер обязан это видеть");
        }

        [Test]
        public void TheCounter_IgnoresCommentsAndStrings()
        {
            Assert.IsTrue(ChangesOptions(new[] { "dropdown.options = opts;" }), "присваивание");
            Assert.IsTrue(ChangesOptions(new[] { "_dropdown.AddOptions(labels);" }), "AddOptions");
            Assert.IsFalse(ChangesOptions(new[] { "// dropdown.options = opts;" }),
                "закомментированный код не считается");
            Assert.IsFalse(ChangesOptions(new[] { "var hint = \"dropdown.options = opts\";" }),
                "текст подсказки внутри строки — не код");
        }
    }
}

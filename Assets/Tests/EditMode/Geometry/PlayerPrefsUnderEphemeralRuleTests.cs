using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож правила «прогон на машине пользователя изолируется по ЗАПИСИ»
    /// (<c>agents/TESTS.md</c>). Дымовая проверка запускает настоящий плеер на рабочей
    /// машине, поэтому каждый ключ <c>PlayerPrefs</c>, который она трогает, — это
    /// настройка пользователя, стёртая молча: сначала «последний проект» и «первый
    /// запуск», потом три ключа сайдбара, из-за которых панель переезжала.
    ///
    /// Подчистка после прогона лечением не считается — прогон может упасть посередине.
    /// Лечение одно: под <c>-ephemeralSession</c> не писать вовсе, и решение об этом
    /// принимается в ОДНОМ месте (<c>conventions/CORRECTNESS.md</c> → «An effect that
    /// leaves the process gets ONE adapter»). Отсюда и форма правила: <c>PlayerPrefs</c>
    /// упоминает ровно один файл продакшена, и этот файл спрашивает
    /// <c>EphemeralSessionArgument</c>. Следующий ключ, заведённый мимо адаптера, красит
    /// этот тест и называет файл и сам ключ.</summary>
    public class PlayerPrefsUnderEphemeralRuleTests
    {
        /// <summary>Единственный файл продакшена, которому дозволено трогать
        /// <c>PlayerPrefs</c>: в нём и живёт выбор «писать в реестр или только в память».</summary>
        private const string TheOnlyAdapter = "PreferenceStore.cs";

        /// <summary>Держатели ключей. Список — не определение правила, а его вторая
        /// половина: правило запрещает сырой <c>PlayerPrefs</c> всем, а здесь перечислено,
        /// где ключи ТОЧНО есть, — чтобы файл не мог тихо отвязаться от адаптера.</summary>
        private static readonly (string path, string why)[] KeyHolders =
        {
            ("Persistence/LastProjectMemory.cs", "KitchenLastSavePath — последний открытый проект пользователя"),
            ("Persistence/FirstRunMarker.cs", "KitchenFirstRunDone — показывать ли демо-проект"),
            ("UI/SidebarDockPreference.cs", "KitchenSidebarDockChoice — раскрытый док или рейка иконок"),
            ("UI/SidebarLastGroupPreference.cs", "KitchenSidebarLastGroup — какая группа каталога открыта"),
            ("UI/SidebarPresetPreference.cs", "KitchenSidebarPreset_* — выбранный пресет плитки"),
        };

        private static readonly Regex PrefsCall =
            new Regex(@"PlayerPrefs\.\w+\(\s*([^,)]+)");

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

        private static IEnumerable<string> ProductionFiles() =>
            SourceCorpus.Files(ScriptsDir())
                .Concat(Directory.GetFiles(RepoPaths.Subdir("Assets", "Editor"), "*.cs",
                    SearchOption.AllDirectories));

        [Test]
        public void PlayerPrefs_IsTouchedByExactlyOneProductionFile()
        {
            var offenders = new List<string>();
            foreach (var file in ProductionFiles())
            {
                if (Path.GetFileName(file) == TheOnlyAdapter) continue;
                var keys = PrefsCall.Matches(SourceCorpus.Text(file))
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Distinct()
                    .ToArray();
                if (keys.Length == 0) continue;
                offenders.Add(Path.GetFileName(file) + " (" + string.Join(", ", keys) + ")");
            }

            Assert.IsEmpty(offenders,
                "новый ключ PlayerPrefs заведён мимо " + TheOnlyAdapter + ", то есть мимо "
                + "-ephemeralSession: дымовой прогон на машине пользователя сотрёт эту "
                + "настройку молча, и подчистка после прогона тут не лечит — прогон может "
                + "упасть посередине. Нарушители: " + string.Join("; ", offenders));
        }

        [Test]
        public void TheOnlyAdapter_AsksWhetherTheRunIsEphemeral()
        {
            var adapter = Path.Combine(ScriptsDir(), "Core", "Persistence", TheOnlyAdapter);
            Assert.IsTrue(File.Exists(adapter), "адаптер переехал: " + TheOnlyAdapter);

            var text = SourceCorpus.Text(adapter);
            StringAssert.Contains("PlayerPrefs", text,
                "сканируемое множество обязано включать сам адаптер — иначе тест выше "
                + "зелен на пустом месте");
            StringAssert.Contains("EphemeralSessionArgument", text,
                "единственное место, где спрашивают «это прогон?», обязано спрашивать");
        }

        [Test]
        public void EveryKeyHolder_GoesThroughThePreferenceStore()
        {
            foreach (var (path, why) in KeyHolders)
            {
                var full = Path.Combine(ScriptsDir(), "Core",
                    path.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(full), $"держатель ключа переехал: {path}");

                var text = SourceCorpus.Text(full);
                StringAssert.Contains("PreferenceStore", text,
                    $"{path}: {why} — ключ обязан ходить через PreferenceStore");
                StringAssert.DoesNotContain("PlayerPrefs", text,
                    $"{path}: {why} — сырой PlayerPrefs мимо адаптера");
            }
        }

        [Test]
        public void TheScan_ActuallyReadsTheProductionLayer()
        {
            var files = ProductionFiles().ToArray();
            Assert.Greater(files.Length, 100,
                "скан по несуществующему пути зелен и не проверяет ничего");
            CollectionAssert.Contains(files.Select(f => Path.GetFileName(f)!).ToArray(), TheOnlyAdapter,
                "файл с единственным легальным PlayerPrefs обязан попадать в скан");
        }
    }
}

using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>«Прозрачность материала настраивается в одном месте»
    /// (`docs/UI-GUIDELINES.md` §9). «Сделать материал прозрачным» было написано
    /// четыре раза: `HighlightOverlay.MakeSeeThrough` и `ElementHighlighter` — полностью,
    /// `ScenePreview.Tint` и `ElementMover` — неполно, без `_SrcBlend`/`_DstBlend`/`_ZWrite`,
    /// отчего призрак предпросмотра оставался непрозрачным и полностью закрывал то, что
    /// заменял. Свести четыре копии в одну мало — нужен сторож, иначе пятая копия появится
    /// в первом же новом призраке.
    ///
    /// Список файлов здесь не написан руками: он собирается сканом каталога
    /// `Assets/Scripts`, а не двумя именами, которые сторож ловил бы вслепую при третьем
    /// потребителе (`agents/TEST-DESIGN.md`). Сторож обязан доказать, что нашёл файлы —
    /// пустой список зеленеет на любом коде.</summary>
    public class TransparencySingleSourceRuleTests
    {
        private const string CanonicalFileName = "TransparentMaterial.cs";

        private static readonly string[] TransparencyTokens =
        {
            "_Surface",
            "_SrcBlend",
            "_DstBlend",
            "_ZWrite",
            "_SURFACE_TYPE_TRANSPARENT",
        };

        private static string ScriptsRoot => RepoPaths.Subdir("Assets", "Scripts");

        [Test]
        public void NoFileOtherThanTheCanonicalOne_ConfiguresMaterialTransparency()
        {
            var files = SourceCorpus.Files(ScriptsRoot)
                .Where(f => Path.GetFileName(f) != CanonicalFileName)
                .ToList();

            Assert.Greater(files.Count, 0,
                "сканер обязан найти файлы под Assets/Scripts — иначе он ничего не стережёт");

            foreach (var file in files)
            {
                var source = SourceCorpus.Text(file);
                foreach (var token in TransparencyTokens)
                    Assert.IsFalse(source.Contains(token),
                        $"{Path.GetFileName(file)} пишет {token} напрямую — "
                        + $"прозрачность материала настраивается только в {CanonicalFileName} "
                        + "(TransparentMaterial.Make/Apply), а этот файл зовёт его, а не "
                        + "повторяет рецепт");
            }
        }

        [Test]
        public void CanonicalFile_ExistsAndCarriesTheFullRecipe()
        {
            var matches = Directory.GetFiles(ScriptsRoot, CanonicalFileName, SearchOption.AllDirectories);
            Assert.AreEqual(1, matches.Length,
                $"{CanonicalFileName} обязан существовать ровно в одном экземпляре — "
                + "иначе сторожить нечего, либо непонятно, что именно");

            var source = File.ReadAllText(matches[0]);
            foreach (var token in TransparencyTokens)
                StringAssert.Contains(token, source,
                    $"{CanonicalFileName} не содержит {token}"
                    + " — рецепт неполон, и хотя бы один из четырёх бывших вызывающих "
                    + "снова останется непрозрачным");
        }
    }
}

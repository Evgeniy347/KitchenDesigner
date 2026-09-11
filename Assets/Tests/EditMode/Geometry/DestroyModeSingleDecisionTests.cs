using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож единственного места, где решают «уничтожить в play mode или вне
    /// его». Цена этой копии уже оплачена: <c>ElementMover</c> звал <c>Destroy</c> напрямую
    /// и падал вне play mode, поэтому перетаскивание, которое что-нибудь подкрасило,
    /// ВООБЩЕ нельзя было проверить тестом — покадровую цену драга не стерёг никто именно
    /// поэтому (коммит 6dae5488). Ответ один на всех: <c>DestroyNow.The</c>.
    ///
    /// Признак копии — <c>DestroyImmediate</c> рядом с вопросом <c>Application.isPlaying</c>.
    /// Список ниже не разрешение, а ДОЛГ: он умеет только уменьшаться. Появился новый файл
    /// с копией — тест красный; свели файл к <c>DestroyNow.The</c>, а строку не убрали —
    /// тоже красный, иначе запись переживёт свой файл и начнёт молча прощать следующий.</summary>
    public class DestroyModeSingleDecisionTests
    {
        /// <summary>Единственный файл, которому положено спрашивать про play mode: в нём
        /// и живёт ответ.</summary>
        private const string TheOnlyDecision = "DestroyNow.cs";

        /// <summary>Файлы, где копия ещё осталась, с тем, что каждая уничтожает. Все они
        /// вне границ задачи P1 №2, которая свела Core/Rendering; каждая строка уходит
        /// вместе со своей копией.</summary>
        private static readonly (string file, string what)[] CopiesStillOwed =
        {
            ("ApplianceBoxes.cs", "дочерние объекты коробов техники"),
            ("ChairBackrest.cs", "панель спинки"),
            ("CooktopMesh.cs", "дочерние объекты варочной"),
            ("DoorElement.cs", "дочерние объекты двери"),
            ("ElementFactoryInstance.cs", "объект элемента при откате создания"),
            ("FloorElement.cs", "меш контура пола"),
            ("FurniturePartSet.cs", "деталь мебели"),
            ("LaundryMachineElement.cs", "меш корпуса машины"),
            ("LegSet.cs", "ножки"),
            ("LightSourceElement.cs", "материал плафона"),
            ("MeshAccumulator.cs", "накопленный меш"),
            ("SinkMesh.cs", "дочерние объекты мойки"),
            ("TableElement.cs", "столешницу"),
            ("Wall.cs", "меш стены с проёмами"),
            ("WallOpeningElement.cs", "группу проёма"),
            ("WindowElement.cs", "дочерние объекты окна"),
            ("TextureLibrary.cs", "загруженную текстуру"),
            ("McpCommandHandler.PlanGeometry.cs", "пробный объект замера"),
            ("SceneElements.cs", "объекты сцены при очистке"),
            ("TextureOverlayHandles.cs", "ручки накладки текстуры"),
        };

        /// <summary>Кто уже сведён — вторая половина правила: молчаливая потеря вызова
        /// означала бы, что файл отвязался от общего решения.</summary>
        private static readonly string[] AlreadyReduced =
        {
            "Elements/ElementMover.cs",
            "Rendering/ElementOutline.cs",
            "Rendering/HoverTint.cs",
            "Rendering/HighlightOverlay.cs",
            "Rendering/TextureOverlayRenderer.cs",
            "Rendering/CeilingBuilder.cs",
            "Rendering/ScenePreview.cs",
            "Rendering/PhotoQualityController.cs",
            "Diagnostics/PerfHud.cs",
            "Lighting/LightPickRenderer.cs",
            "Measure/MeasureRenderer.cs",
            "Rendering/EdgeOutlineRenderer.cs",
            "Rendering/SpatialGridRenderer.cs",
            "Snap/ResizeHandleManager.cs",
            "UI/HierarchyPanelUI.cs",
            "UI/MultiSelectDropdown.cs",
            "MCP/McpCommandHandler.Info.cs",
            "MCP/McpCommandHandler.Scene.cs",
        };

        /// <summary>ГОЛЫЙ вызов: <c>Destroy(x)</c>, <c>Object.Destroy(x)</c> или
        /// <c>UnityEngine.Object.Destroy(x)</c>. <c>DestroyImmediate</c>, <c>OnDestroy</c>
        /// и вызов метода с тем же именем у собственного объекта (<c>_legSet?.Destroy()</c>)
        /// сюда не попадают.</summary>
        private static readonly Regex TheBareCall = new Regex(
            @"(?<![\w.])Destroy\s*\(|(?<!\w)(?:UnityEngine\.)?Object\.Destroy\s*\(");

        /// <summary>Законные голые вызовы — с причиной у каждого. Сегодня список ПУСТ:
        /// при сведении P1 №2 ни одного случая, которому ветка «play mode или нет» была
        /// бы не нужна, не нашлось. Список оставлен, чтобы следующее исключение пришлось
        /// вносить сюда с причиной, а не расширять шаблон.</summary>
        private static readonly (string file, string why)[] BareCallsAllowed = { };

        private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

        private static string[] ProductionFiles() =>
            Directory.GetFiles(ScriptsDir(), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(RepoPaths.Subdir("Assets", "Editor"), "*.cs",
                    SearchOption.AllDirectories))
                .ToArray();

        private static bool CarriesTheCopy(string file)
        {
            var text = File.ReadAllText(file);
            return text.Contains("DestroyImmediate") && text.Contains("Application.isPlaying");
        }

        private static IEnumerable<string> BareCallsIn(string name, IReadOnlyList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                foreach (Match match in TheBareCall.Matches(line))
                {
                    if (match.Index >= 5 && line.Substring(match.Index - 5, 5) == "void ") continue;
                    yield return name + ":" + (i + 1) + "  " + line.Trim();
                }
            }
        }

        private static IEnumerable<string> BareCallsIn(string file) =>
            BareCallsIn(Path.GetFileName(file)!, File.ReadAllLines(file));

        [Test]
        public void NoNewCopy_OfTheDestroyModeDecision_Appears()
        {
            var owed = CopiesStillOwed.Select(c => c.file).ToArray();
            var offenders = ProductionFiles()
                .Where(f => Path.GetFileName(f) != TheOnlyDecision)
                .Where(CarriesTheCopy)
                .Select(f => Path.GetFileName(f)!)
                .Where(name => !owed.Contains(name))
                .ToArray();

            Assert.IsEmpty(offenders,
                "решение «в play mode или вне его» принимает только " + TheOnlyDecision
                + " (DestroyNow.The). Прямой Destroy падает вне play mode — и тогда то, что "
                + "его зовёт, перестаёт быть проверяемым тестом. Новые копии: "
                + string.Join(", ", offenders));
        }

        [Test]
        public void EveryOwedCopy_IsStillThere_OrItsLineGoesAway()
        {
            var carrying = ProductionFiles()
                .Where(CarriesTheCopy)
                .Select(f => Path.GetFileName(f)!)
                .ToArray();

            var stale = CopiesStillOwed
                .Where(c => !carrying.Contains(c.file))
                .Select(c => c.file + " (уничтожал " + c.what + ")")
                .ToArray();

            Assert.IsEmpty(stale,
                "копия сведена — убери строку из списка долга тем же изменением, иначе "
                + "запись переживёт свой файл и начнёт прощать следующий: "
                + string.Join(", ", stale));
        }

        [Test]
        public void EveryReducedFile_AsksThroughDestroyNow()
        {
            foreach (var path in AlreadyReduced)
            {
                var full = Path.Combine(ScriptsDir(), "Core",
                    path.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(full), $"файл переехал: {path}");

                var text = File.ReadAllText(full);
                StringAssert.Contains("DestroyNow.The", text,
                    $"{path}: уничтожение обязано идти через общее решение");
                Assert.IsFalse(text.Contains("DestroyImmediate"),
                    $"{path}: копия идиома вернулась");
            }
        }

        [Test]
        public void TheScan_ActuallyReadsTheProductionLayer()
        {
            var files = ProductionFiles();
            Assert.Greater(files.Length, 100,
                "скан по несуществующему пути зелен и не проверяет ничего");

            var decision = files.SingleOrDefault(f => Path.GetFileName(f) == TheOnlyDecision);
            Assert.IsNotNull(decision, $"{TheOnlyDecision} обязан попадать в скан");
            Assert.IsTrue(CarriesTheCopy(decision!),
                "признак копии обязан срабатывать хотя бы на образце — на самом "
                + TheOnlyDecision + ", иначе тест выше зелен на пустом месте");
        }

        [Test]
        public void NoBareDestroyCall_RemainsInProduction()
        {
            var allowed = BareCallsAllowed.Select(a => a.file).ToArray();
            var offenders = ProductionFiles()
                .Where(f => !CarriesTheCopy(f))
                .Where(f => !allowed.Contains(Path.GetFileName(f)!))
                .SelectMany(BareCallsIn)
                .ToArray();

            Assert.IsEmpty(offenders,
                "ПРАВИЛО: уничтожать в продакшене можно только через " + TheOnlyDecision
                + " (DestroyNow.The), и привести к нему свой вызов — часть того же "
                + "изменения, разрешения ни у кого спрашивать не надо. ЧЕМ ПЛАТИМ: "
                + "голый Destroy вне play mode бросает InvalidOperationException, поэтому "
                + "ВЕСЬ путь, на котором он стоит, перестаёт быть проверяемым в EditMode "
                + "— так покадровая цена перетаскивания не стерёглась ничем, а окно "
                + "«Сцена» и поповер фильтра проверялись только тяжёлым PlayMode. И даже "
                + "там отложенный Destroy снимает объект лишь в конце кадра, так что "
                + "перестроение списка успевает удвоиться. ЧТО ДЕЛАТЬ: заменить вызов на "
                + "DestroyNow.The(x) — он сам проверяет null и сам выбирает "
                + "Destroy/DestroyImmediate, новых using не нужно; если вызову ветка "
                + "ДЕЙСТВИТЕЛЬНО не нужна (уничтожение внутри самого решения или вызов "
                + "своего метода с тем же именем) — внести файл в BareCallsAllowed с "
                + "причиной и доложить. Голые вызовы: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryAllowedBareCall_StillExists_AndStillNeedsTheException()
        {
            foreach (var allowed in BareCallsAllowed)
            {
                var file = ProductionFiles()
                    .FirstOrDefault(f => Path.GetFileName(f) == allowed.file);
                Assert.IsNotNull(file,
                    $"{allowed.file} ({allowed.why}): файл исчез, а разрешение осталось — "
                    + "оно начнёт молча прощать следующий файл с тем же именем");
                Assert.IsNotEmpty(BareCallsIn(file!).ToArray(),
                    $"{allowed.file} ({allowed.why}): голого вызова больше нет — убери "
                    + "строку разрешения тем же изменением");
            }
        }

        [Test]
        public void TheBareCallScan_TellsACallFromADeclarationAndFromANeighboursMethod()
        {
            var sample = new[]
            {
                "            Destroy(_background);",
                "            Object.Destroy(tex);",
                "            UnityEngine.Object.Destroy(go);",
                "            DestroyNow.The(_popupOverlay);",
                "            _legSet?.Destroy();",
                "            private void OnDestroy()",
                "            private static void Destroy(Transform node)",
            };

            var found = BareCallsIn("Sample.cs", sample).ToArray();

            Assert.AreEqual(3, found.Length,
                "шаблон обязан ловить все три написания голого вызова и НИ ОДНОГО из "
                + "соседних: правило, отобранное по одному написанию, обходится сменой "
                + "стиля — а лишнее срабатывание на OnDestroy, на объявлении метода или "
                + "на Destroy() своего же объекта превратило бы стража в помеху. "
                + "Найдено: " + string.Join(" | ", found));
            Assert.IsTrue(found[0].Contains("_background"),
                "первая находка — вызов без квалификатора, унаследованный от компонента");
            Assert.IsTrue(found[1].Contains("tex"),
                "вторая — тот же вызов через короткое имя типа");
            Assert.IsTrue(found[2].Contains("UnityEngine"),
                "третья — он же полным именем: правило, не покрывающее полное имя, "
                + "обходится одной строкой и молчит");
        }

        [Test]
        public void TheBareCallScan_OnTheDecisionItself_SeesTheCallAndNotItsImmediateTwin()
        {
            var decision = ProductionFiles()
                .Single(f => Path.GetFileName(f) == TheOnlyDecision);

            var found = BareCallsIn(decision).ToArray();

            Assert.AreEqual(1, found.Length,
                "скан обязан читать НАСТОЯЩИЙ исходник, а не только синтетику: в самом "
                + TheOnlyDecision + " обе ветки стоят рядом, и находка обязана быть "
                + "ровно одна — та, что уничтожает в play mode. Ноль означал бы сломанный "
                + "шаблон, на котором NoBareDestroyCall_RemainsInProduction зеленеет "
                + "вхолостую; два — что шаблон считает нарушением и вторую ветку, и тогда "
                + "он покраснеет на каждом сведённом файле. Найдено: "
                + string.Join(" | ", found));
        }
    }
}

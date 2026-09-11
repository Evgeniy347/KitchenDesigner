using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        };

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
    }
}

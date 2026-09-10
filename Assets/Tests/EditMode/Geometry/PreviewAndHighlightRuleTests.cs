using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Два правила из `docs/UI-GUIDELINES.md` § 9, которые нельзя проверить
    /// вызовом: они про то, чего в коде БЫТЬ не должно.
    ///
    /// Первое — «превью это состояние вида, а не проекта». Призрак живёт внутри
    /// `ElementFactorySandbox`, поэтому не попадает в реестр, а с ним — ни в
    /// автосохранение, ни в валидацию, ни в отмену. Одна строка `CommandStack
    /// .Execute` в `ScenePreview` превратила бы наведение мышью в отменяемую
    /// операцию, а сохранённый файл — в проект с трубой, которую никто не выбирал;
    /// поймать это тестом на поведение можно только целой сценой.
    ///
    /// Второе — «накладка в сцене одна». Красную геометрию строит, хранит и гасит
    /// `HighlightOverlay`; потребители решают ТОЛЬКО какой участок красить. Свой
    /// `new GameObject` в потребителе — это второй набор квадов со своим временем
    /// жизни, который чужой `Hide()` не уберёт.</summary>
    public class PreviewAndHighlightRuleTests
    {
        private static string Source(string fileName) =>
            File.ReadAllText(Path.Combine(
                RepoPaths.Subdir("Assets", "Scripts", "Core", "Rendering"), fileName));

        [Test]
        public void ScenePreview_SpawnsTheGhost_InsideTheFactorySandbox()
        {
            StringAssert.Contains("ElementFactorySandbox.Enter()", Source("ScenePreview.cs"),
                "призрак, рождённый вне песочницы, регистрируется в PartRegistry — а оттуда "
                + "он виден автосохранению, валидации и спецификации как настоящий элемент");
        }

        [Test]
        public void ScenePreview_NeverReachesIntoTheProject()
        {
            var source = Source("ScenePreview.cs");
            foreach (var forbidden in new[]
            {
                "CommandStack", "SaveLoadManager", "PartRegistry", "ConstraintValidator",
                "SpecificationManager", "AutoSave",
            })
                StringAssert.DoesNotContain(forbidden, source,
                    "превью — состояние ВИДА: обращение к " + forbidden + " делает наведение "
                    + "мышью правкой проекта, а её никто не просил");
        }

        [Test]
        public void ScenePreview_TakesItsGreen_FromThePalette()
        {
            var source = Source("ScenePreview.cs");

            StringAssert.Contains("UIStyle.PreviewGhost", source,
                "зелёный призрака обязан значить одно и то же во всех превью — "
                + "значит, он живёт в UIStyle, а не по месту");
            StringAssert.DoesNotContain("new Color(", source,
                "цветовой литерал мимо палитры — ровно то, что ловит сторож цветов");
        }

        [Test]
        public void HighlightConsumers_BuildNoSceneObjectsOfTheirOwn()
        {
            foreach (var consumer in new[] { "SideHighlighter.cs", "PartHighlighter.cs" })
            {
                var source = Source(consumer);
                StringAssert.DoesNotContain("new GameObject(", source,
                    "потребитель " + consumer + " решает, КАКОЙ участок красить, "
                    + "а рисует один HighlightOverlay. Свой объект в сцене переживёт "
                    + "чужой Hide() и останется красным навсегда");
                StringAssert.DoesNotContain("new Material(", source,
                    "потребитель " + consumer + " не заводит своего материала: краска у накладки одна");
            }
        }

        [Test]
        public void HighlightOverlay_IsTheOnlyOne_ThatKnowsThePalette()
        {
            StringAssert.Contains("UIStyle.EdgeHighlight3D", Source("HighlightOverlay.cs"),
                "красный накладки живёт в одном месте — иначе два потребителя разойдутся "
                + "в оттенке, и один участок станет краснее другого");
            StringAssert.DoesNotContain("UIStyle", Source("SideHighlighter.cs"),
                "потребитель не знает про палитру: за цвет отвечает накладка");
            StringAssert.DoesNotContain("UIStyle", Source("PartHighlighter.cs"),
                "гильзы труб и фитингов красит та же накладка тем же красным");
        }
    }
}

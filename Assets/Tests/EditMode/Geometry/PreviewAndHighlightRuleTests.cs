using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Правило `docs/UI-GUIDELINES.md` § 9 «превью — это состояние ВИДА, а не
    /// проекта», которое нельзя проверить вызовом: оно про то, чего в коде БЫТЬ не
    /// должно.
    ///
    /// Призрак живёт внутри `ElementFactorySandbox`, поэтому не попадает в реестр, а с
    /// ним — ни в автосохранение, ни в валидацию, ни в отмену. Одна строка
    /// `CommandStack.Execute` в `ScenePreview` превратила бы наведение мышью в
    /// отменяемую операцию, а сохранённый файл — в проект с трубой, которую никто не
    /// выбирал; поймать это тестом на поведение можно только целой сценой.
    ///
    /// Запрет тут именно на ПРАВКУ проекта. Спросить у `PartRegistry`, жив ли ещё
    /// хозяин превью, законно — призрак обязан уходить вместе с удалённым хозяином, — и
    /// этот вопрос вынесен в `HoverAnchor`, общий контракт устаревания призрака и
    /// красной накладки.
    ///
    /// Правило «накладка в сцене одна» живёт в `HighlightConsumerRuleTests`: оно требует
    /// отражения над сборкой, а этот каталог собирается ещё и обычным .NET, без движка.
    ///
    /// Сканирует этот сторож ровно один файл и ничего не знает про его вызывающих: то,
    /// что `PipePortHover` читает `PartRegistry.GetAll()`, законно и сюда не входит.</summary>
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
        public void ScenePreview_NeverEditsTheProject()
        {
            var source = Source("ScenePreview.cs");
            foreach (var forbidden in new[]
            {
                "CommandStack", "SaveLoadManager", "ConstraintValidator",
                "SpecificationManager", "AutoSave",
                "PartRegistry.Register", "PartRegistry.Unregister",
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
    }
}

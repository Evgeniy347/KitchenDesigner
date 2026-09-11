using System.Collections.Generic;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сенсор золотых UI-снимков проверяется на быстром пути: сам
    /// UiSnapshotEngine без сцены не построить, а решение «это одна точка или
    /// две» сцены и не требует — оно целиком в UiNodeOverlap.
    ///
    /// Каждый тест здесь называет ситуацию, из-за которой инвариант либо
    /// сработает, либо будет отключён первым же прогоном: настоящая свалка,
    /// законный дрейф вёрстки, нулевой узел, панель целиком уехавшая в
    /// сторону.</summary>
    public class UiNodeOverlapTests
    {
        private static UiNodeOverlap.Placed At(string id, float x, float y) =>
            new UiNodeOverlap.Placed(id, x, y, 16f, 16f);

        private static UiNodeOverlap.Placed[] Panel(float dx, float dy) => new[]
        {
            At("a", 40f + dx, 120f + dy),
            At("b", 40f + dx, 123f + dy),
            At("c", 90f + dx, 120f + dy),
        };

        [Test]
        public void FifteenHintIconsInOnePoint_AreReportedAsOneGroup_WithEveryName()
        {
            var nodes = new List<UiNodeOverlap.Placed>();
            for (int i = 0; i < 15; i++) nodes.Add(At("Label «i» (Hint_" + i + ")", 40f, 120f));

            var groups = UiNodeOverlap.Collisions(nodes);

            Assert.AreEqual(1, groups.Count,
                "одна свалка — одна строка сообщения, а не пятнадцать: читать придётся человеку");
            StringAssert.Contains("×15", groups[0],
                "число в группе отвечает на вопрос «сколько их», без второго прогона");
            StringAssert.Contains("Hint_0", groups[0],
                "имена узлов — единственное, по чему свалку находят в сцене");
            StringAssert.Contains("Hint_14", groups[0],
                "имена нужны ВСЕ: обрезанный список делает два разных диагноза одинаковыми");

            var report = UiNodeOverlap.Report("ui_settings_tab_view", nodes);
            Assert.IsNotEmpty(report,
                "настоящая свалка обязана давать непустой отчёт, иначе сенсор молчит");
            StringAssert.Contains("ui_settings_tab_view", report,
                "сообщение обязано называть снимок: снимков в репозитории под восемьдесят");
        }

        /// <summary>Тот самый дрейф, ради невосприимчивости к которому координаты
        /// и перестали сличать: панель целиком уехала, взаимное расположение
        /// цело. Вердикт обязан не измениться ни на пиксель сдвига, ни на
        /// триста — иначе эталоны снова станут одноразовыми и сенсор выключат
        /// первым же зелёным коммитом.</summary>
        [Test]
        public void AShiftOfTheWholePanel_ChangesNothing_NeitherByOnePixelNorByThreeHundred()
        {
            Assert.IsEmpty(UiNodeOverlap.Collisions(Panel(0f, 0f)),
                "исходная панель чиста — иначе следующие два сравнения ничего не значат");
            Assert.IsEmpty(UiNodeOverlap.Collisions(Panel(1f, 1f)),
                "сдвиг на пиксель — обычный дрейф вёрстки от правки шрифта или отступа");
            Assert.IsEmpty(UiNodeOverlap.Collisions(Panel(300f, -220f)),
                "сравниваются не координаты, а только совпадение узлов между собой");
        }

        /// <summary>Противоположный вход к предыдущему: допуск обязан быть
        /// конечным. Если бы он съедал любое расстояние, тест выше был бы
        /// зелёным и на сенсоре, который всегда молчит.</summary>
        [Test]
        public void TwoNodesFartherThanTheTolerance_AreTwoNodes_AndCloserOnesAreOne()
        {
            Assert.IsEmpty(UiNodeOverlap.Collisions(new[] { At("a", 40f, 120f), At("b", 40f, 123f) }),
                "три пикселя по вертикали — разные строки, а не свалка");
            Assert.AreEqual(1,
                UiNodeOverlap.Collisions(new[] { At("a", 40f, 120f), At("b", 41f, 120f) }).Count,
                "пиксель между центрами двух разных узлов — это и есть «в одной точке»");
        }

        /// <summary>Единственное исключение правила, и оно про ПЛОЩАДЬ, а не про
        /// имена: узел, который ничего не рисует, никого собой не закрывает.
        /// Свёрнутые строки и нераскрытые группы складываются в нуль законно.
        /// Список имён-исключений здесь заводить нельзя — он протухнет ровно
        /// так же, как протух бы список проверяемых панелей.</summary>
        [Test]
        public void NodesWithNoArea_AreExcluded_TheyDrawNothingAndHideNothing()
        {
            var collapsed = new List<UiNodeOverlap.Placed>
            {
                new UiNodeOverlap.Placed("collapsed_a", 0f, 0f, 0f, 0f),
                new UiNodeOverlap.Placed("collapsed_b", 0f, 0f, 120f, 0f),
                new UiNodeOverlap.Placed("collapsed_c", 0f, 0f, 0f, 18f),
            };

            Assert.IsEmpty(UiNodeOverlap.Collisions(collapsed),
                "три невидимых узла в нуле — законное совпадение свёрнутых строк");
            Assert.IsFalse(UiNodeOverlap.Draws(collapsed[1]),
                "нулевая высота — тоже нулевая площадь, ширина одна ничего не рисует");

            collapsed.Add(new UiNodeOverlap.Placed("visible_a", 0f, 0f, 16f, 16f));
            Assert.IsEmpty(UiNodeOverlap.Collisions(collapsed),
                "один видимый узел среди невидимых — не свалка");

            collapsed.Add(new UiNodeOverlap.Placed("visible_b", 0f, 0f, 16f, 16f));
            Assert.AreEqual(1, UiNodeOverlap.Collisions(collapsed).Count,
                "а два видимых — свалка, и исключение не вправе её прикрыть");
        }

        /// <summary>Сообщение обязано быть одинаковым при любом порядке обхода
        /// сцены: иначе дифф двух прогонов неизменившегося кода читается как
        /// изменение.</summary>
        [Test]
        public void TheReport_DoesNotDependOnTheOrderOfTheWalk()
        {
            var forward = new[]
            {
                At("b", 10f, 10f), At("a", 10f, 10f), At("c", 90f, 90f), At("d", 90f, 90f),
            };
            var backward = new[]
            {
                At("d", 90f, 90f), At("c", 90f, 90f), At("a", 10f, 10f), At("b", 10f, 10f),
            };

            var groups = UiNodeOverlap.Collisions(forward);
            Assert.AreEqual(2, groups.Count,
                "две независимые свалки — две строки: иначе сравнение ниже сойдётся на пустоте");
            CollectionAssert.AreEqual(groups, UiNodeOverlap.Collisions(backward),
                "порядок обхода сцены не вправе менять текст сообщения");
        }

        [Test]
        public void ACleanPanel_ProducesAnEmptyReport_SoTheSensorIsSilentWhenItShouldBe()
        {
            Assert.IsEmpty(
                UiNodeOverlap.Report("ui_clean",
                    new[] { At("a", 0f, 0f), At("b", 0f, 40f), At("c", 0f, 80f) }),
                "на здоровой панели сенсор обязан молчать — иначе его отключат, а не починят");
        }
    }
}

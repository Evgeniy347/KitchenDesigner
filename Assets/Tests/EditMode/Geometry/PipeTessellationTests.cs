using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сколько граней у трубы по окружности.
    ///
    /// Число было ОДНО на всё — шестнадцать. Для штанги Ø 32 мм это плоскости
    /// по 6 мм, незаметно; для тропической лейки Ø 250 мм — по 48 мм, и диск
    /// на кадре читался пилюлей с гранёным контуром. Обиднее всего, что
    /// выглядит это как ошибка формы: «лейка не похожа на диск» толкает
    /// править толщину пояска и фаски, тогда как править надо было
    /// тесселяцию.
    ///
    /// Поэтому число граней теперь выводится из РАДИУСА, а критерий качества
    /// — не сами грани, а стрелка прогиба: насколько многоугольник
    /// проваливается внутрь окружности. Её и проверяем, потому что именно её
    /// видит глаз.</summary>
    public class PipeTessellationTests
    {
        [Test]
        public void PipeTessellation_GivesTheRainHeadFarMoreSidesThanTheRiser()
        {
            int head = PipeTessellation.RadialSegmentsFor(
                ShowerColumnSpec.DefaultHeadDiameterMM * 0.5f);
            int riser = PipeTessellation.RadialSegmentsFor(
                ShowerColumnSpec.DefaultRiserDiameterMM * 0.5f);

            Assert.Greater(head, riser,
                "лейка Ø 250 мм и штанга Ø 32 мм не могут иметь одинаковое число граней: "
                + "одна грань из шестнадцати — это 6 мм на штанге и 48 мм на лейке, и "
                + "именно поэтому диск читался гранёной пилюлей");
        }

        [Test]
        public void PipeTessellation_KeepsTheSagittaBelowHalfAMillimetreOnBigParts()
        {
            float radius = ShowerColumnSpec.DefaultHeadDiameterMM * 0.5f;
            int sides = PipeTessellation.RadialSegmentsFor(radius);

            Assert.Less(PipeTessellation.SagittaMM(radius, sides), Tolerance.ContactMm,
                "стрелка прогиба многоугольника — это и есть та щербина на контуре, "
                + "которую видно глазом. Полмиллиметра на четвертьметровой лейке "
                + "неразличимы, а вот те 6 мм, что давали шестнадцать граней, — очень");
        }

        [Test]
        public void PipeTessellation_NeverDropsBelowTheFloorNorClimbsPastTheCeiling()
        {
            Assert.AreEqual(PipeTessellation.MinRadialSegments,
                PipeTessellation.RadialSegmentsFor(0f),
                "у вырожденного радиуса всё равно есть минимум граней: ноль граней — это "
                + "пустой меш вместо детали");
            Assert.AreEqual(PipeTessellation.MinRadialSegments,
                PipeTessellation.RadialSegmentsFor(
                    ShowerColumnSpec.MinRiserDiameterMM * 0.5f),
                "самая тонкая труба сидит на полу: дробить её мельче незачем, а "
                + "вершины стоят денег на каждом элементе сцены");
            Assert.AreEqual(PipeTessellation.MaxRadialSegments,
                PipeTessellation.RadialSegmentsFor(
                    ShowerColumnSpec.MaxHeadDiameterMM * 0.5f),
                "а самая большая лейка упирается в потолок: без него формула на "
                + "полуметровом диске насчитала бы полторы сотни граней");
        }

        [Test]
        public void PipeTessellation_AlwaysReturnsAnEvenCount()
        {
            foreach (float radius in new[] { 8f, 16f, 25f, 55f, 125f, 300f })
                Assert.AreEqual(0, PipeTessellation.RadialSegmentsFor(radius) % 2,
                    "число граней чётное: у нечётного многоугольника нет двух "
                    + "противоположных рёбер, и силуэт трубы получается несимметричным "
                    + "относительно собственной оси");
        }
    }
}

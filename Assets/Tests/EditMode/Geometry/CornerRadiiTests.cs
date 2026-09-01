using System;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Четыре радиуса углов как ОДНО значение. Раньше они ездили
    /// четырьмя подряд идущими float-ами, и порядок держался только на памяти
    /// вызывающего: перепутанные местами два аргумента компилируются молча и
    /// срезают не тот угол.
    ///
    /// Порядок — обход контура: (−X,−Z) → (+X,−Z) → (+X,+Z) → (−X,+Z). Индексатор
    /// нужен коду, который перебирает углы в цикле (Build, Fit), именованные поля —
    /// коду, который знает, какой угол ему нужен. Оба обязаны говорить одно и то
    /// же, иначе цикл и вызывающий разойдутся.</summary>
    public class CornerRadiiTests
    {
        [Test]
        public void Indexer_FollowsTheContourOrder_SameAsTheNamedFields()
        {
            var radii = new CornerRadii(1f, 2f, 3f, 4f);

            Assert.AreEqual(1f, radii.MinusXMinusZ, "первый параметр — угол (−X,−Z)");
            Assert.AreEqual(2f, radii.PlusXMinusZ, "второй — (+X,−Z)");
            Assert.AreEqual(3f, radii.PlusXPlusZ, "третий — (+X,+Z)");
            Assert.AreEqual(4f, radii.MinusXPlusZ, "четвёртый — (−X,+Z)");

            Assert.AreEqual(radii.MinusXMinusZ, radii[0], "индекс 0 — тот же угол, что и поле");
            Assert.AreEqual(radii.PlusXMinusZ, radii[1],
                "индексатор и имена обязаны совпадать: Build перебирает углы индексом, "
                + "а вызывающий задаёт их именами, и расхождение срезало бы не тот угол");
            Assert.AreEqual(radii.PlusXPlusZ, radii[2], "индекс 2");
            Assert.AreEqual(radii.MinusXPlusZ, radii[3], "индекс 3");
        }

        [Test]
        public void Count_IsFour_AndBoundsTheIndexer()
        {
            Assert.AreEqual(4, CornerRadii.Count, "у прямоугольника четыре угла");

            var radii = CornerRadii.Uniform(5f);
            Assert.Throws<IndexOutOfRangeException>(() => { _ = radii[CornerRadii.Count]; },
                "выход за последний угол — ошибка вызывающего, а не тихий ноль: тихий ноль "
                + "дал бы острый угол там, где просили скруглённый");
            Assert.Throws<IndexOutOfRangeException>(() => { _ = radii[-1]; },
                "и в другую сторону тоже");
        }

        [Test]
        public void Uniform_PutsTheSameRadiusOnEveryCorner()
        {
            var radii = CornerRadii.Uniform(7f);

            for (int corner = 0; corner < CornerRadii.Count; corner++)
                Assert.AreEqual(7f, radii[corner],
                    "четыре типа мебели из шести задают одинаковое скругление всех углов, "
                    + "и повторять число четыре раза у каждого из них — тот самый источник "
                    + "перепутанных аргументов. Угол " + corner);
        }
    }
}

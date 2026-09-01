using System;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Защёлка от повторного входа в перестроение мебели. Раньше её
    /// поле `_applying` жило отдельной копией в табуретке, стуле и радиусном
    /// столе: перестроение клампит свои же свойства, сеттер клампленного
    /// свойства снова зовёт ApplyDimensions — без защёлки это бесконечная
    /// рекурсия и переполнение стека прямо в редакторе.
    ///
    /// Тесты нарочно проверяют не «сколько раз позвали», а ЧТО именно
    /// происходит с вложенным вызовом: он обязан быть проглочен, а не отложен.</summary>
    public class RebuildGuardTests
    {
        private RebuildGuard _guard = new RebuildGuard();
        private int _calls;

        [SetUp]
        public void SetUp()
        {
            _guard = new RebuildGuard();
            _calls = 0;
        }

        private void CountAndReenter()
        {
            _calls++;
            if (_calls < 5) _guard.Run(CountAndReenter);
        }

        [Test]
        public void Run_CallsTheBody_WhenNobodyIsInside()
        {
            _guard.Run(() => _calls++);

            Assert.AreEqual(1, _calls, "обычный вызов обязан дойти до тела");
        }

        [Test]
        public void Run_SwallowsTheNestedCall_InsteadOfRecursing()
        {
            _guard.Run(CountAndReenter);

            Assert.AreEqual(1, _calls,
                "вложенный вызов обязан быть проглочен: именно так перестроение "
                + "переживает сеттер, который клампит своё поле и снова зовёт "
                + "ApplyDimensions — иначе рекурсия до переполнения стека");
        }

        [Test]
        public void Run_ReleasesTheLatch_AfterTheBodyReturns()
        {
            _guard.Run(() => _calls++);
            _guard.Run(() => _calls++);

            Assert.AreEqual(2, _calls,
                "защёлка одноразовой быть не может: следующая правка габарита "
                + "обязана перестроить мебель заново");
        }

        [Test]
        public void Run_ReleasesTheLatch_EvenWhenTheBodyThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => _guard.Run(() => throw new InvalidOperationException("boom")));

            _guard.Run(() => _calls++);

            Assert.AreEqual(1, _calls,
                "исключение внутри перестроения не имеет права навсегда запереть "
                + "элемент: без finally первая же ошибка в геометрии превращала бы "
                + "мебель в неизменяемую до перезапуска");
        }
    }
}

using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Допуски — общий словарь снэпа, ресайза и валидации, поэтому их
    /// ГРАНИЦЫ важнее значений: именно на «&gt;= против &gt;» и «&lt; против &lt;=»
    /// ломается прилипание. Тесты пинят каждую границу отдельно.
    ///
    /// Живут в KitchenDesigner.Geometry: исполняются и Unity, и обычным
    /// dotnet test (см. docs/GEOMETRY-EXTRACTION-PLAN.md).</summary>
    public class ToleranceTests
    {
        // ── IsParallel ────────────────────────────────────────────────────
        // Порог ВКЛЮЧАЮЩИЙ: грань ровно на ParallelDot обязана считаться
        // параллельной, иначе пара граней на границе точности теряется.

        [Test]
        public void IsParallel_AcceptsExactThreshold()
        {
            Assert.IsTrue(Tolerance.IsParallel(Tolerance.ParallelDot));
            Assert.IsTrue(Tolerance.IsParallel(-Tolerance.ParallelDot));
        }

        [Test]
        public void IsParallel_RejectsJustBelowThreshold()
        {
            Assert.IsFalse(Tolerance.IsParallel(Tolerance.ParallelDot - 1e-4f));
            Assert.IsFalse(Tolerance.IsParallel(-(Tolerance.ParallelDot - 1e-4f)));
        }

        /// <summary>Плоскость двусторонняя: встречная грань (dot ≈ -1) и
        /// со-направленная (dot ≈ +1) одинаково параллельны.</summary>
        [Test]
        public void IsParallel_TreatsBothDirectionsAlike()
        {
            Assert.IsTrue(Tolerance.IsParallel(1f));
            Assert.IsTrue(Tolerance.IsParallel(-1f));
            Assert.IsFalse(Tolerance.IsParallel(0f));
        }

        // ── IntervalsOverlap ──────────────────────────────────────────────
        // Пересечение СТРОГОЕ с запасом: касание встык — не пересечение,
        // иначе детали, стоящие вплотную, считались бы столкнувшимися.

        [Test]
        public void IntervalsOverlap_TouchingIsNotOverlap()
        {
            Assert.IsFalse(Tolerance.IntervalsOverlap(0f, 1f, 1f, 2f));
        }

        [Test]
        public void IntervalsOverlap_RealOverlapIsDetected()
        {
            Assert.IsTrue(Tolerance.IntervalsOverlap(0f, 1f, 0.5f, 2f));
            Assert.IsTrue(Tolerance.IntervalsOverlap(0.5f, 2f, 0f, 1f));
        }

        [Test]
        public void IntervalsOverlap_SeparatedIntervalsDoNotOverlap()
        {
            Assert.IsFalse(Tolerance.IntervalsOverlap(0f, 1f, 2f, 3f));
            Assert.IsFalse(Tolerance.IntervalsOverlap(2f, 3f, 0f, 1f));
        }

        /// <summary>Перекрытие мельче запаса — это касание, а не столкновение.
        /// На этом стоит проверка твёрдых тел с margin = ContactMm.</summary>
        [Test]
        public void IntervalsOverlap_OverlapSmallerThanMarginIsIgnored()
        {
            float margin = Tolerance.ContactMm * 0.001f;
            Assert.IsFalse(Tolerance.IntervalsOverlap(0f, 1f, 1f - margin * 0.5f, 2f, margin));
            Assert.IsTrue(Tolerance.IntervalsOverlap(0f, 1f, 1f - margin * 3f, 2f, margin));
        }

        /// <summary>Обе половины условия строгие, и проверять это надо РОВНО на
        /// границе. Здесь `max2 - margin` даёт точный ноль, поэтому первая половина
        /// решает исход одна: строгое `&lt;` отвечает «нет», нестрогое сказало бы «да».
        /// Заодно ловится и перепутанный знак запаса (`max2 + margin`).</summary>
        [Test]
        public void IntervalsOverlap_LowerBoundIsStrictAtExactMargin()
        {
            float m = Tolerance.EpsilonUnits;
            Assert.IsFalse(Tolerance.IntervalsOverlap(0f, 10f, -10f, m, m));
        }

        /// <summary>Зеркальная граница: `min2 + margin` точно равен `max1`.</summary>
        [Test]
        public void IntervalsOverlap_UpperBoundIsStrictAtExactMargin()
        {
            float m = Tolerance.EpsilonUnits;
            Assert.IsFalse(Tolerance.IntervalsOverlap(-10f, m, 0f, 10f, m));
        }

        [Test]
        public void IntervalsOverlap_ContainedIntervalOverlaps()
        {
            Assert.IsTrue(Tolerance.IntervalsOverlap(0f, 10f, 4f, 5f));
            Assert.IsTrue(Tolerance.IntervalsOverlap(4f, 5f, 0f, 10f));
        }

        // ── ApproxEqual / IsNoiseMm ───────────────────────────────────────
        // Сравнение СТРОГОЕ: ровно эпсилон — уже различие.

        [Test]
        public void ApproxEqual_BelowEpsilonIsEqual()
        {
            Assert.IsTrue(Tolerance.ApproxEqual(1f, 1f + Tolerance.EpsilonUnits * 0.5f));
            Assert.IsTrue(Tolerance.ApproxEqual(1f, 1f));
        }

        [Test]
        public void ApproxEqual_AtOrAboveEpsilonIsDifferent()
        {
            Assert.IsFalse(Tolerance.ApproxEqual(1f, 1f + Tolerance.EpsilonUnits));
            Assert.IsFalse(Tolerance.ApproxEqual(1f, 1f + Tolerance.EpsilonUnits * 2f));
        }

        /// <summary>Разница РОВНО в эпсилон — уже различие (сравнение строгое).
        /// Проверять это на 1f + эпсилон нельзя: 1f + 1e-4f в float даёт разницу
        /// чуть больше эпсилона, и строгое сравнение неотличимо от нестрогого.
        /// От нуля же вычитание точное, и граница проверяется честно.</summary>
        [Test]
        public void ApproxEqual_ExactlyEpsilonIsAlreadyDifferent()
        {
            Assert.IsFalse(Tolerance.ApproxEqual(0f, Tolerance.EpsilonUnits));
            Assert.IsFalse(Tolerance.ApproxEqual(0f, -Tolerance.EpsilonUnits));
        }

        [Test]
        public void IsNoiseMm_HalfMillimetreIsAlreadySignificant()
        {
            Assert.IsTrue(Tolerance.IsNoiseMm(Tolerance.ContactMm * 0.5f));
            Assert.IsTrue(Tolerance.IsNoiseMm(-Tolerance.ContactMm * 0.5f));
            Assert.IsFalse(Tolerance.IsNoiseMm(Tolerance.ContactMm));
            Assert.IsFalse(Tolerance.IsNoiseMm(-Tolerance.ContactMm));
        }

        // ── Соотношения констант ──────────────────────────────────────────
        // Ослабление любого из них молча меняет поведение всех трёх систем.

        [Test]
        public void SnapEpsilon_IsFinerThanCoordinateNoise()
        {
            Assert.Less(Tolerance.SnapEpsilon, Tolerance.EpsilonUnits,
                "инклюзивный допуск порога обязан быть тоньше шума координат");
        }

        [Test]
        public void CoordinateNoise_IsFinerThanContact()
        {
            Assert.Less(Tolerance.EpsilonUnits, Tolerance.ContactMm * 0.001f,
                "шум координат обязан быть тоньше порога контакта");
        }

        [Test]
        public void MinSupportOverlap_IsAFractionNotAPercent()
        {
            Assert.Greater(Tolerance.MinSupportOverlap, 0f);
            Assert.Less(Tolerance.MinSupportOverlap, 1f);
        }

        [Test]
        public void ParallelDot_IsCloseToOneButNotOne()
        {
            Assert.Greater(Tolerance.ParallelDot, 0.9f);
            Assert.Less(Tolerance.ParallelDot, 1f);
        }
    }
}

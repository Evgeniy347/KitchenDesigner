using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Подушка дивана, построенная из куба, а не выдавленная из плоского
    /// контура. Выдавка (ProfileExtrusionMesh) скругляет ровно две стороны из трёх:
    /// две кромки, перпендикулярные оси выдавливания, остаются острыми, а пласти —
    /// плоскими. Живьём это читается «глубоким стадионом» и топорно.
    ///
    /// Взятая техника — скруглённый куб по Catlike Coding: внутри габарита сидит
    /// уменьшенная коробка, и каждая точка поверхности = ближайшая точка этой
    /// коробки плюс радиус вдоль нормали. Скругление получается ОДИНАКОВОЕ по всем
    /// трём осям и задано в физических миллиметрах, а не долей размера — этим
    /// приём и отличается от суперэллипсоида, у которого «радиус» пропорционален
    /// полуоси и на подушке 650x440x200 превращается в эллипс.
    ///
    /// Поверх скруглённого куба — надув (в Blender это давление ткани): грани
    /// выпучиваются наружу с весом cos^2, который равен единице в центре грани и
    /// НУЛЮ со нулевой производной на стыке. Поэтому шов остаётся стянутым, соседние
    /// грани сходятся в одну и ту же точку с одной и той же нормалью, а габарит
    /// подушки задают именно центры граней.
    ///
    /// Класс живёт на быстром пути: это чистая арифметика без Mesh, и она
    /// проверяется за 0,3 с. Сборка меша — CushionMesh в Core/Elements.</summary>
    public class CushionSurfaceTests
    {
        private const float Tol = 1e-4f;

        private static Vector3 BackCushion() => new Vector3(0.65f, 0.2f, 0.44f);

        private static CushionSurface Default() => new CushionSurface(BackCushion(), 0.09f);

        private static Vector3 Abs(Vector3 v)
            => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static float MaxAlong(CushionSurface surface, int axis)
        {
            float max = 0f;
            foreach (var p in surface.Positions) max = Mathf.Max(max, Mathf.Abs(p[axis]));
            return max;
        }

        [Test]
        public void EveryVertex_StaysInsideTheDeclaredBox_AndTheFaceCentresTouchIt()
        {
            var surface = Default();
            var half = BackCushion() * 0.5f;

            foreach (var p in surface.Positions)
                for (int axis = 0; axis < 3; axis++)
                    Assert.LessOrEqual(Mathf.Abs(p[axis]), half[axis] + Tol,
                        "подушка обязана уложиться в объявленный габарит: рамка "
                        + "изометрического снимка и раскладка дивана строятся по нему, и всё, "
                        + "что торчит наружу, вылезет за пределы дивана");

            for (int axis = 0; axis < 3; axis++)
                Assert.AreEqual(half[axis], MaxAlong(surface, axis), Tol,
                    "и обязана его КАСАТЬСЯ центром грани: надув выпучивает грань ровно "
                    + "настолько, насколько сжато ядро, иначе подушка молча похудеет "
                    + "против размера, который назвала раскладка, ось " + axis);
        }

        [Test]
        public void EveryVertex_LiesBetweenTheFillet_AndTheFilletPlusTheBulge()
        {
            var surface = Default();

            foreach (var p in surface.Positions)
            {
                float distance = surface.DistanceToInnerBox(p);
                Assert.GreaterOrEqual(distance, surface.Radius - Tol,
                    "ближе радиуса к внутренней коробке поверхности быть не может: это и "
                    + "есть скруглённый куб (сумма Минковского коробки и шара)");
                Assert.LessOrEqual(distance, surface.Radius + surface.Bulge + Tol,
                    "а дальше радиуса её отпускает только надув, и ровно на его величину");
            }
        }

        [Test]
        public void TheFillet_KeepsItsPhysicalRadius_OnACushionOfAnyAspectRatio()
        {
            const float radius = 0.08f;
            var flat = new CushionSurface(new Vector3(0.6f, 0.2f, 0.4f), radius);
            var square = new CushionSurface(new Vector3(0.4f, 0.4f, 0.4f), radius);

            foreach (var surface in new[] { flat, square })
            {
                Assert.AreEqual(radius, surface.Radius, Tol,
                    "радиус задан в миллиметрах и не зависит от пропорций подушки: у "
                    + "суперэллипсоида он пропорционален полуоси, и на несимметричной "
                    + "подушке скругление вытянулось бы в эллипс");

                float closest = float.MaxValue;
                foreach (var p in surface.Positions)
                    closest = Mathf.Min(closest, surface.DistanceToInnerBox(p));
                Assert.AreEqual(radius, closest, Tol,
                    "и на шве скругление лежит ровно на радиусе — там, где надув равен нулю");
            }
        }

        [Test]
        public void TheSeamsStayPinched_WhileTheFacesBulgeOut()
        {
            var surface = Default();
            var half = BackCushion() * 0.5f;

            Assert.AreEqual(1f, CushionSurface.BulgeWeight(0f), Tol,
                "в центре грани надув полный");
            Assert.AreEqual(0f, CushionSurface.BulgeWeight(1f), Tol,
                "на стыке граней он обязан обнулиться, иначе соседние грани разойдутся "
                + "и по шву пойдёт щель");
            Assert.AreEqual(0f, CushionSurface.BulgeWeight(1f) - CushionSurface.BulgeWeight(0.999f),
                1e-5f, "и обнулиться ПЛАВНО: у cos^2 в этой точке нулевая производная, "
                + "поэтому надув переходит в скругление без излома");

            for (int axis = 0; axis < 3; axis++)
                Assert.Less(surface.CoreHalfExtents[axis], half[axis],
                    "ядро подушки ужато на величину надува: угол получается поджатым, "
                    + "как у настоящей подушки, а не разбухшим наружу, ось " + axis);
        }

        [Test]
        public void EveryTriangle_FacesOutwards()
        {
            var surface = Default();
            var triangles = surface.Triangles;

            Assert.AreEqual(0, triangles.Length % 3, "треугольники идут тройками");
            int counted = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = surface.Positions[triangles[i]];
                var b = surface.Positions[triangles[i + 1]];
                var c = surface.Positions[triangles[i + 2]];
                var winding = Vector3.Cross((b - a).normalized, (c - a).normalized);
                if (winding.sqrMagnitude < Tolerance.EpsilonSqr) continue;

                counted++;
                var shading = surface.Normals[triangles[i]];
                Assert.Greater(Vector3.Dot(winding.normalized, shading), 0f,
                    "обход треугольника и нормаль вершины смотрят в разные стороны — "
                    + "половина подушки вывернется наизнанку и пропадёт при отсечении "
                    + "задних граней, треугольник " + i / 3);
            }

            Assert.Greater(counted, triangles.Length / 6,
                "вырожденных треугольников больше половины — тест позеленел бы, "
                + "ничего не проверив");
        }

        [Test]
        public void TheSeamVertices_ShareTheirNormals_SoTheFacesShadeAsOneSurface()
        {
            var surface = Default();
            int compared = 0;

            for (int i = 0; i < surface.Positions.Length; i++)
            {
                Assert.AreEqual(1f, surface.Normals[i].magnitude, Tol,
                    "нормаль обязана быть единичной, иначе освещение поедет");

                for (int j = i + 1; j < surface.Positions.Length; j++)
                {
                    if ((surface.Positions[i] - surface.Positions[j]).sqrMagnitude
                        > Tolerance.EpsilonSqr) continue;
                    compared++;
                    Assert.Greater(Vector3.Dot(surface.Normals[i], surface.Normals[j]), 0.999f,
                        "две грани сходятся в одной точке шва и обязаны согласиться о "
                        + "нормали: иначе по стыку пойдёт видимая грань, и подушка снова "
                        + "будет выглядеть склеенной из досок");
                }
            }

            Assert.Greater(compared, 0,
                "совпадающих вершин на швах не нашлось — грани не сходятся, и тест "
                + "проверил бы пустоту");
        }

        [Test]
        public void AThinCushion_ClampsTheRadiusToItsOwnCore_InsteadOfTurningInsideOut()
        {
            var thin = new CushionSurface(new Vector3(0.6f, 0.1f, 0.4f), 0.5f);

            Assert.AreEqual(Mathf.Min(thin.CoreHalfExtents.x,
                Mathf.Min(thin.CoreHalfExtents.y, thin.CoreHalfExtents.z)),
                thin.Radius, Tol,
                "радиус больше половины наименьшей стороны вывернул бы поверхность "
                + "наизнанку; ужимать его надо по ЯДРУ, а не по габариту, потому что "
                + "надув уже съел свою долю");
            Assert.GreaterOrEqual(thin.InnerHalfExtents.y, 0f,
                "внутренняя коробка не имеет права стать отрицательной");
        }

        [Test]
        public void WithoutTheBulge_TheShapeIsExactlyARoundedBox()
        {
            var box = new CushionSurface(BackCushion(), 0.09f, 0f,
                CushionSurface.DefaultArcSegments, CushionSurface.DefaultFlatSegments);

            Assert.AreEqual(0f, box.Bulge, Tol, "надув выключен");
            foreach (var p in box.Positions)
                Assert.AreEqual(box.Radius, box.DistanceToInnerBox(p), Tol,
                    "без надува каждая точка поверхности стоит ровно на радиусе от "
                    + "внутренней коробки — это определение скруглённого куба, и оно "
                    + "здесь положительный контроль для формулы точки");
        }

        [Test]
        public void TheGrid_SpendsItsSegmentsOnTheFillet_SoTheSilhouetteIsSmoothAndTheCostStaysKnown()
        {
            var surface = Default();
            int triangles = surface.Triangles.Length / 3;

            Assert.AreEqual(640, triangles,
                "цена подушки известна и записана: 640 треугольников против 256 у плоской "
                + "выдавки. Три сегмента на каждые 45 градусов скругления дают на радиусе "
                + "90 мм отклонение силуэта меньше двух миллиметров — дороже платить не за "
                + "что, дешевле уже видно гранями");
            Assert.AreEqual(414, surface.Positions.Length,
                "вершины дублируются по граням ради развёртки, и это тоже часть цены");
            Assert.AreEqual(surface.Positions.Length, surface.Normals.Length,
                "нормаль на каждую вершину: короткий массив Unity молча дополнит нулями, "
                + "и часть подушки почернеет");
            Assert.AreEqual(surface.Positions.Length, surface.Uvs.Length,
                "и развёртка на каждую вершину — по той же причине");
        }

        [Test]
        public void TheUvs_SpanZeroToOne_AcrossEachFace()
        {
            var surface = Default();
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (var uv in surface.Uvs)
            {
                min = Mathf.Min(min, Mathf.Min(uv.x, uv.y));
                max = Mathf.Max(max, Mathf.Max(uv.x, uv.y));
            }

            Assert.AreEqual(0f, min, Tol, "развёртка каждой грани начинается в нуле");
            Assert.AreEqual(1f, max, Tol,
                "и заканчивается в единице: повтор декора задаёт BaseMap_ST по "
                + "DecorSurfaceMM владельца, как у всех остальных мешей проекта");
        }

        [Test]
        public void BulgeFor_TakesTheSmallestHalfExtent_SoAThinCushionIsNotBlownApart()
        {
            var slab = new Vector3(0.65f, 0.2f, 0.44f);

            Assert.AreEqual(0.1f * CushionSurface.DefaultBulgeRatio,
                CushionSurface.BulgeFor(slab), Tol,
                "надув отмеряется от САМОЙ ТОНКОЙ полуоси: доля от длинной стороны "
                + "съела бы всю толщину подушки, и ядро ушло бы в минус");
            Assert.AreEqual(CushionSurface.BulgeFor(slab),
                new CushionSurface(slab, 0.09f).Bulge, Tol,
                "и именно эта величина уходит в поверхность по умолчанию");
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Физическая развёртка мягких деталей дивана: валика (плоская плита с
    /// мягкой кромкой) и подушки. Их собственные UV — «0..1 по плану» и «0..1 на каждую
    /// грань», и вместе с одним <c>_BaseMap_ST</c> на деталь они растягивают рисунок
    /// на боковых стенках (полосы вдоль одной оси на фото пользователя). Проекция по
    /// доминирующей оси нормали даёт на плоских гранях ровно миллиметры детали, и тогда
    /// <c>ST = 1000 / размер плитки</c> одинаков по обеим осям.</summary>
    public class PartUvTests
    {
        private const float Mm = 1000f;

        private static Vector3[] ToMm(Vector3[] units)
        {
            var mm = new Vector3[units.Length];
            for (int i = 0; i < units.Length; i++) mm[i] = units[i] * Mm;
            return mm;
        }

        private static Vector2[] ScaleBy(Vector2[] uv, Vector2 scale)
        {
            var scaled = new Vector2[uv.Length];
            for (int i = 0; i < uv.Length; i++) scaled[i] = new Vector2(uv[i].x * scale.x, uv[i].y * scale.y);
            return scaled;
        }

        private static SoftSlabSurface ArmSlab() => new SoftSlabSurface(0.32f, 0.70f,
            new CornerRadii(0.04f, 0.04f, 0.15f, 0.15f), 0.24f, 0.08f);

        [Test]
        public void ProjectedUv_OfASoftSlab_IsPhysicalMillimetres_OnEveryFlatFace()
        {
            var slab = ArmSlab();
            var uv = PartUv.BoxProjectionUnits(slab.Positions, slab.Normals);

            var report = UvStretch.Measure(ToMm(slab.Positions), ScaleBy(uv, new Vector2(Mm, Mm)),
                slab.Normals, slab.Triangles);

            Assert.Greater(report.Triangles, 20,
                "плоских треугольников нашлось слишком мало — сенсор ничего не проверил бы");
            Assert.AreEqual(1f, report.MinSigma, 0.01f,
                "в любом направлении по плоской грани валика миллиметр детали — это миллиметр "
                + "текстуры: полос нет");
            Assert.AreEqual(1f, report.MaxSigma, 0.01f, "и крупнее картинка тоже не стала");
        }

        [Test]
        public void ProjectedUv_OfACushion_IsPhysicalMillimetres_OnEveryFlatFace()
        {
            var cushion = new CushionSurface(new Vector3(0.65f, 0.44f, 0.2f), 0.09f);
            var uv = PartUv.BoxProjectionUnits(cushion.Positions, cushion.Normals);

            var report = UvStretch.Measure(ToMm(cushion.Positions),
                ScaleBy(uv, new Vector2(Mm, Mm)), cushion.Normals, cushion.Triangles, 0.9f);

            Assert.Greater(report.Triangles, 20, "плоских треугольников слишком мало");
            Assert.AreEqual(1f, report.MinSigma, 0.12f,
                "у подушки на каждой из шести граней та же честная развёртка");
            Assert.AreEqual(1f, report.MaxSigma, 0.12f, "и тот же масштаб");
        }

        [Test]
        public void TheSensor_ActuallyFires_OnTheOldPlanUvOfASoftSlab()
        {
            var slab = ArmSlab();

            var report = UvStretch.Measure(ToMm(slab.Positions),
                ScaleBy(slab.Uvs, new Vector2(320f, 700f)), slab.Normals, slab.Triangles);

            Assert.IsTrue(report.MinSigma < 0.9f || report.MaxSigma > 1.1f,
                "прежняя развёртка «0..1 по плану» на боковых стенках валика — это растяжка в "
                + "разы; если сенсор её не видит, зелёные тесты выше — пустышка. σ: "
                + report.MinSigma + " .. " + report.MaxSigma);
        }

        [Test]
        public void TheSensor_ActuallyFires_OnTheOldPerFaceUvOfACushion()
        {
            var cushion = new CushionSurface(new Vector3(0.65f, 0.44f, 0.2f), 0.09f);

            var report = UvStretch.Measure(ToMm(cushion.Positions),
                ScaleBy(cushion.Uvs, new Vector2(650f, 440f)), cushion.Normals, cushion.Triangles);

            Assert.IsTrue(report.MinSigma < 0.9f || report.MaxSigma > 1.1f,
                "у подушки «0..1 на каждую грань» при одном ST на деталь растягивает боковые "
                + "грани тонкой подушки; сенсор обязан это видеть. σ: "
                + report.MinSigma + " .. " + report.MaxSigma);
        }

        [Test]
        public void Projection_PicksTheAxisOfTheNormal_AndDropsIt()
        {
            var positions = new[] { new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 3f) };
            var normals = new[] { Vector3.right, Vector3.up, Vector3.forward };

            var uv = PartUv.BoxProjectionUnits(positions, normals);

            Assert.AreEqual(new Vector2(2f, 3f), uv[0], "грань X: остаются Y и Z");
            Assert.AreEqual(new Vector2(3f, 1f), uv[1], "грань Y: остаются Z и X");
            Assert.AreEqual(new Vector2(1f, 2f), uv[2], "грань Z: остаются X и Y");
        }
    }
}

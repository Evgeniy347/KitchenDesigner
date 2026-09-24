using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сумма площадей треугольников по формуле полупроизведения векторного
    /// произведения — независимая от BoxRunMesh проверка «замкнутая поверхность действительно
    /// закрыта», используемая её же тестами и будущими V4b/V4c мешами.</summary>
    public class MeshAreaTests
    {
        [Test]
        public void TotalMm2_OfASingleUnitRightTriangle_IsOneHalf()
        {
            var vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            var triangles = new[] { 0, 1, 2 };

            Assert.AreEqual(0.5f, MeshArea.TotalMm2(vertices, triangles), 1e-6f,
                "катеты по 1 — площадь прямоугольного треугольника ½×1×1");
        }

        [Test]
        public void TotalMm2_OfATenByTenQuad_IsOneHundred()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f),
                new Vector3(10f, 10f, 0f), new Vector3(0f, 10f, 0f),
            };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            Assert.AreEqual(100f, MeshArea.TotalMm2(vertices, triangles), 1e-4f,
                "квадрат 10×10, разрезанный по диагонали на два треугольника — сумма 100");
        }

        [Test]
        public void TotalMm2_OfNoTriangles_IsZero()
        {
            Assert.AreEqual(0f, MeshArea.TotalMm2(new Vector3[0], new int[0]), 1e-6f,
                "нет треугольников — нет и площади, сумма по пустому индексу не должна падать");
        }
    }
}

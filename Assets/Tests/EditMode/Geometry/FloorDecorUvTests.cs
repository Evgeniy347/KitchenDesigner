using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Развёртка пола: по каким осям и от какой точки.
    ///
    /// ОСИ. Пол лежит горизонтально, поэтому его поверхность декора — мировые X и Z,
    /// а НЕ (x, y) базового класса: вторая ось пола — глубина, как у столешницы.
    /// FloorElement обязан объявить DecorSurfaceMM = (dims.x, dims.z), иначе
    /// MaterialManager.RefreshTiling поделит развёртку на толщину плиты (100 мм) и
    /// плитка вытянется в двадцать раз. Это стережёт DecorSurfaceUvTests.
    ///
    /// НОЛЬ. Меш отдаёт UV в 0..1, а физический шаг задаёт BaseMap_ST =
    /// DecorSurfaceMM / TileMM, поэтому uv * DecorSurfaceMM — это миллиметры, и
    /// вопрос только в том, ОТ ЧЕГО они отсчитаны. Ноль взят в начале МИРА, а не в
    /// точке привязки плиты: точка привязки пола — центр габарита контура
    /// (McpCommandHandler.HandleCreateFloorV2 считает cx/cz именно так), и она
    /// уезжает при любой правке контура. Привяжи развёртку к ней — и достроенный
    /// эркер сдвинет плитку по всей комнате. Мир не двигается, поэтому шов плитки
    /// стоит там, где положен, и правка контура не трогает уже уложенное.</summary>
    public class FloorDecorUvTests
    {
        private static readonly Vector2Int Surface = new Vector2Int(3000, 2000);

        private const float Tol = 0.01f;

        [Test]
        public void TopFace_TimesDecorSurface_IsTheWorldMillimetre()
        {
            var uv = FloorDecorUv.TopFace(new Vector2(-1500f, -1000f), new Vector2(5000f, 3000f), Surface);

            Assert.AreEqual(3500f, uv.x * Surface.x, Tol,
                "uv * DecorSurfaceMM — это мировые миллиметры: только тогда BaseMap_ST = "
                + "DecorSurfaceMM / TileMM даёт шаг плитки в физических мм");
            Assert.AreEqual(2000f, uv.y * Surface.y, Tol,
                "вторая ось пола — мировая Z (глубина), а не высота");
        }

        [Test]
        public void TopFace_AfterTheContourGrows_KeepsTheSameWorldMillimetre()
        {
            var before = FloorDecorUv.TopFace(new Vector2(-1500f, -1000f), new Vector2(5000f, 3000f),
                Surface);

            var wider = new Vector2Int(4000, 2000);
            var after = FloorDecorUv.TopFace(new Vector2(-2000f, -1000f), new Vector2(5500f, 3000f),
                wider);

            Assert.AreEqual(before.x * Surface.x, after.x * wider.x, Tol,
                "тот же угол комнаты (мировые 3500 мм) после пристройки эркера: габарит стал "
                + "3000→4000 мм и точка привязки уехала 5000→5500, а плитка обязана остаться "
                + "на месте — иначе правка контура перекладывает пол по всей комнате");
            Assert.AreEqual(before.y * Surface.y, after.y * wider.y, Tol);
        }

        [Test]
        public void TopFace_AnchoredAtThePivotInstead_WouldSlideWhenTheContourGrows()
        {
            var before = FloorDecorUv.TopFace(new Vector2(-1500f, -1000f), Vector2.zero, Surface);
            var after = FloorDecorUv.TopFace(new Vector2(-2000f, -1000f), Vector2.zero,
                new Vector2Int(4000, 2000));

            Assert.AreEqual(-500f, after.x * 4000f - before.x * Surface.x, Tol,
                "положительный контроль к предыдущему тесту: без мирового нуля тот же угол "
                + "комнаты уезжает на 500 мм — ровно на сдвиг центра габарита");
        }

        [Test]
        public void SideFace_TimesDecorSurface_IsMillimetresAlongThePerimeterAndUp()
        {
            var uv = FloorDecorUv.SideFace(2750f, 100f, Surface);

            Assert.AreEqual(2750f, uv.x * Surface.x, Tol,
                "торец плиты разворачивается по ДЛИНЕ обхода контура: проекция сверху "
                + "схлопнула бы его в линию и размазала один ряд пикселей по всей толщине");
            Assert.AreEqual(100f, uv.y * Surface.y, Tol, "вторая ось торца — толщина плиты");
        }

        [Test]
        public void ZeroSurface_DoesNotSendTheUvToInfinity()
        {
            var uv = FloorDecorUv.TopFace(new Vector2(500f, 500f), Vector2.zero, Vector2Int.zero);

            Assert.IsFalse(float.IsInfinity(uv.x) || float.IsNaN(uv.x),
                "чисто цветовой декор и вырожденный габарит не должны уносить UV в бесконечность");
            Assert.IsFalse(float.IsInfinity(uv.y) || float.IsNaN(uv.y));
        }
    }
}

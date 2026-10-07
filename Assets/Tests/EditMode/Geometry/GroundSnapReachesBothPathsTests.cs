using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Снэп - две реализации одной геометрии (перетаскивание и растяжение), и земля
    /// обязана доходить до обеих, иначе «прилипает, но не растягивается». Сторож читает
    /// исходники: пути входа в сцену снэпа берут геометрию через <c>ForSnapping</c>
    /// (с землёй), а голый <c>For</c> остаётся только у направляющих расстояний, которым
    /// земля как соседний ящик не нужна. <c>Diagnose</c> строит свою сцену сам и обязан
    /// просить землю у той же <c>GroundSnapGeometry.TryOffer</c>.</summary>
    public class GroundSnapReachesBothPathsTests
    {
        private static string SnapDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "Snap");

        private static string Source(string name) => SourceCorpus.Text(Path.Combine(SnapDir(), name));

        [TestCase("SnapSystem.cs")]
        [TestCase("ResizeHandleManager.cs")]
        public void SnapEntry_TakesItsSceneWithTheGround(string file)
        {
            string text = Source(file);

            StringAssert.Contains("SnapSceneGeometry.ForSnapping(", text,
                $"{file} строит сцену снэпа без земли: пустая сцена перестанет прилипать к полу");
            StringAssert.DoesNotContain("SnapSceneGeometry.For(", text,
                $"{file} зовёт голый For: земля не дойдёт до этого пути");
        }

        [Test]
        public void Diagnose_AsksTheSameFunctionForTheGround()
        {
            StringAssert.Contains("GroundSnapGeometry.TryOffer(", Source("SnapSystem.cs"),
                "Diagnose собирает сцену отдельно и обязан решать про землю той же функцией, "
                + "что и снэп, иначе оракул врёт");
        }

        [Test]
        public void OnlyTheDistanceGuides_UseTheBareScene()
        {
            var bare = new List<string>();
            foreach (var file in SourceCorpus.Files(SnapDir()))
                if (SourceCorpus.Text(file).Contains("SnapSceneGeometry.For("))
                    bare.Add(Path.GetFileName(file));

            CollectionAssert.AreEquivalent(new[] { "DistanceGuideSession.cs" }, bare,
                "голый For отдаёт сцену без земли; направляющим расстояний она не нужна, "
                + "остальным путям снэпа - нужна");
        }
    }
}

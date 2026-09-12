using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сенсор РАБОТЫ на кадре снэпа: сколько раз <c>SnapSceneGeometry.For</c>
/// собирает <c>ElementGeometry</c> заново.
///
/// Зачем он нужен, числом из живого дампа
/// (<c>test-results/perf/perf_20260912_185912.csv</c>, проект на 411 деталей):
/// маркер <c>SnapSystem.TrySnap</c> стоил 24 мс на КАЖДОМ кадре жеста, 55 кадров
/// из 56 просадок дороже 33 мс на фазе перетаскивания — и эти 24 мс уходили не в
/// отбор пар, а в то, что путь снэпа пересобирал геометрию ВСЕЙ сцены заново
/// каждый кадр. Валидация ту же геометрию уже держит и отдаёт по штампу
/// (<c>ElementSnapshotReuse</c>); снэпу оставалось её спросить.
///
/// Тест считает РАБОТУ, а не время: тесты по времени в этом репозитории не
/// годятся, а «ноль пересборок» — проверяемое утверждение. Красным он становится
/// от удаления одной ветки — обращения к <c>ElementSnapshotReuse.TryReuseGeometry</c>
/// в <c>SnapSceneGeometry.For</c>: тогда вместо нуля приходит вся сцена.
///
/// Второй сенсор здесь — ЧЕСТНОСТЬ: деталь, которую сдвинули или которой поменяли
/// габарит, обязана прийти пересобранной. Кэш геометрии без звучащего признака
/// «эта деталь не менялась» — это снэп, который липнет ко вчерашней сцене, и
/// никакой счётчик этого не видит. Признак — тот же штамп по ЗНАЧЕНИЯМ, что и у
/// валидации, и каждый способ изменить деталь перечислен в
/// <c>ValidationSnapshotReuseTests</c>; здесь проверяется, что путь снэпа этим
/// признаком действительно пользуется.</summary>
public class SnapSceneGeometryReuseTests : ElementTestBase
{
    private const int SceneSize = 12;

    private bool _suppressBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        FaceCache.Clear();
        ElementSnapshotReuse.Clear();
        _suppressBefore = KitchenElement.SuppressVisualRebuild;
        KitchenElement.SuppressVisualRebuild = true;
    }

    [TearDown]
    public void TearDown()
    {
        KitchenElement.SuppressVisualRebuild = _suppressBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
    }

    private List<KitchenElement> MakeScene()
    {
        var scene = new List<KitchenElement>(SceneSize);
        for (int i = 0; i < SceneSize; i++)
            scene.Add(MakePrimitiveElement($"Board{i}", new Vector3Int(600, 720, 18),
                new Vector3(i * 0.7f, 0.36f, 0f)));
        return scene;
    }

    private static void WarmTheValidationPass(List<KitchenElement> scene)
        => ValidationSnapshot.Build(scene, new List<ValidationElement>());

    [Test]
    public void ColdCache_BuildsEveryElement_ThatIsTheCostTheDumpNamed()
    {
        var scene = MakeScene();

        SnapSceneGeometry.For(scene, scene[0]);

        Assert.AreEqual(SceneSize, SnapSceneGeometry.ElementsInLastPass,
            "сцена до пути снэпа не доехала — считать нечего");
        Assert.AreEqual(SceneSize, SnapSceneGeometry.GeometryBuildsInLastPass,
            "без прогретого снимка снэп обязан собрать геометрию сам: это та самая "
            + "полная пересборка сцены, которая стоила 24 мс на кадр");
    }

    [Test]
    public void AfterTheValidationPass_ASnapFrameBuildsNothing()
    {
        var scene = MakeScene();
        WarmTheValidationPass(scene);

        SnapSceneGeometry.For(scene, scene[0]);

        Assert.AreEqual(SceneSize, SnapSceneGeometry.ElementsInLastPass,
            "сцена до пути снэпа не доехала — считать нечего");
        Assert.AreEqual(0, SnapSceneGeometry.GeometryBuildsInLastPass,
            "снимок прошлого кадра лежит рядом и годен — снэпу нечего пересобирать");
    }

    [Test]
    public void MovedElement_IsRebuilt_AndOnlyIt()
    {
        var scene = MakeScene();
        WarmTheValidationPass(scene);

        scene[3].transform.position += new Vector3(0.05f, 0f, 0f);
        SnapSceneGeometry.For(scene, scene[0]);

        Assert.AreEqual(1, SnapSceneGeometry.GeometryBuildsInLastPass,
            "сдвинутая деталь обязана прийти пересобранной, а её соседи — из кэша");
    }

    [Test]
    public void ResizedElement_IsRebuilt()
    {
        var scene = MakeScene();
        WarmTheValidationPass(scene);

        scene[5].DimensionsMM = new Vector3Int(900, 720, 18);
        SnapSceneGeometry.For(scene, scene[5]);

        Assert.AreEqual(1, SnapSceneGeometry.GeometryBuildsInLastPass,
            "габарит поменялся — геометрия из кэша описывает деталь, которой больше нет");
    }

    [Test]
    public void GeometryFromTheCache_AgreesWithAColdBuild()
    {
        var scene = MakeScene();
        WarmTheValidationPass(scene);

        var warm = SnapSceneGeometry.For(scene, scene[0]);

        Assert.AreEqual(SceneSize, warm.Count, "из кэша пришло не столько деталей");
        for (int i = 0; i < scene.Count; i++)
        {
            var cold = scene[i].ToGeometry();
            Assert.AreEqual(cold.Name, warm[i].Name, $"деталь {i}: имя");
            Assert.AreEqual(cold.Id, warm[i].Id, $"деталь {cold.Name}: идентификатор");
            Assert.AreEqual(cold.Min, warm[i].Min, $"деталь {cold.Name}: нижний угол");
            Assert.AreEqual(cold.Max, warm[i].Max, $"деталь {cold.Name}: верхний угол");
            Assert.AreEqual(cold.Faces.Length, warm[i].Faces.Length,
                $"деталь {cold.Name}: число граней");
            for (int f = 0; f < cold.Faces.Length; f++)
            {
                Assert.AreEqual(cold.Faces[f].center, warm[i].Faces[f].center,
                    $"деталь {cold.Name}: центр грани {f}");
                Assert.AreEqual(cold.Faces[f].normal, warm[i].Faces[f].normal,
                    $"деталь {cold.Name}: нормаль грани {f}");
            }
        }
    }
}

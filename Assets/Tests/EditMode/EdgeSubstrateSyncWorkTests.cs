using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож работы для <c>EdgeSubstrate.Sync</c> — того, что берётся за ОДНУ
/// деталь и обходил ВСЮ сцену.
///
/// Нашёл это не чтение кода, а прибор <c>SceneScanLog</c>: на создании одной
/// детали через MCP он назвал <c>EdgeSubstrate.Sync ×2</c> среди четырёх обходов
/// сцены за кадр. Зовут <c>Sync</c> шесть мест в <c>KitchenElement</c> — смена
/// размеров, три поля кромки, состояние стороны, пересборка меша, — поэтому
/// правка N деталей стоила N полных обходов. На сцене пользователя в 411 деталей
/// один обход — это 411 вызовов <c>GetFaces()</c> со свежим массивом на каждый и
/// ещё один линейный поиск детали в списке, где сравнение идёт перегруженным
/// <c>==</c> у <c>UnityEngine.Object</c>, то есть переходом в нативный код.
///
/// Лечится двумя разными ответами, и оба проверяются здесь числами:
///
/// 1. Сцена нужна НЕ ВСЕГДА. Маска голых граней без кромки, у не-листа и у детали
///    с негодной раскладкой считается по самой детали — обходов ноль, а не один.
/// 2. Когда сцена нужна, грани соседей берутся из снимка прошлого прохода
///    (<c>ElementSnapshotReuse</c>, штамп по значениям), а сама деталь находится
///    в индексе по ссылке, а не перегруженным <c>==</c>.
///
/// Маска голых граней решает, где ВИДНА подложка кромки. Устаревшая маска — это
/// молчаливая ложь на экране, которую не покажет ни один счётчик, поэтому у
/// каждого счётчика здесь есть обратный вход: соседа подвинули к детали и от
/// неё, у детали сняли кромку — маска обязана измениться.</summary>
public class EdgeSubstrateSyncWorkTests : ElementTestBase
{
    private bool _suppressBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        FaceCache.Clear();
        ElementSnapshotReuse.Clear();
        _suppressBefore = KitchenElement.SuppressVisualRebuild;
        KitchenElement.SuppressVisualRebuild = true;
        SceneFaces.TakeIndexBuilds();
        SceneFaces.TakeFaceBuilds();
        SceneFaces.TakeLinearLookups();
    }

    [TearDown]
    public void TearDown()
    {
        KitchenElement.SuppressVisualRebuild = _suppressBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
    }

    private const int SceneSize = 12;

    private List<KitchenElement> ARowOfBoards(int count, bool banded)
    {
        var scene = new List<KitchenElement>(count);
        for (int i = 0; i < count; i++)
        {
            var board = MakePrimitiveElement("B" + i, new Vector3Int(600, 720, 18),
                new Vector3(i * 2f, 0.36f, 0f));
            board.EdgeBandingEnabled = banded;
            scene.Add(board);
        }
        return scene;
    }

    private static void WarmTheValidationPass(List<KitchenElement> scene)
        => ValidationSnapshot.Build(scene, new List<ValidationElement>());

    private static int BitsIn(int mask)
    {
        int n = 0;
        for (int bit = 0; bit < 32; bit++) if ((mask & (1 << bit)) != 0) n++;
        return n;
    }

    [Test]
    public void SyncOfAnUnbandedPart_WalksTheSceneNotAtAll()
    {
        var scene = ARowOfBoards(SceneSize, banded: false);
        SceneFaces.TakeIndexBuilds();

        EdgeSubstrate.Sync(scene[0]);

        Assert.AreEqual(0, SceneFaces.TakeIndexBuilds(),
            "без кромки маска считается по самой детали — обходить сцену незачем");
    }

    [Test]
    public void SyncOfAPartThatIsNoSheet_WalksTheSceneNotAtAll()
    {
        ARowOfBoards(SceneSize, banded: true);
        var block = MakePrimitiveElement("Block", new Vector3Int(600, 720, 600),
            new Vector3(-3f, 0.36f, 0f));
        SceneFaces.TakeIndexBuilds();

        EdgeSubstrate.Sync(block);

        Assert.IsFalse(block.SupportsEdges, "деталь-брусок не лист — кромки у неё нет");
        Assert.AreEqual(0, SceneFaces.TakeIndexBuilds(),
            "у не-листа кромки нет вовсе — обход сцены здесь чистая потеря");
    }

    [Test]
    public void SyncOfABandedSheet_RebuildsNoFaces_WhenTheSnapshotIsWarm()
    {
        var scene = ARowOfBoards(SceneSize, banded: true);
        WarmTheValidationPass(scene);
        SceneFaces.TakeFaceBuilds();
        SceneFaces.TakeLinearLookups();

        EdgeSubstrate.Sync(scene[0]);

        Assert.AreEqual(0, SceneFaces.TakeFaceBuilds(),
            $"грани {SceneSize} деталей собраны заново, хотя ни одна не менялась");
        Assert.AreEqual(0, SceneFaces.TakeLinearLookups(),
            "деталь ищется в индексе по ссылке; линейный поиск сравнивал её "
            + "перегруженным == у UnityEngine.Object, то есть переходом в натив");
    }

    [Test]
    public void MovedNeighbour_IsTheOnlyOneRebuilt()
    {
        var scene = ARowOfBoards(SceneSize, banded: true);
        WarmTheValidationPass(scene);
        scene[1].transform.position += new Vector3(0.05f, 0f, 0f);
        SceneFaces.TakeFaceBuilds();

        EdgeSubstrate.Sync(scene[0]);

        Assert.AreEqual(1, SceneFaces.TakeFaceBuilds(),
            "пересобрать обязаны ровно сдвинутого соседа, остальных — взять из снимка");
    }

    [Test]
    public void NeighbourBroughtIntoContact_ClosesASide()
    {
        var scene = ARowOfBoards(2, banded: true);
        WarmTheValidationPass(scene);

        EdgeSubstrate.Sync(scene[0]);
        int apart = scene[0].BareFaceMask;

        scene[1].transform.position = new Vector3(0.6f, 0.36f, 0f);
        EdgeSubstrate.Sync(scene[0]);
        int touching = scene[0].BareFaceMask;

        Assert.AreEqual(BitsIn(apart) - 1, BitsIn(touching),
            $"сосед встал вплотную, а маска не изменилась ({apart} → {touching}): "
            + "подложка осталась видна там, где её больше не видно — кэш граней "
            + "не заметил сдвига соседа");
    }

    [Test]
    public void NeighbourTakenAway_OpensTheSideAgain()
    {
        var scene = ARowOfBoards(2, banded: true);
        scene[1].transform.position = new Vector3(0.6f, 0.36f, 0f);
        WarmTheValidationPass(scene);

        EdgeSubstrate.Sync(scene[0]);
        int touching = scene[0].BareFaceMask;

        scene[1].transform.position = new Vector3(9f, 0.36f, 0f);
        EdgeSubstrate.Sync(scene[0]);
        int apart = scene[0].BareFaceMask;

        Assert.AreEqual(BitsIn(touching) + 1, BitsIn(apart),
            $"соседа увезли, а маска осталась прежней ({touching} → {apart}): "
            + "кромка нарисована там, где торец теперь голый");
    }

    [Test]
    public void BandingTurnedOff_LeavesEveryEdgeBare()
    {
        var scene = ARowOfBoards(2, banded: true);
        scene[1].transform.position = new Vector3(0.6f, 0.36f, 0f);
        WarmTheValidationPass(scene);

        EdgeSubstrate.Sync(scene[0]);
        int banded = scene[0].BareFaceMask;

        scene[0].EdgeBandingEnabled = false;
        EdgeSubstrate.Sync(scene[0]);

        Assert.AreEqual(4, BitsIn(scene[0].BareFaceMask),
            "без кромки голы все четыре торца, сколько бы соседей их ни закрывало");
        Assert.Less(BitsIn(banded), 4,
            "фикстура собрана зря: с кромкой и соседом вплотную голых торцов "
            + "должно быть меньше четырёх");
    }
}

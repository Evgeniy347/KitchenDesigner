using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Третий заход по <c>EdgeSubstrate</c>, и назвал его снова прибор, а не
/// чтение кода: <see cref="SceneRestoreCostTests"/> поймал на восстановлении сцены
/// <b>ровно один обход сцены на каждую деталь</b> — 10 деталей 10 обходов, 60
/// деталей 60, и обходившим числился <c>EdgeSubstrate.Sync</c>. На проекте
/// пользователя в 411 деталей это 411 обходов за одну загрузку, то есть квадрат.
///
/// Прошлые два ответа здесь не работают, и это проверено, а не предположено:
/// снимка прошлого прохода на загрузке ещё нет (сцена только строится), а
/// восстановленная деталь — лист с кромкой, которому сцена нужна.
///
/// Но главное даже не цена. На загрузке сцена собирается ПО ЧАСТЯМ, и маска
/// детали, посчитанная в момент её создания, отвечает за сцену, которой ещё нет:
/// соседей, закрывающих торцы, в ней не хватает. То есть каждый из 411 обходов
/// не только дорог — он даёт ЗАВЕДОМО НЕВЕРНЫЙ ответ, который в приложении потом
/// перетирает <c>SyncScene</c> из <c>ElementHighlighter.RefreshHighlights</c> в
/// самом конце восстановления. Значит правильный ответ — не считать маски по
/// одной, а отложить их до одного прохода по готовой сцене.
///
/// Отсюда область <c>EdgeSubstrate.PostponeToOnePass</c>: внутри неё
/// <c>Sync(деталь)</c> не обходит сцену, а помечает, что маски устарели, и на
/// закрытии области один проход считает их все по ПОЛНОЙ сцене. Деталь, которой
/// сцена не нужна вовсе (без кромки, не лист), отвечает сразу и внутри области —
/// обхода там всё равно нет.
///
/// Обратный вход обязателен и он тот же, что был: после области маска каждой
/// детали обязана совпасть с посчитанной с нуля по готовой сцене. Отложенная
/// маска, которую забыли посчитать, — это подложка, нарисованная там, где её не
/// видно, и кромка на голом торце, и никакой счётчик обходов этого не покажет.
/// Направление маски (бит стоит там, где торец БЕЗ кромки) записано в
/// <see cref="EdgeSubstrateSyncOfOnePartTests"/>.</summary>
public class EdgeSubstratePostponedSyncTests : ElementTestBase
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
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
    }

    private KitchenElement ABandedBoardAt(int i)
    {
        var board = MakePrimitiveElement("B" + i, new Vector3Int(600, 720, 18),
            new Vector3(i * 0.6f, 0.36f, 0f));
        board.EdgeBandingEnabled = true;
        return board;
    }

    private List<KitchenElement> ARowOfBandedBoards(int count)
    {
        var all = new List<KitchenElement>(count);
        for (int i = 0; i < count; i++) all.Add(ABandedBoardAt(i));
        return all;
    }

    private static int BitsIn(int mask)
    {
        int n = 0;
        for (int bit = 0; bit < 32; bit++) if ((mask & (1 << bit)) != 0) n++;
        return n;
    }

    private static List<int> MasksComputedFromScratch(List<KitchenElement> all)
    {
        var scene = SceneFaces.Of(all);
        var masks = new List<int>(all.Count);
        for (int i = 0; i < all.Count; i++) masks.Add(EdgeSubstrate.BareFaceMask(all[i], scene, i));
        return masks;
    }

    [Test]
    public void WithoutTheScope_EachSyncWalksTheSceneOnItsOwn()
    {
        var all = ARowOfBandedBoards(SceneSize);

        long mark = SceneScanCounter.Scans;
        foreach (var board in all) EdgeSubstrate.Sync(board);
        long walks = SceneScanCounter.Scans - mark;

        Assert.AreEqual(SceneSize, walks,
            "это цена ДО правки, записанная числом: обход сцены на каждую деталь");
    }

    [Test]
    public void InsideTheScope_TheWholeBatchWalksTheSceneOnce()
    {
        var all = ARowOfBandedBoards(SceneSize);

        long mark = SceneScanCounter.Scans;
        using (EdgeSubstrate.PostponeToOnePass())
            foreach (var board in all) EdgeSubstrate.Sync(board);
        long walks = SceneScanCounter.Scans - mark;

        Assert.AreEqual(1, walks,
            $"{SceneSize} деталей — {walks} обходов вместо одного: откладывание не сработало, "
            + $"и на 411 деталях это снова квадрат. Кто обходил: "
            + $"[{SceneScanCounter.Since(mark)}]");
    }

    [Test]
    public void AnEmptyScope_WalksNothing()
    {
        ARowOfBandedBoards(SceneSize);

        long mark = SceneScanCounter.Scans;
        using (EdgeSubstrate.PostponeToOnePass()) { }

        Assert.AreEqual(0, SceneScanCounter.Scans - mark,
            "область, в которой никто не просил Sync, не имеет права трогать сцену");
    }

    [Test]
    public void NestedScopes_FlushOnceAtTheOuterClose()
    {
        var all = ARowOfBandedBoards(SceneSize);

        long mark = SceneScanCounter.Scans;
        using (EdgeSubstrate.PostponeToOnePass())
        {
            using (EdgeSubstrate.PostponeToOnePass())
                foreach (var board in all) EdgeSubstrate.Sync(board);

            Assert.AreEqual(0, SceneScanCounter.Scans - mark,
                "внутренняя область закрылась, а внешняя ещё открыта — считать рано");
        }

        Assert.AreEqual(1, SceneScanCounter.Scans - mark,
            "проход обязан быть один и на закрытии ВНЕШНЕЙ области");
    }

    [Test]
    public void APartThatNeedsNoScene_IsAnsweredInsideTheScope_WithoutAWalk()
    {
        ARowOfBandedBoards(SceneSize);
        var unbanded = MakePrimitiveElement("Plain", new Vector3Int(600, 720, 18),
            new Vector3(-2f, 0.36f, 0f));

        long mark = SceneScanCounter.Scans;
        using (EdgeSubstrate.PostponeToOnePass()) EdgeSubstrate.Sync(unbanded);
        long walks = SceneScanCounter.Scans - mark;

        Assert.AreEqual(0, walks, "детали без кромки сцена не нужна ни внутри области, ни вне её");
        Assert.AreEqual(4, BitsIn(unbanded.BareFaceMask),
            "без кромки голы все четыре торца, и ответ этот известен сразу");
    }

    [Test]
    public void AfterTheScope_EveryMaskEqualsAFreshComputation()
    {
        var all = ARowOfBandedBoards(SceneSize);

        using (EdgeSubstrate.PostponeToOnePass())
            foreach (var board in all) EdgeSubstrate.Sync(board);

        var fromScratch = MasksComputedFromScratch(all);
        for (int i = 0; i < all.Count; i++)
            Assert.AreEqual(fromScratch[i], all[i].BareFaceMask,
                $"деталь {all[i].PartName}: отложенная маска {all[i].BareFaceMask} разошлась "
                + $"с посчитанной заново {fromScratch[i]} — подложка нарисована не там");
    }

    [Test]
    public void APartBornBeforeItsNeighbours_StillSeesThem_WhenTheScopeCloses()
    {
        var all = new List<KitchenElement>(SceneSize);
        using (EdgeSubstrate.PostponeToOnePass())
        {
            for (int i = 0; i < SceneSize; i++)
            {
                all.Add(ABandedBoardAt(i));
                EdgeSubstrate.Sync(all[i]);
            }
        }

        Assert.AreEqual(1, BitsIn(all[0].BareFaceMask),
            "первая деталь считалась, когда соседа справа ещё не было; маска обязана "
            + "отвечать за ГОТОВУЮ сцену, а не за ту, что была в момент её рождения");
        Assert.AreEqual(2, BitsIn(all[SceneSize / 2].BareFaceMask),
            "у детали в середине ряда закрыты оба длинных торца");
    }
}

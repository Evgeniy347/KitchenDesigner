using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Прибор, названный дампом <c>perf_20260913_080209.csv</c>: семнадцать кадров
/// дороже 94 мс, все — клики по детали на сцене в 411 деталей. Сумма ВСЕХ маркеров такого
/// кадра — 21 мс (из них 17,3 приходится на <c>ConstraintValidator.Validate</c>), а
/// <c>main_ms</c> = 200–214. То есть девять десятых кадра не измеряет никто.
///
/// Клик по детали кончается одним событием — <c>SelectionManager.OnSelectionChanged</c>, —
/// и на нём висят пять подписчиков из четырёх слоёв (<c>ContextMenuUI</c>,
/// <c>ResizeHandleManager</c>, <c>HierarchyPanelUI</c>, <c>GroupMenuUI</c>,
/// <c>ElementMover</c>). Общий маркер на рассылку назвал бы сумму, но не виновника, а
/// заводить маркер с ИМЕНЕМ чужого класса нельзя: <c>PerfMarkerCoverageTests</c> требует,
/// чтобы имя маркера называло файл, в котором он замеряется. Поэтому цена каждого
/// подписчика считается по месту рассылки и складывается здесь — по имени типа.
///
/// Тесты держат ровно то, ради чего прибор существует: одно имя — одна строка (иначе пять
/// вызовов одного подписчика читаются как пять разных виновников), порядок по убыванию
/// (первым стоит тот, ради кого дамп и открывают), и <c>Take</c> отдаёт кадр И обнуляет —
/// иначе следующий кадр унаследует чужие миллисекунды и обвинит невиновного.</summary>
public class ListenerCostLogTests
{
    [SetUp]
    public void SetUp() => ListenerCostLog.Forget();

    [TearDown]
    public void TearDown() => ListenerCostLog.Forget();

    [Test]
    public void AnEmptyFrame_SaysNothing()
    {
        Assert.AreEqual(string.Empty, ListenerCostLog.Take(),
            "кадр без рассылки не имеет права печатать строку о слушателях");
    }

    [Test]
    public void OneListener_IsNamedWithItsMilliseconds()
    {
        ListenerCostLog.Note("ContextMenuUI", 92.5f);

        Assert.AreEqual("ContextMenuUI 92.50мс", ListenerCostLog.Take());
    }

    [Test]
    public void RepeatsOfOneListener_CollapseIntoOneLine_WithTheSumAndTheCount()
    {
        ListenerCostLog.Note("ResizeHandleManager", 4.0f);
        ListenerCostLog.Note("ResizeHandleManager", 6.0f);

        Assert.AreEqual("ResizeHandleManager 10.00мс ×2", ListenerCostLog.Take(),
            "пять вызовов одного подписчика — это один виновник, а не пять");
    }

    [Test]
    public void TheDearestListener_ComesFirst()
    {
        ListenerCostLog.Note("ElementMover", 0.1f);
        ListenerCostLog.Note("ContextMenuUI", 90f);
        ListenerCostLog.Note("ResizeHandleManager", 8f);

        var line = ListenerCostLog.Take();

        Assert.IsTrue(line.StartsWith("ContextMenuUI 90.00мс", System.StringComparison.Ordinal),
            "дамп открывают ради первого имени в строке: " + line);
        Assert.Less(line.IndexOf("ResizeHandleManager", System.StringComparison.Ordinal),
            line.IndexOf("ElementMover", System.StringComparison.Ordinal),
            "порядок по убыванию цены, а не по порядку вызова: " + line);
    }

    [Test]
    public void Take_EmptiesTheFrame_SoTheNextOneDoesNotInheritTheseMilliseconds()
    {
        ListenerCostLog.Note("ContextMenuUI", 90f);
        ListenerCostLog.Take();

        Assert.AreEqual(string.Empty, ListenerCostLog.Take(),
            "унаследованные миллисекунды обвинили бы подписчика в кадре, где он не работал");
    }

    [Test]
    public void AListenerWithoutAName_IsStillCounted_AsAQuestionMark()
    {
        ListenerCostLog.Note(null, 3f);

        Assert.AreEqual("? 3.00мс", ListenerCostLog.Take(),
            "безымянный подписчик обязан остаться в сумме кадра: пропажа строки читается "
            + "как «этой работы не было»");
    }

    [Test]
    public void ListenersBeyondTheLimit_AreCountedRatherThanDropped()
    {
        for (int i = 0; i < ListenerCostLog.MostListenersRemembered + 3; i++)
            ListenerCostLog.Note("Слушатель" + i, 1f);

        StringAssert.Contains("и ещё 3 сверх предела", ListenerCostLog.Take(),
            "молча забытые подписчики превращают неполную строку в полную ложь");
    }
}

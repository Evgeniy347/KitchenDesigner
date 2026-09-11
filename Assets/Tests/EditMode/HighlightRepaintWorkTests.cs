using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож работы для <see cref="ElementHighlighter"/>. Пользователь сказал про
/// перекраску соседей: «нужна, оставляем — но ускорить… мы же текущий элемент красим сразу,
/// потому что знаем, что есть пересечение, соответственно проход по списку — линейная
/// операция». Здесь и проверяется ровно это требование: за один проход перекрашены те детали,
/// которые СМЕНИЛИ состояние, а не все подряд.
///
/// Профиль (test-results/perf/perf_20260911_191534.csv) показал, что `RefreshHighlights` идёт
/// покадрово и стоит 65,5 мс, из которых 63,6 — `ConstraintValidator.Validate`. То есть сама
/// покраска уже почти даровая: `PaintBody` пропускает рендерер, который и так носит нужный
/// материал. Этот сенсор держит это свойство, чтобы оно не пропало, и называет цифру, которой
/// дорожить: перекрашено обязано быть НОЛЬ, когда в сцене ничего не изменилось.
///
/// Заодно убрана линейная выборка: принадлежность к списку нарушителей спрашивалась через
/// `List.Contains` на каждую деталь, то есть O(деталей × нарушителей); теперь это множество.</summary>
public class HighlightRepaintWorkTests : ElementTestBase
{
    private ElementHighlighter? _highlighter;
    private ElementHighlighter? _before;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        var go = new GameObject("ElementHighlighter");
        _spawned.Add(go);
        _highlighter = go.AddComponent<ElementHighlighter>();
        _before = ElementHighlighter.Instance;
        ElementHighlighter.Instance = _highlighter;
        ElementHighlighter.TakeBodiesVisited();
        ElementHighlighter.TakeBodiesRepainted();
    }

    [TearDown]
    public void TearDown()
    {
        ElementHighlighter.Instance = _before;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementHighlighter.TakeBodiesVisited();
        ElementHighlighter.TakeBodiesRepainted();
    }

    private KitchenElement TwoBoardsApart()
    {
        var a = MakePrimitiveElement("A", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("B", new Vector3Int(600, 720, 18),
            new Vector3(0.6f, 0.36f, 0f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));
        return a;
    }

    /// <summary>Главный сенсор: второй проход по неизменившейся сцене не перекрашивает ничего.
    /// Обойти детали он обязан — обход и есть та линейная операция, о которой говорит
    /// пользователь, — но ни одной СМЕНЫ материала быть не должно.</summary>
    [Test]
    public void RefreshingAnUnchangedScene_RepaintsNothing()
    {
        TwoBoardsApart();
        _highlighter!.RefreshHighlights();
        ElementHighlighter.TakeBodiesVisited();
        ElementHighlighter.TakeBodiesRepainted();

        _highlighter.RefreshHighlights();

        Assert.Greater(ElementHighlighter.TakeBodiesVisited(), 0,
            "положительный контроль обхода: детали обязаны быть просмотрены, иначе «ноль "
            + "перекрасок» значит «проход не состоялся»");
        Assert.AreEqual(0, ElementHighlighter.TakeBodiesRepainted(),
            "в сцене не изменилось ничего — менять материал не у кого");
    }

    /// <summary>Положительный контроль к нулю выше: деталь, которая СТАЛА нарушителем, обязана
    /// быть перекрашена, и только она со своим соседом по пересечению — не вся сцена.</summary>
    [Test]
    public void WhenOnePairStartsOverlapping_OnlyThatPairIsRepainted()
    {
        var a = TwoBoardsApart();
        _highlighter!.RefreshHighlights();
        ElementHighlighter.TakeBodiesRepainted();

        a.transform.position = new Vector3(0.58f, 0.36f, 0f);
        _highlighter.RefreshHighlights();

        int repainted = ElementHighlighter.TakeBodiesRepainted();
        int visited = ElementHighlighter.TakeBodiesVisited();
        Assert.Greater(repainted, 0,
            "пересечение появилось — нарушители обязаны покраснеть");
        Assert.Less(repainted, visited,
            $"перекрашено {repainted} из {visited}: краснеет пара, а не вся сцена");
    }

    /// <summary>И третий проход после того же пересечения снова стоит ноль перекрасок:
    /// покраска не имеет права переставлять один и тот же материал по кругу.</summary>
    [Test]
    public void RefreshingAgainAfterTheOverlapAppeared_RepaintsNothingMore()
    {
        var a = TwoBoardsApart();
        _highlighter!.RefreshHighlights();
        a.transform.position = new Vector3(0.58f, 0.36f, 0f);
        _highlighter.RefreshHighlights();
        ElementHighlighter.TakeBodiesRepainted();

        _highlighter.RefreshHighlights();

        Assert.AreEqual(0, ElementHighlighter.TakeBodiesRepainted(),
            "состояние не менялось со прошлого прохода — и материал менять не за чем");
    }
}

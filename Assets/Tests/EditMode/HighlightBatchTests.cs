using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Почему этот класс существует: <c>ElementRoot.Publish</c> дёргает
/// <c>RefreshHighlights</c> на КАЖДУЮ созданную деталь, а один такой вызов —
/// это полная <c>ConstraintValidator.Validate</c> плюс
/// <c>EdgeSubstrate.SyncScene</c> по всей сцене. На загрузке проекта это
/// превращалось в N полных проходов по растущей сцене: замер
/// <see cref="ProjectLoadPerfTests"/> на 274 деталях дал 274 вызова и 6,5 с
/// в цикле восстановления против ~0,9 с без подсветки. Пользователь видел это
/// как «после заставки небо и горизонт, потом 6–7 секунд ничего».
///
/// <see cref="HighlightBatch"/> — заслонка: пока открыт scope, вызовы
/// откладываются, на закрытии выполняется РОВНО ОДИН.
/// </summary>
public class HighlightBatchTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ElementHighlighter? _highlighter;

    [SetUp]
    public void SetUp()
    {
        HighlightBatch.Reset();
        var go = new GameObject("ElementHighlighter");
        _spawned.Add(go);
        _highlighter = go.AddComponent<ElementHighlighter>();
        ElementHighlighter.Instance = _highlighter;
    }

    [TearDown]
    public void TearDown()
    {
        HighlightBatch.Reset();
        ElementHighlighter.Instance = null;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement CreateElement(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(800, 400, 18);
        _spawned.Add(go);
        return element;
    }

    [Test]
    public void Suspended_IsFalse_OutsideAnyScope()
    {
        Assert.IsFalse(HighlightBatch.Suspended);
    }

    [Test]
    public void OpenScope_SwallowsEveryRefresh_AndRunsOneOnClose()
    {
        int before = _highlighter!.RefreshCount;

        using (HighlightBatch.Open())
        {
            for (int i = 0; i < 5; i++) _highlighter.RefreshHighlights();
            Assert.AreEqual(before, _highlighter.RefreshCount,
                "внутри пакета обновление подсветки не должно выполняться");
        }

        Assert.AreEqual(before + 1, _highlighter.RefreshCount,
            "на закрытии пакета обновление должно пройти ровно один раз");
    }

    [Test]
    public void OpenScope_WithoutAnyRequest_DoesNotRefreshOnClose()
    {
        int before = _highlighter!.RefreshCount;

        using (HighlightBatch.Open()) { }

        Assert.AreEqual(before, _highlighter.RefreshCount,
            "пакет без единого запроса не должен придумывать обновление сам");
    }

    [Test]
    public void NestedScopes_RefreshOnlyWhenTheOutermostCloses()
    {
        int before = _highlighter!.RefreshCount;

        using (HighlightBatch.Open())
        {
            using (HighlightBatch.Open())
            {
                _highlighter.RefreshHighlights();
            }

            Assert.AreEqual(before, _highlighter.RefreshCount,
                "внутренний пакет не закрывает внешний");
        }

        Assert.AreEqual(before + 1, _highlighter.RefreshCount);
    }

    /// <summary>Путь пользователя: открыть проект. Без заслонки счётчик равен
    /// числу деталей плюс один — именно это и было тормозом загрузки.</summary>
    [Test]
    public void RestoreScene_RefreshesHighlightsOnce_NotPerElement()
    {
        var sources = new List<KitchenElement>();
        for (int i = 0; i < 6; i++)
            sources.Add(CreateElement("Board_" + i, new Vector3(i, 0f, 0f)));

        var data = SaveLoadManager.CaptureScene(sources);
        Assert.AreEqual(6, data.elements.Length);

        int before = _highlighter!.RefreshCount;
        var created = SaveLoadManager.RestoreScene(data);

        Assert.AreEqual(6, created.Count, "сцена должна восстановиться целиком");
        Assert.AreEqual(before + 1, _highlighter.RefreshCount,
            "восстановление сцены обязано обновлять подсветку один раз, а не на каждую деталь");
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>M2: <c>LevelSwitch.To</c> звало только <c>SceneVisibilityManager.Invalidate</c> —
/// приглушение соседнего этажа (<see cref="ElementHighlighter"/>) само по себе не значилось в
/// списке слушателей смены этажа и оставалось прежним до следующей ПОСТОРОННЕЙ правки сцены,
/// которая случайно дёргала <c>RefreshHighlights</c>.</summary>
public class LevelSwitchTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ElementHighlighter? _highlighter;
    private KitchenSettingsData? _backup;

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s, "Resources/KitchenSettings.asset не найден");
        _backup = s.ToData();
        s.ResetToDefaults();
        s.NeighbourLevels = NeighbourLevelsMode.Dim;

        var host = new GameObject("Highlighter");
        _spawned.Add(host);
        _highlighter = host.AddComponent<ElementHighlighter>();

        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        LevelRegistry.CurrentId = "1";
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var g in _spawned)
            if (g != null) Object.DestroyImmediate(g);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        LevelRegistry.Reset();
        SceneVisibilityManager.Invalidate();
        ValidityTint.Clear();
        MaterialManager.ClearCache();

        var s = KitchenSettings.Instance;
        if (s != null && _backup != null) s.ApplyFrom(_backup);
        _backup = null;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void To_RepaintsDimmingOfTheLevelLeftBehind_WithoutAnExplicitRefresh()
    {
        // Одинокая стена, а не деталь: деталь без опоры под собой сама по себе
        // нарушение (см. ElementHighlighterTests.PlainWall), и тест проверял бы
        // снятие тонировки нарушения, а не приглушение по этажу.
        var go = Spawn(ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Wall", Vector3.zero));
        var element = go.GetComponent<KitchenElement>()!;
        element.LevelId = "1";
        var own = go.GetComponent<MeshRenderer>()!.sharedMaterial;

        _highlighter!.RefreshHighlights();
        Assert.AreEqual(own, go.GetComponent<MeshRenderer>()!.sharedMaterial,
            "предпосылка: на СВОЁМ (текущем) этаже деталь не притонирована");

        LevelSwitch.To("2");

        Assert.AreNotEqual(own, go.GetComponent<MeshRenderer>()!.sharedMaterial,
            "переключение на второй этаж обязано приглушить деталь первого немедленно — "
            + "«Приглушать» уже стоит в настройках, менять нужно только текущий этаж");
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class NameDropdownBinderTests
{
    private const string None = "(не выбрано)";

    private Canvas? _canvas;
    private TMP_Dropdown? _dropdown;
    private NameDropdownBinder? _binder;
    private List<string> _candidates = new List<string>();
    private string _current = "";
    private bool _detached;
    private readonly List<string> _committed = new List<string>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        _dropdown = UIFactory.CreateDropdown("Dd", _canvas!.transform, new List<string> { None },
            Vector2.zero, new Vector2(200, 28), _ => { });
        _candidates = new List<string>();
        _current = "";
        _detached = false;
        _committed.Clear();
        _binder = new NameDropdownBinder(_dropdown!, None,
            () => _current, () => _candidates, () => _detached,
            name => { _committed.Add(name); _current = name; });
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    private List<string> Options()
    {
        var texts = new List<string>();
        foreach (var o in _dropdown!.options) texts.Add(o.text);
        return texts;
    }

    [Test]
    public void Rebuild_PutsTheNoneCaptionFirst()
    {
        _candidates.Add("Полка");
        _binder!.Rebuild();
        Assert.AreEqual(None, Options()[0],
            "нулевой пункт — «не выбрано»: индексация значений начинается с единицы");
    }

    [Test]
    public void Rebuild_ListsOnlyTheOfferedCandidates()
    {
        _candidates.AddRange(new[] { "Полка", "Боковина" });
        _binder!.Rebuild();
        CollectionAssert.AreEqual(new[] { None, "Полка", "Боковина" }, Options());
    }

    [Test]
    public void Rebuild_KeepsTheCurrentName_EvenWhenItIsNotACandidate()
    {
        _candidates.Add("Полка");
        _current = "Удалённая";
        _binder!.Rebuild();
        CollectionAssert.Contains(Options(), "Удалённая",
            "родителя удалили (или сборка разъехалась), а имя осталось: без принудительного "
            + "пункта список молча сбросился бы на «не выбрано» и связь пропала бы при первом же выборе");
    }

    [Test]
    public void Rebuild_DoesNotDuplicateTheCurrentName_WhenItIsAlsoACandidate()
    {
        _candidates.AddRange(new[] { "Полка", "Боковина" });
        _current = "Полка";
        _binder!.Rebuild();
        CollectionAssert.AreEqual(new[] { None, "Полка", "Боковина" }, Options());
    }

    [Test]
    public void SetValue_SelectsThatName()
    {
        _candidates.AddRange(new[] { "Полка", "Боковина" });
        _binder!.Rebuild();
        _binder!.SetValue("Боковина");
        Assert.AreEqual(2, _dropdown!.value);
    }

    [Test]
    public void SetValue_UnknownName_FallsBackToNone()
    {
        _candidates.Add("Полка");
        _binder!.Rebuild();
        _binder!.SetValue("Чужая");
        Assert.AreEqual(0, _dropdown!.value);
    }

    [Test]
    public void Select_CommitsTheChosenName()
    {
        _candidates.AddRange(new[] { "Полка", "Боковина" });
        _binder!.Rebuild();

        _binder!.Select(2);

        CollectionAssert.AreEqual(new[] { "Боковина" }, _committed);
    }

    [Test]
    public void Select_NoneItem_CommitsAnEmptyName()
    {
        _candidates.Add("Полка");
        _current = "Полка";
        _binder!.Rebuild();

        _binder!.Select(0);

        CollectionAssert.AreEqual(new[] { "" }, _committed,
            "выбор «не выбрано» — это снятие связи, а не отсутствие действия");
    }

    [Test]
    public void Caption_TurnsRed_WhenTheLinkIsBroken()
    {
        _candidates.Add("Полка");
        _current = "Полка";
        _binder!.Rebuild();
        _binder!.SetValue("Полка");
        var normal = _dropdown!.captionText.color;

        _detached = true;
        _binder!.UpdateCaptionColor();

        Assert.AreEqual(Color.red, _dropdown!.captionText.color,
            "разъехавшаяся связь обязана быть видна сразу: иначе сборка выглядит целой, "
            + "а деталь за родителем уже не едет");

        _detached = false;
        _binder!.UpdateCaptionColor();
        Assert.AreEqual(normal, _dropdown!.captionText.color, "связь восстановлена — цвет обычный");
    }

    [Test]
    public void UserPickingAnItem_GoesThroughTheBinder()
    {
        _candidates.AddRange(new[] { "Полка", "Боковина" });
        _binder!.Rebuild();

        _dropdown!.value = 1;

        CollectionAssert.AreEqual(new[] { "Полка" }, _committed,
            "выбор мышью обязан применяться сам — кнопки «Применить» у списка нет (правило 2)");
    }
}

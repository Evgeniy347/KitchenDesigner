using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Переключатель этажей на тулбаре. С одним этажом ▲, подпись и ▼ не нужны и только
/// съедают ширину: остаётся одна кнопка «Этажи…». С двумя и больше этажами видно всё, но кнопка,
/// у которой нет хода (▲ на верхнем этаже, ▼ на нижнем), выключена, а не молча ничего не делает
/// (UI-GUIDELINES §9, «Кнопка, нажатие которой в этом состоянии ничего не сделает, выключена»). Состояние обновляется само на
/// каждое изменение набора этажей, на отмену/возврат, загрузку проекта и переключение.</summary>
public class ToolbarLevelSwitcherTests
{
    private sealed class FakeToolbarHost : IToolbarHost
    {
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => false;
        public void SaveCurrent() { }
        public void NewProject() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private GameObject? _canvasGo;
    private ToolbarUI _toolbar = null!;
    private Transform _bar = null!;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        CommandStack.Clear();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    private void Build()
    {
        _toolbar = new ToolbarUI();
        _toolbar.Build(_canvasGo!.transform, new FakeToolbarHost());
        _bar = _canvasGo.transform.Find("Toolbar")!;
        _toolbar.Refresh();
    }

    private static Level[] Floors(int count)
    {
        var levels = new Level[count];
        for (int i = 0; i < count; i++)
            levels[i] = new Level((i + 1).ToString(), (i + 1) + " этаж", i * 3000, 3000);
        return levels;
    }

    private Button ButtonOf(string name) => _bar.Find(name)!.GetComponent<Button>();

    private bool Shown(string name) => _bar.Find(name)!.gameObject.activeSelf;

    private float X(string name) => ((RectTransform)_bar.Find(name)!).anchoredPosition.x;

    private TMP_Text Label() => _bar.Find("LevelLabel")!.GetComponent<TMP_Text>();

    private void AssertOnlyTheLevelsButton()
    {
        Assert.IsFalse(Shown("LevelUp"), "с одним этажом ▲ скрыта");
        Assert.IsFalse(Shown("LevelLabel"), "с одним этажом подпись скрыта");
        Assert.IsFalse(Shown("LevelDown"), "с одним этажом ▼ скрыта");
        Assert.IsTrue(Shown("LevelsWindow"), "кнопка «Этажи…» остаётся — через неё этаж добавляют");
    }

    private void AssertAllFour()
    {
        Assert.IsTrue(Shown("LevelUp"), "с несколькими этажами ▲ видна");
        Assert.IsTrue(Shown("LevelLabel"), "с несколькими этажами подпись видна");
        Assert.IsTrue(Shown("LevelDown"), "с несколькими этажами ▼ видна");
        Assert.IsTrue(Shown("LevelsWindow"));
    }

    [Test]
    public void OneLevel_ShowsOnlyTheLevelsButton()
    {
        LevelRegistry.Set(Floors(1));
        Build();

        AssertOnlyTheLevelsButton();
    }

    [Test]
    public void NoLevelsInProject_ShowsOnlyTheLevelsButton()
    {
        Build();

        AssertOnlyTheLevelsButton();
    }

    [Test]
    public void TwoLevels_ShowArrowsLabelAndLevelsButton()
    {
        LevelRegistry.Set(Floors(2));
        Build();

        AssertAllFour();
        Assert.AreEqual("1 этаж", Label().text);
    }

    [Test]
    public void TopLevel_UpIsDisabled_DownIsEnabled()
    {
        LevelRegistry.Set(Floors(3));
        LevelRegistry.CurrentId = "3";
        Build();

        Assert.IsFalse(ButtonOf("LevelUp").interactable, "выше верхнего этажа хода нет");
        Assert.IsTrue(ButtonOf("LevelDown").interactable);
    }

    [Test]
    public void BottomLevel_DownIsDisabled_UpIsEnabled()
    {
        LevelRegistry.Set(Floors(3));
        LevelRegistry.CurrentId = "1";
        Build();

        Assert.IsTrue(ButtonOf("LevelUp").interactable);
        Assert.IsFalse(ButtonOf("LevelDown").interactable, "ниже нижнего этажа хода нет");
    }

    [Test]
    public void MiddleLevel_BothEnabled()
    {
        LevelRegistry.Set(Floors(3));
        LevelRegistry.CurrentId = "2";
        Build();

        Assert.IsTrue(ButtonOf("LevelUp").interactable);
        Assert.IsTrue(ButtonOf("LevelDown").interactable);
    }

    [Test]
    public void LevelSwitchUp_OnTheTopLevel_ChangesNothing()
    {
        LevelRegistry.Set(Floors(2));
        LevelRegistry.CurrentId = "2";

        LevelSwitch.Up();

        Assert.AreEqual("2", LevelRegistry.CurrentId, "LevelSwitch.Up на верхнем этаже не имеет права никуда уводить");
    }

    [Test]
    public void SingleLevelFlow_ClosesTheGap_LevelsButtonSitsWhereUpArrowWas()
    {
        LevelRegistry.Set(Floors(2));
        Build();
        float upX = X("LevelUp");
        float levelsXWithArrows = X("LevelsWindow");
        float handleXWithArrows = X("HandleMode");

        LevelRegistry.Set(Floors(1));
        _toolbar.Refresh();

        Assert.AreEqual(upX, X("LevelsWindow"), 0.01f, "«Этажи…» занимает место скрытой ▲ — дыры в тулбаре быть не должно");
        Assert.AreEqual(handleXWithArrows - (levelsXWithArrows - upX), X("HandleMode"), 0.01f,
            "всё, что правее переключателя, сдвигается ровно на освободившуюся ширину");
    }

    [Test]
    public void AddingASecondLevel_ShowsTheArrowsAtOnce()
    {
        LevelRegistry.Set(Floors(1));
        Build();
        AssertOnlyTheLevelsButton();

        CommandStack.Execute(CreateLevelCommand.AboveTop());
        _toolbar.Refresh();

        AssertAllFour();
    }

    [Test]
    public void DeletingTheSecondLevel_HidesTheArrowsAtOnce()
    {
        LevelRegistry.Set(Floors(2));
        Build();
        AssertAllFour();

        CommandStack.Execute(new DeleteLevelCommand("2"));
        _toolbar.Refresh();

        AssertOnlyTheLevelsButton();
    }

    [Test]
    public void UndoAndRedoOfLevelCreation_FlipTheSwitcherBothWays()
    {
        LevelRegistry.Set(Floors(1));
        Build();
        CommandStack.Execute(CreateLevelCommand.AboveTop());
        _toolbar.Refresh();
        AssertAllFour();

        CommandStack.Undo();
        _toolbar.Refresh();
        AssertOnlyTheLevelsButton();

        CommandStack.Redo();
        _toolbar.Refresh();
        AssertAllFour();
    }

    [Test]
    public void RenamingTheCurrentLevel_UpdatesTheLabel_AlsoOnUndo()
    {
        LevelRegistry.Set(Floors(2));
        Build();

        CommandStack.Execute(new RenameLevelCommand(LevelRegistry.Current, "Цоколь"));
        _toolbar.Refresh();
        Assert.AreEqual("Цоколь", Label().text);

        CommandStack.Undo();
        _toolbar.Refresh();
        Assert.AreEqual("1 этаж", Label().text);
    }

    [Test]
    public void SwitchingLevel_MovesTheLabelAndTheDisabledArrow()
    {
        LevelRegistry.Set(Floors(2));
        Build();
        Assert.IsFalse(ButtonOf("LevelDown").interactable);

        LevelSwitch.Up();
        _toolbar.Refresh();

        Assert.AreEqual("2 этаж", Label().text);
        Assert.IsFalse(ButtonOf("LevelUp").interactable);
        Assert.IsTrue(ButtonOf("LevelDown").interactable);
    }

    [Test]
    public void ProjectLoad_ReplacingTheLevelSet_RebuildsTheSwitcher()
    {
        LevelRegistry.Set(Floors(1));
        Build();
        AssertOnlyTheLevelsButton();

        LevelRegistry.Set(Floors(4));
        LevelRegistry.CurrentId = "";
        _toolbar.Refresh();

        AssertAllFour();
        Assert.IsTrue(ButtonOf("LevelUp").interactable);
        Assert.IsFalse(ButtonOf("LevelDown").interactable, "загруженный проект открывается на нижнем этаже");
    }

    [Test]
    public void DeletingTheCurrentLevel_FallsBackAndKeepsTheSwitcherConsistent()
    {
        LevelRegistry.Set(Floors(3));
        LevelRegistry.CurrentId = "3";
        Build();
        Assert.IsFalse(ButtonOf("LevelUp").interactable);

        CommandStack.Execute(new DeleteLevelCommand("3"));
        _toolbar.Refresh();

        var state = LevelSwitcherState.Of(LevelRegistry.Snapshot(), LevelRegistry.CurrentId);
        Assert.AreEqual(state.CanGoUp, ButtonOf("LevelUp").interactable);
        Assert.AreEqual(state.CanGoDown, ButtonOf("LevelDown").interactable);
    }

    private static float RightEdgeOfFlow(Transform bar)
    {
        float maxRight = 0f;
        foreach (Transform child in bar)
        {
            if (child.name == "Music" || !child.gameObject.activeSelf) continue;
            var rt = (RectTransform)child;
            float right = rt.anchoredPosition.x + rt.sizeDelta.x;
            if (right > maxRight) maxRight = right;
        }
        return maxRight;
    }

    [TestCase(1, 1366f)]
    [TestCase(2, 1366f)]
    [TestCase(5, 1366f)]
    [TestCase(1, 1920f)]
    [TestCase(2, 1920f)]
    public void Toolbar_StillFitsBeforeTheMusicButton_InBothSwitcherStates(int levels, float screenWidth)
    {
        LevelRegistry.Set(Floors(levels));
        Build();
        var music = (RectTransform)_bar.Find("Music")!;
        float budget = screenWidth - (-music.anchoredPosition.x + music.sizeDelta.x);

        float flowRight = RightEdgeOfFlow(_bar);

        Assert.Less(flowRight, budget,
            $"{levels} этаж(ей), экран {screenWidth}px: поток доходит до {flowRight:0}px при бюджете {budget:0}px");
    }

    [Test]
    public void SingleLevelToolbar_IsNarrowerThanTheMultiLevelOne_ByTheHiddenGroup()
    {
        LevelRegistry.Set(Floors(2));
        Build();
        float multi = RightEdgeOfFlow(_bar);

        LevelRegistry.Set(Floors(1));
        _toolbar.Refresh();
        float single = RightEdgeOfFlow(_bar);

        Assert.Less(single, multi - 100f, "скрытые ▲, подпись и ▼ обязаны отдать свою ширину потоку");
    }
}

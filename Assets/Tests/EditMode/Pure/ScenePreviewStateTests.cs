using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Порядок переходов призрак-превью: навёл — построить, перевёл на
/// соседний пункт — пересобрать, ушёл — убрать, выбрал — применить по-настоящему.
///
/// Состояние отделено от сцены нарочно. Промах именно тут стоит дороже всего:
/// «навёл на тот же пункт» без ответа «ничего не делать» пересоздавал бы объект
/// каждый кадр, а `Leave` без памяти о том, что показано, гасил бы заменяемый
/// объект навсегда — превью ушло, а дырка в сцене осталась.</summary>
public class ScenePreviewStateTests
{
    [Test]
    public void Hover_OnAFreshState_AsksToBuild()
    {
        var state = new ScenePreviewState();

        Assert.AreEqual(ScenePreviewStep.Build, state.Hover("dn25"),
            "первое наведение обязано построить призрака: без него в сцене не видно, "
            + "что именно предлагает пункт списка");
        Assert.IsTrue(state.IsShowing);
        Assert.AreEqual("dn25", state.ShownKey);
    }

    [Test]
    public void Hover_OnTheSameItemAgain_DoesNothing()
    {
        var state = new ScenePreviewState();
        state.Hover("dn25");

        Assert.AreEqual(ScenePreviewStep.None, state.Hover("dn25"),
            "мышь шевелится над пунктом каждый кадр: без этого ответа призрак "
            + "пересоздавался бы шестьдесят раз в секунду и мигал");
    }

    [Test]
    public void Hover_OnTheNextItem_AsksToRebuild()
    {
        var state = new ScenePreviewState();
        state.Hover("dn25");

        Assert.AreEqual(ScenePreviewStep.Rebuild, state.Hover("dn32"),
            "переход на соседний пункт — это ДРУГОЙ кандидат: старый призрак надо снять, "
            + "иначе в сцене окажутся оба");
        Assert.AreEqual("dn32", state.ShownKey);
    }

    [Test]
    public void Leave_AfterHover_AsksToClear()
    {
        var state = new ScenePreviewState();
        state.Hover("dn25");

        Assert.AreEqual(ScenePreviewStep.Clear, state.Leave(),
            "ушли с пункта — сцена обязана вернуться как была");
        Assert.IsFalse(state.IsShowing);
        Assert.IsNull(state.ShownKey);
    }

    [Test]
    public void Leave_WithNothingShown_DoesNothing()
    {
        var state = new ScenePreviewState();

        Assert.AreEqual(ScenePreviewStep.None, state.Leave(),
            "выход из пустого списка не должен трогать сцену: лишний Clear зажёг бы "
            + "рендереры, которые погасил кто-то другой");
    }

    [Test]
    public void Hover_OnNothing_IsTheSameAsLeaving()
    {
        var state = new ScenePreviewState();
        state.Hover("dn25");

        Assert.AreEqual(ScenePreviewStep.Clear, state.Hover(null),
            "пустой ключ — это «ни на чём не стоим»: список отдаёт его при уходе курсора "
            + "за свои границы, и второй ветки для этого заводить незачем");
        Assert.IsFalse(state.IsShowing);
    }

    [Test]
    public void Commit_ClearsThePreview_SoTheRealChangeGoesThroughTheStack()
    {
        var state = new ScenePreviewState();
        state.Hover("dn32");

        Assert.AreEqual(ScenePreviewStep.Commit, state.Commit(),
            "выбор — это конец превью: дальше правку кладёт CommandStack, "
            + "а призрак обязан исчезнуть до того, как появится настоящий объект");
        Assert.IsFalse(state.IsShowing);
        Assert.IsNull(state.ShownKey);
    }

    [Test]
    public void Commit_WithoutHover_StillClears()
    {
        var state = new ScenePreviewState();

        Assert.AreEqual(ScenePreviewStep.Commit, state.Commit(),
            "по пункту можно щёлкнуть с клавиатуры, не наводя мышь: превью не было, "
            + "но выбор состоялся, и хвостов после него остаться не должно");
        Assert.IsFalse(state.IsShowing);
    }

    [Test]
    public void Hover_AfterCommit_StartsOver()
    {
        var state = new ScenePreviewState();
        state.Hover("dn32");
        state.Commit();

        Assert.AreEqual(ScenePreviewStep.Build, state.Hover("dn32"),
            "после применения тот же ключ — уже новое превью: память о снятом призраке "
            + "заставила бы список молчать при следующем наведении");
    }
}

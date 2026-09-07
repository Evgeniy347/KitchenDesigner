using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Выводимые диаметры фитингов.
///
/// У фитинга нет своего диаметра: он читается с подведённых труб, и переходник
/// честно показывает ДВА разных числа. Неподключённый порт даёт прочерк — не
/// «3/4 по умолчанию»: иначе в спецификацию уедет размер, которого никто не
/// задавал. Пересчёт делает ОДНА функция PipeSurvey.Of, её адаптер зовёт на
/// каждом поводе; второго писателя у этих полей нет.</summary>
public class PipeSurveyTests
{
    private static PipeTestScene Reducer()
    {
        var start = PipeTestScene.At(0f, 0f, 0f);
        var jointIn = PipeTestScene.At(1000f, 0f, 0f);
        var jointOut = PipeTestScene.At(1050f, 0f, 0f);
        var end = PipeTestScene.At(2000f, 0f, 0f);

        return new PipeTestScene()
            .Pipe("thin", start, jointIn, PipeSpec.Dn20)
            .Pipe("thick", jointOut, end, PipeSpec.Dn25)
            .Fitting("r", PipeNodeKind.Reducer, (jointIn, PipeAxis.Left), (jointOut, PipeAxis.Right));
    }

    [Test]
    public void PipeSurvey_SizesOfElement_ShowBothPipesOnAReducer()
    {
        var survey = Reducer().Survey();
        CollectionAssert.AreEqual(new[] { PipeSpec.Dn20, PipeSpec.Dn25 },
            survey.SizesOfElement("r"),
            "к переходной муфте подошли 3/4\" и 1\" — ровно это и должно быть в её свойствах");
    }

    [Test]
    public void PipeSurvey_SizesOfElement_AreOrderedByPortIndex()
    {
        var scene = Reducer();
        var survey = scene.Survey();
        Assert.AreEqual(PipeSpec.Dn20, survey.SizeOf(scene.IndexOf("r", 0)));
        Assert.AreEqual(PipeSpec.Dn25, survey.SizeOf(scene.IndexOf("r", 1)));
    }

    [Test]
    public void PipeSurvey_UnconnectedFittingPort_HasNoSizeAtAll()
    {
        var joint = PipeTestScene.At(1000f, 0f, 0f);
        var scene = new PipeTestScene()
            .Pipe("thin", PipeTestScene.At(0f, 0f, 0f), joint, PipeSpec.Dn20)
            .Fitting("t", PipeNodeKind.Tee,
                (joint, PipeAxis.Left), (joint, PipeAxis.Right), (joint, PipeAxis.Up));

        var survey = scene.Survey();
        Assert.AreEqual(PipeSpec.Dn20, survey.SizeOf(scene.IndexOf("t", 0)));
        Assert.IsNull(survey.SizeOf(scene.IndexOf("t", 1)),
            "к этому порту ничего не подведено — размера нет");
        Assert.AreEqual(PipeSpec.NoValue, survey.DesignationOf(scene.IndexOf("t", 2)),
            "в свойствах такой порт показывает прочерк");
    }

    [Test]
    public void PipeSurvey_PipeKeepsItsOwnSize_EvenWhenNothingIsConnected()
    {
        var scene = new PipeTestScene()
            .Pipe("p", PipeTestScene.At(0f, 0f, 0f), PipeTestScene.At(500f, 0f, 0f), PipeSpec.Dn40);
        var survey = scene.Survey();

        Assert.AreEqual(PipeSpec.Dn40, survey.SizeOf(scene.IndexOf("p", 0)),
            "диаметр трубы задаёт пользователь, он не выводится из соседей");
        Assert.AreEqual("1 1/2\"", survey.DesignationOf(scene.IndexOf("p", 1)));
    }

    [Test]
    public void PipeSurvey_SizeOf_IsNull_OutsideTheRange()
    {
        var survey = PipeSurvey.Of(new PipePort[0]);
        Assert.IsNull(survey.SizeOf(0));
        Assert.AreEqual(PipeSpec.NoValue, survey.DesignationOf(-1));
    }
}

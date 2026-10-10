using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class ModalDialogLayoutTests
{
    private static ModalDialogLayout Layout(float title, float body) =>
        ModalDialogLayout.For(titleHeight: title, bodyHeight: body,
            buttonHeight: 32f, pad: 24f, gap: 8f, gapBeforeButtons: 24f);

    [Test]
    public void ModalDialogLayout_TitleEndsAboveTheBody_ByTheGap()
    {
        var layout = Layout(title: 24f, body: 40f);
        Assert.AreEqual(24f + 24f + 8f, layout.BodyTop, 1e-4f,
            "тело начинается под заголовком, а не на заранее зашитом числе: у DemoModeDialogUI и "
            + "NewerVersionDialogUI заголовок налезал на первую строку текста");
    }

    [Test]
    public void ModalDialogLayout_Height_IsTheContentPlusPadding_NotAConstant()
    {
        var shortBody = Layout(24f, 20f);
        var longBody = Layout(24f, 80f);
        Assert.AreEqual(24f + 24f + 8f + 20f + 24f + 32f + 24f, shortBody.Height, 1e-4f);
        Assert.AreEqual(60f, longBody.Height - shortBody.Height, 1e-4f,
            "высота диалога растёт ровно на прирост текста: 100 px пустоты между текстом и "
            + "кнопками у старых диалогов были высотой-константой");
    }
}

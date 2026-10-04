using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class ModalDialogLayoutTests
{
    [Test]
    public void ModalDialogLayout_TitleEndsAboveTheBody_ByTheGap()
    {
        var layout = ModalDialogLayout.For(titleHeight: 24f, bodyHeight: 40f, noteHeight: 0f,
            buttonHeight: 32f, pad: 24f, gap: 8f, gapBeforeButtons: 24f);
        Assert.AreEqual(24f + 24f + 8f, layout.BodyTop, 1e-4f,
            "тело начинается под заголовком, а не на заранее зашитом числе: у DemoModeDialogUI и "
            + "NewerVersionDialogUI заголовок налезал на первую строку текста");
    }

    [Test]
    public void ModalDialogLayout_Height_IsTheContentPlusPadding_NotAConstant()
    {
        var shortBody = ModalDialogLayout.For(24f, 20f, 0f, 32f, 24f, 8f, 24f);
        var longBody = ModalDialogLayout.For(24f, 80f, 0f, 32f, 24f, 8f, 24f);
        Assert.AreEqual(24f + 24f + 8f + 20f + 24f + 32f + 24f, shortBody.Height, 1e-4f);
        Assert.AreEqual(60f, longBody.Height - shortBody.Height, 1e-4f,
            "высота диалога растёт ровно на прирост текста: 100 px пустоты между текстом и "
            + "кнопками у старых диалогов были высотой-константой");
    }

    [Test]
    public void ModalDialogLayout_Note_SitsUnderTheBody_AndAddsItsGapOnlyWhenPresent()
    {
        var without = ModalDialogLayout.For(24f, 40f, 0f, 32f, 24f, 8f, 24f);
        var with = ModalDialogLayout.For(24f, 40f, 18f, 32f, 24f, 8f, 24f);
        Assert.AreEqual(with.BodyTop + 40f + 8f, with.NoteTop, 1e-4f);
        Assert.AreEqual(18f + 8f, with.Height - without.Height, 1e-4f,
            "без пояснения нет и его зазора — иначе пустая строка висит над кнопками");
    }
}

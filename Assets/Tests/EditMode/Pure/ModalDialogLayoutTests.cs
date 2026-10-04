using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class ModalDialogLayoutTests
{
    private static ModalDialogLayout Layout(float title, float body, float extra = 0f, float note = 0f) =>
        ModalDialogLayout.For(titleHeight: title, bodyHeight: body, extraHeight: extra, noteHeight: note,
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

    [Test]
    public void ModalDialogLayout_Note_SitsUnderTheBody_AndAddsItsGapOnlyWhenPresent()
    {
        var without = Layout(24f, 40f);
        var with = Layout(24f, 40f, note: 18f);
        Assert.AreEqual(with.BodyTop + 40f + 8f, with.NoteTop, 1e-4f);
        Assert.AreEqual(18f + 8f, with.Height - without.Height, 1e-4f,
            "без пояснения нет и его зазора — иначе пустая строка висит над кнопками");
    }

    [Test]
    public void ModalDialogLayout_Extra_SitsBetweenTheBodyAndTheNote_AndAddsItsGapOnlyWhenPresent()
    {
        var without = Layout(24f, 40f, note: 18f);
        var with = Layout(24f, 40f, extra: 8f, note: 18f);
        Assert.AreEqual(with.BodyTop + 40f + 8f, with.ExtraTop, 1e-4f,
            "полоса хода загрузки стоит под текстом, а не поверх него");
        Assert.AreEqual(with.ExtraTop + 8f + 8f, with.NoteTop, 1e-4f, "пояснение — под полосой");
        Assert.AreEqual(8f + 8f, with.Height - without.Height, 1e-4f);

        var noExtra = Layout(24f, 40f);
        Assert.AreEqual(noExtra.BodyTop + 40f, noExtra.ExtraTop, 1e-4f,
            "без вставки её зазора нет: старые диалоги не сдвигаются ни на пиксель");
    }
}

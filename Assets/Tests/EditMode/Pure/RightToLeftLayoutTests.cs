using KitchenDesigner.Core;
using NUnit.Framework;

public class RightToLeftLayoutTests
{
    [Test]
    public void RightToLeftLayout_Prepare_ShapesArabic_AndReversesTheNumber()
    {
        Assert.AreEqual("ﺳﻼﻡ 008", RightToLeftLayout.Prepare("سلام 800"),
            "подпись проходит и формы букв, и порядок: одна дверь на оба шага");
    }

    [Test]
    public void RightToLeftLayout_Prepare_EachLineIsItsOwnParagraph()
    {
        Assert.AreEqual("ﺍ\n008 VTG", RightToLeftLayout.Prepare("ا\nGTV 800"),
            "направление строки берётся по её первой букве: латинская строка в арабском интерфейсе остаётся слева направо");
    }

    [Test]
    public void RightToLeftLayout_Prepare_RichTextTags_StayIntact()
    {
        Assert.AreEqual("<b>ﺳﻼﻡ</b> 21", RightToLeftLayout.Prepare("<b>سلام</b> 12"),
            "тег TMP не переворачивается — иначе вместо жирного на экране сам тег задом наперёд");
    }

    [Test]
    public void RightToLeftLayout_Prepare_NoStrongLetter_DefaultsToRightToLeft()
    {
        Assert.AreEqual(": 54", RightToLeftLayout.Prepare(": 45"),
            "строка без букв в арабском интерфейсе считается арабской: двоеточие справа, число слева (на экране «45 :»)");
    }

    [TestCase("")]
    [TestCase(null)]
    public void RightToLeftLayout_Prepare_Empty_IsReturnedAsIs(string? text)
    {
        Assert.AreEqual(text, RightToLeftLayout.Prepare(text!));
    }
}

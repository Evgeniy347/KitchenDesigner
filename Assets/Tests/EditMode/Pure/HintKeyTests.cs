using NUnit.Framework;
using KitchenDesigner.Core.UI;

/// <summary>
/// Форма ключа подсказки. Наполнение текстами уходит дешёвому исполнителю на сотни
/// контролов, и единственное, что удерживает набор от расползания («Settings.View.Walls»,
/// «settings_view_walls», «settings.view.walls ») — проверяемая форма: 2–4 сегмента через
/// точку, каждый начинается со строчной латинской буквы, дальше только буквы и цифры.
/// Тесты ниже парны: на каждое разрешение есть встречный запрет.
/// </summary>
public class HintKeyTests
{
    [Test]
    public void TwoSegments_IsTheShortestValidKey()
    {
        Assert.IsTrue(HintKey.IsValid("settings.walls"), "два сегмента — минимум");
        Assert.IsFalse(HintKey.IsValid("walls"),
            "один сегмент не говорит, ГДЕ живёт контрол, и столкнётся с тёзкой из другой панели");
    }

    [Test]
    public void FourSegments_IsTheLongestValidKey()
    {
        Assert.IsTrue(HintKey.IsValid("settings.view.wall.outline"));
        Assert.IsFalse(HintKey.IsValid("settings.view.wall.outline.color"),
            "пятый сегмент означает, что панель пора делить, а не удлинять ключ");
    }

    [Test]
    public void CamelCase_IsTheOnlyShapeOfASegment()
    {
        Assert.IsTrue(HintKey.IsValid("settings.view.wallOutline"));
        Assert.IsFalse(HintKey.IsValid("settings.view.WallOutline"),
            "сегмент с прописной буквы — вторая форма того же ключа, и словарь примет обе");
        Assert.IsFalse(HintKey.IsValid("settings.view.wall_outline"), "подчёркивание — третья форма");
        Assert.IsFalse(HintKey.IsValid("settings.view.wall-outline"), "дефис — четвёртая");
    }

    [Test]
    public void EmptySegment_IsRejected()
    {
        Assert.IsFalse(HintKey.IsValid("settings..walls"));
        Assert.IsFalse(HintKey.IsValid(".settings.walls"));
        Assert.IsFalse(HintKey.IsValid("settings.walls."));
    }

    [Test]
    public void WhitespaceAndCyrillic_AreRejected()
    {
        Assert.IsFalse(HintKey.IsValid("settings.view walls"),
            "пробел внутри ключа переживёт копипасту и всплывёт как «нет текста»");
        Assert.IsFalse(HintKey.IsValid("настройки.вид.стены"),
            "ключ — латиница: он часть кода, а не текста интерфейса");
    }

    [Test]
    public void NullAndEmpty_AreNotKeys()
    {
        Assert.IsFalse(HintKey.IsValid(null),
            "необязательный параметр подсказки приходит null — форма обязана это пережить, а не упасть");
        Assert.IsFalse(HintKey.IsValid(string.Empty),
            "пустая строка — забытый ключ, а не «подсказки нет»: у контрола без подсказки нет и «i»");
    }

    [Test]
    public void OverlongKey_IsRejected()
    {
        Assert.IsFalse(HintKey.IsValid("settings." + new string('a', HintKey.MaxLength)),
            "ключ длиннее " + HintKey.MaxLength + " — это уже пересказ подсказки в имени");
    }
}

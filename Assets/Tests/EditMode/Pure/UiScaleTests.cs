using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class UiScaleTests
{
    [Test]
    public void UiScale_Fit_OnTheReferenceScreen_IsOne()
    {
        Assert.AreEqual(1f, UiScale.Fit(1920, 1080), 1e-5f);
    }

    [Test]
    public void UiScale_Fit_OnTheBaseLaptopScreen_IsTheSeventyOneThatMadeTextTiny()
    {
        Assert.AreEqual(0.711f, UiScale.Fit(1366, 768), 0.001f,
            "Это то же, что считает CanvasScaler ScaleWithScreenSize с match 0,5 (среднее "
            + "логарифмов): 1366×768 сжимал кегль 16 до 11,4 физического пикселя — мельче "
            + "системного шрифта Windows (D1)");
    }

    [Test]
    public void UiScale_Automatic_NeverGoesBelowTheFloor()
    {
        Assert.AreEqual(UiScale.Floor, UiScale.Automatic(1366, 768), 1e-5f,
            "пол 0,8125 — тело 16 становится 13 px, а не 11,4 (D1)");
        Assert.AreEqual(1f, UiScale.Automatic(1920, 1080), 1e-5f, "на референсном экране пол не вмешивается");
        Assert.AreEqual(2f, UiScale.Automatic(3840, 2160), 1e-5f, "4K растёт как прежде");
    }

    [Test]
    public void UiScale_CanvasHeight_OnTheBaseScreenAtTheFloor_Is945()
    {
        float h = UiScale.CanvasHeight(768, UiScale.Automatic(1366, 768));
        Assert.AreEqual(945.2f, h, 0.1f,
            "D1: высота канвы на 1366×768 — 945 реф. px, а не 1080: на это число и проверяются "
            + "окна (WindowFitsTheBaseScreenTests)");
    }

    [TestCase(90, 0.9f)]
    [TestCase(150, 1.5f)]
    [TestCase(60, 0.9f)]
    [TestCase(400, 1.5f)]
    public void UiScale_Factor_MultipliesTheAutomaticScale_WithinNinetyToOneFifty(int percent, float multiplier)
    {
        Assert.AreEqual(multiplier, UiScale.Factor(1920, 1080, percent), 1e-5f,
            "«Масштаб интерфейса» — множитель поверх автоматического (как масштаб страницы в "
            + "браузере), зажатый в 90–150 %: значение из чужого или испорченного файла "
            + "настроек не может сделать интерфейс нечитаемым");
    }

    [Test]
    public void UiScale_ScaleFactorFor_EmulatedScreen_GivesTheEmulatedCanvasHeight()
    {
        float scale = UiScale.ScaleFactorFor(1080, 1366, 768, UiScale.AutoPercent);
        Assert.AreEqual(945.2f, 1080 / scale, 0.1f,
            "сторож окон эмулирует 1366×768 на любом реальном экране батча: высота канвы "
            + "обязана выйти та же, что у пользователя на ноутбуке");
    }

    [Test]
    public void UiScale_Choices_StartWithAuto_AndStayInRange()
    {
        Assert.AreEqual(UiScale.AutoPercent, UiScale.ChoicePercents[0]);
        foreach (int p in UiScale.ChoicePercents)
            Assert.AreEqual(p, UiScale.ClampPercent(p), "пункт списка вне 90–150 % был бы зажат молча");
        Assert.AreEqual(0, UiScale.ChoiceIndex(777), "неизвестное значение показывается как «Авто»");
    }
}

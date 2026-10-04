using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class ModuleEditBannerTests
{
    private GameObject? _canvasGo;
    private Transform? _banner;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        _canvasGo.AddComponent<ModuleEditBannerUI>().Build(_canvasGo.transform);
        _banner = _canvasGo.transform.Find("ModuleEditBanner");
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    [Test]
    public void TheBanner_IsPaintedWithTheNavigationTokens_NotALocalBlue()
    {
        Assert.IsNotNull(_banner, "плашка собрана");
        Assert.AreEqual(UIStyle.NavBg, _banner!.GetComponent<Image>().color, "фон плашки — NavBg (D4)");
        var rim = _banner.Find(UIFactory.FieldStrokeNode)!.GetComponent<Image>();
        Assert.AreEqual(UIStyle.Divider, rim.color, "рамка — Divider");
    }

    [Test]
    public void TheLabel_UsesTheSmallFontAndPrimaryText_AndTheDoneButtonSitsOnTheRight()
    {
        var label = _banner!.Find("MebLabel")!.GetComponent<TMP_Text>();
        Assert.AreEqual(UIStyle.FontSmall, label.fontSize, "кегль плашки — FontSmall (D3)");
        Assert.AreEqual(UIStyle.Text, label.color);

        var done = (RectTransform)_banner.Find("MebDone")!;
        Assert.AreEqual(1f, done.anchorMax.x, "«Готово» пристёгнута к правому краю плашки");
        Assert.Less(label.rectTransform.anchoredPosition.x + label.rectTransform.sizeDelta.x,
            done.anchoredPosition.x + ModuleEditBannerUI.BannerWidth - done.sizeDelta.x + 0.01f,
            "подпись заканчивается левее кнопки");
    }
}

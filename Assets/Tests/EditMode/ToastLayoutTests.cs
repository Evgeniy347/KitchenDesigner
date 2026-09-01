using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class ToastLayoutTests
{
    private GameObject? _canvasGo;
    private ToastNotification? _toast;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();

        var host = new GameObject("Toast");
        host.transform.SetParent(_canvasGo!.transform);
        _toast = host.AddComponent<ToastNotification>();
        _toast!.Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private RectTransform Panel() => (RectTransform)_canvasGo!.transform.Find("ToastPanel")!;

    private TMP_Text Label() => Panel().Find("ToastLabel")!.GetComponent<TMP_Text>();

    [Test]
    public void Toast_ActionButton_IsPinnedToTheRightEdge_AndHiddenWithoutAnAction()
    {
        var button = (RectTransform)Panel().Find("ToastAction")!;

        Assert.AreEqual(new Vector2(1, 0.5f), button.anchorMin, "кнопка действия прижата вправо");
        Assert.AreEqual(new Vector2(1, 0.5f), button.pivot);
        Assert.IsFalse(button.gameObject.activeSelf,
            "и видна только тогда, когда у тоста есть действие — «Отменить» после удаления");
    }

    [Test]
    public void Toast_Label_GivesUpTheRightSideOfThePanel_WhenThereIsAnActionButton()
    {
        _toast!.LayOutLabelBesideTheActionButton(false);
        Assert.AreEqual(0f, Label().rectTransform.offsetMax.x,
            "без кнопки текст занимает всю панель и стоит по центру");
        Assert.AreEqual(TextAlignmentOptions.Center, Label().alignment);

        _toast!.LayOutLabelBesideTheActionButton(true);
        Assert.AreEqual(-ToastNotification.LabelWidthGivenUpToTheActionButton,
            Label().rectTransform.offsetMax.x,
            "с кнопкой текст уступает ей правую часть панели, иначе они наезжают друг на друга");
        Assert.AreEqual(TextAlignmentOptions.Left, Label().alignment,
            "и выравнивается влево, к своему краю");
    }
}

using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class ConfirmDeleteButtonTests
{
    private GameObject? _canvasGo;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();
    }

    [TearDown]
    public void Teardown()
    {
        ConfirmDeleteButton.DisarmAll();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private (Button button, ConfirmDeleteButton confirm, TMP_Text label) Row(string name,
        System.Action onConfirm)
    {
        var button = UIFactory.CreateConfirmDeleteButton(name, _canvasGo!.transform,
            UIStyle.GlyphClose, Vector2.zero, new Vector2(24, 24), onConfirm);
        return (button, button.GetComponent<ConfirmDeleteButton>(),
            button.GetComponentInChildren<TMP_Text>(true));
    }

    [Test]
    public void ConfirmDelete_ArmingASecondRow_DisarmsTheFirstOne()
    {
        var first = Row("Del1", () => { });
        var second = Row("Del2", () => { });

        first.button.onClick.Invoke();
        Assume.That(first.confirm.Armed, Is.True);

        second.button.onClick.Invoke();

        Assert.IsFalse(first.confirm.Armed,
            "Взведена всегда не больше одной кнопки: вопрос «удалять?» должен висеть в одном "
            + "месте, а не в трёх строках списка сразу");
        Assert.AreEqual(UIStyle.GlyphClose, first.label.text,
            "и снятая кнопка возвращает свой исходный глиф");
        Assert.IsTrue(second.confirm.Armed);
    }

    [Test]
    public void ConfirmDelete_DisarmAll_ResetsTheArmedRow()
    {
        int deleted = 0;
        var row = Row("Del", () => deleted++);

        row.button.onClick.Invoke();
        Assume.That(row.confirm.Armed, Is.True);

        ConfirmDeleteButton.DisarmAll();

        Assert.IsFalse(row.confirm.Armed,
            "Когда набор под кнопками поменялся мимо неё (undo, MCP, смена элемента), взвод "
            + "снимают снаружи: взведённая кнопка спрашивала про строку, которой уже нет");
        Assert.AreEqual(UIStyle.GlyphClose, row.label.text);

        row.button.onClick.Invoke();
        Assert.AreEqual(0, deleted, "после сброса счёт кликов начинается заново");
    }

    [Test]
    public void ConfirmDelete_FirstClick_NeverDeletes()
    {
        int deleted = 0;
        var row = Row("Del", () => deleted++);

        row.button.onClick.Invoke();
        Assert.AreEqual(0, deleted,
            "Attach не вешает на кнопку собственный обработчик удаления: удаление зовётся "
            + "только вторым кликом, первый лишь спрашивает");
        Assert.AreEqual(UIStyle.GlyphConfirm, row.label.text);

        row.button.onClick.Invoke();
        Assert.AreEqual(1, deleted, "второй клик удаляет");
        Assert.AreEqual(UIStyle.GlyphClose, row.label.text, "и кнопка возвращается в исходный вид");
    }
}

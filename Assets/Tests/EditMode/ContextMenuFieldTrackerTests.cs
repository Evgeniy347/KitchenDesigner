using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuFieldTrackerTests
{
    private Canvas? _canvas;
    private int _applyCalls;
    private ContextMenuFieldTracker? _tracker;

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        _applyCalls = 0;
        _tracker = new ContextMenuFieldTracker(() => _applyCalls++);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    private TMP_InputField Field(string text) =>
        UIFactory.CreateInputField("F", _canvas!.transform, text, Vector2.zero, new Vector2(100, 24));

    private static bool Highlighted(TMP_InputField f)
    {
        var outline = f.GetComponent<Outline>();
        return outline != null && outline.enabled;
    }

    [Test]
    public void ParseInt_ValidNumber_ReturnsItAndRejectsNothing()
    {
        var f = Field("450");
        Assert.AreEqual(450, _tracker!.ParseInt(f, 100));
        Assert.AreEqual(0, _tracker!.RejectedFields.Count,
            "принятое значение не должно попадать в список непринятых");
    }

    [Test]
    public void ParseInt_Garbage_KeepsFallbackAndRejectsField()
    {
        var f = Field("не число");
        Assert.AreEqual(100, _tracker!.ParseInt(f, 100),
            "невалидный ввод не применяется — остаётся прежнее значение");
        CollectionAssert.Contains(_tracker!.RejectedFields, f,
            "поле с непринятым вводом обязано попасть в список: иначе значение откатилось бы молча, "
            + "без красной рамки (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void ParseInt_Arithmetic_EvaluatesAndWritesResultBack()
    {
        var f = Field("400+50");
        Assert.AreEqual(450, _tracker!.ParseInt(f, 0));
        Assert.AreEqual("450", f.text.Replace("​", ""),
            "вычисленное значение должно вернуться в поле, иначе пользователь видит формулу, а применилось число");
    }

    [Test]
    public void ParseMillimetresAsMetres_Converts()
    {
        var f = Field("1500");
        Assert.AreEqual(1.5f, _tracker!.ParseMillimetresAsMetres(f, 0f), 1e-4f,
            "поля позиции показывают мм, внутренняя модель живёт в метрах");
    }

    [Test]
    public void ParseDecimalInRange_Comma_IsAcceptedAsDecimalSeparator()
    {
        var f = Field("0,5");
        Assert.AreEqual(0.5f, _tracker!.ParseDecimalInRange(f, 2f, 0.4f, 2f), 1e-4f,
            "на русской раскладке толщина набирается через запятую, а выводится через точку");
        Assert.AreEqual(0, _tracker!.RejectedFields.Count);
    }

    [Test]
    public void ParseDecimalInRange_OutOfRange_IsRejectedNotClamped()
    {
        var f = Field("50");
        Assert.AreEqual(2f, _tracker!.ParseDecimalInRange(f, 2f, 0.4f, 3f), 1e-4f,
            "значение вне диапазона не клампится молча — оно не принимается");
        CollectionAssert.Contains(_tracker!.RejectedFields, f);
    }

    [Test]
    public void ShowRejections_PaintsOnlyRejectedFields()
    {
        var bad = Field("хлам");
        var good = Field("10");
        _tracker!.ParseInt(bad, 1);
        _tracker!.ParseInt(good, 1);
        _tracker!.ShowRejections();
        Assert.IsTrue(Highlighted(bad), "непринятое поле обязано получить рамку");
        Assert.IsFalse(Highlighted(good), "принятое поле рамку получать не должно");
    }

    [Test]
    public void ForgetRejections_ClearsPreviousApplyErrors()
    {
        var f = Field("хлам");
        _tracker!.ParseInt(f, 1);
        _tracker!.ForgetRejections();
        Assert.AreEqual(0, _tracker!.RejectedFields.Count,
            "ошибки прошлого применения снимаются новым вводом");
    }

    [Test]
    public void ApplyOncePerFrame_TwoCallsInOneFrame_AppliesOnce()
    {
        _tracker!.ApplyOncePerFrame();
        _tracker!.ApplyOncePerFrame();
        Assert.AreEqual(1, _applyCalls,
            "потеря фокуса и Enter приходят в одном кадре: без защиты одна правка "
            + "выполнилась бы дважды и стоила бы двух шагов отмены");
    }

    [Test]
    public void Track_EditingField_TurnsHighlightOn()
    {
        var f = Field("400");
        _tracker!.Track(f, "400");
        Assert.IsFalse(Highlighted(f), "нетронутое поле не подсвечивается");
        f.text = "500";
        Assert.IsTrue(Highlighted(f), "изменённое, но ещё не применённое поле подсвечивается");
    }

    [Test]
    public void RefreshUnfocused_SyncsCleanValue_SoHighlightStaysOff()
    {
        var f = Field("400");
        _tracker!.Track(f, "400");
        _tracker!.RefreshUnfocused(f, "700");
        _tracker!.UpdateHighlight(f);
        Assert.IsFalse(Highlighted(f),
            "деталь подвинули мимо панели: поле переписано кодом, а не человеком — "
            + "подсветка «есть непринятая правка» тут ложная");
    }

    [Test]
    public void ClearHighlights_DropsHighlightAndStopsReactingToEdits()
    {
        var f = Field("400");
        _tracker!.Track(f, "400");
        f.text = "500";
        _tracker!.ClearHighlights();
        Assert.IsFalse(Highlighted(f), "после применения подсветки снимаются");
        f.text = "600";
        Assert.IsFalse(Highlighted(f),
            "слушатели сняты вместе с подсветкой — иначе на поле копились бы дубли подписок");
    }
}

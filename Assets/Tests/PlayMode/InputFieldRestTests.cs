using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using KitchenDesigner.Core.UI;

/// <summary>Набранное в числовое поле значение обязано стоять там же, где то же значение,
/// выставленное из кода: у правого края, у единицы измерения. TMP_InputField двигает
/// RectTransform текста вслед за кареткой, и если текст — сам вьюпорт, поле «уезжает» навсегда
/// (в инспекторе «50» и «360» стояли левее «2000» в нетронутом поле). Тест набирает по символу
/// с кадром между ними, снимает фокус и сверяет геометрию с эталоном.
///
/// PlayMode: каретка TMP создаётся только в Play, а двигается она на кадрах. Поля создаются
/// в активном дереве и не пересобираются: тест меряет живое поле, как у пользователя.</summary>
public class InputFieldRestTests
{
    private const float Tolerance = 0.01f;

    private GameObject _canvasGo = null!;

    [SetUp]
    public void SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        UIFactory.EnsureEventSystem();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private TMP_InputField NewNumberField(string name, string initial, float y)
    {
        var field = UIFactory.CreateNumberField(name, _canvasGo.transform, initial, Vector2.zero,
            new Vector2(150f, 28f), "мм");
        field.textComponent!.alignment = TextAlignmentOptions.Right;
        field.contentType = TMP_InputField.ContentType.Custom;
        field.onValidateInput = DimensionFieldValidation.Char();
        var rt = (RectTransform)field.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(100f, y);
        return field;
    }

    [Test]
    public void FieldCreatedInTheActiveTree_HasACaretAtOnce()
    {
        var field = UIFactory.CreateNumberField("N", _canvasGo.transform, "50", Vector2.zero,
            new Vector2(150f, 28f), "мм");

        Assert.IsNotNull(field.textViewport!.Find("Caret"),
            "OnEnable TMP создаёт каретку, только если текст и вьюпорт уже подключены; поле, "
            + "собранное в активном дереве, просыпалось пустым и оставалось без каретки");
        Assert.IsTrue(field.isActiveAndEnabled);
    }

    /// <summary>ProcessEvent зовёт только KeyPressed: он правит строку поля, но не метку. Метку
    /// обновляет вызывающий — OnUpdateSelected, получив событие от EventSystem. Без
    /// ForceLabelUpdate после каждого символа метка осталась бы «2000» (так тест уже промахнулся:
    /// ink-границы набранного совпадали с исходным значением при любом наборе).</summary>
    public static IEnumerator TypeInto(TMP_InputField field, string value, bool verifyText = true)
    {
        field.ActivateInputField();
        yield return null;
        Assert.IsTrue(field.isFocused,
            "поле обязано быть в фокусе перед набором — иначе тест ничего не доказывает");

        foreach (char c in value)
        {
            field.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            field.ForceLabelUpdate();
            yield return null;
        }

        yield return null;
        field.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        yield return null;
        if (verifyText)
            Assert.AreEqual(value, field.text, "набор обязан дать именно это значение");
    }

    private static List<float> Measure(TMP_InputField field)
    {
        var text = field.textComponent!;
        text.ForceMeshUpdate();
        var host = field.transform;
        var corners = new Vector3[4];
        text.rectTransform.GetWorldCorners(corners);
        var viewport = new Vector3[4];
        field.textViewport!.GetWorldCorners(viewport);
        var ink = text.textBounds;
        var inkRight = host.InverseTransformPoint(text.transform.TransformPoint(ink.max)).x;
        var inkLeft = host.InverseTransformPoint(text.transform.TransformPoint(ink.min)).x;
        return new List<float>
        {
            host.InverseTransformPoint(corners[0]).x, host.InverseTransformPoint(corners[2]).x,
            host.InverseTransformPoint(viewport[0]).x, host.InverseTransformPoint(viewport[2]).x,
            text.rectTransform.anchoredPosition.x, field.textViewport.anchoredPosition.x,
            inkLeft, inkRight,
        };
    }

    private static readonly string[] MeasureNames =
    {
        "левый край рамки текста", "правый край рамки текста", "левый край вьюпорта",
        "правый край вьюпорта", "anchoredPosition текста", "anchoredPosition вьюпорта",
        "левый край отрисованных цифр", "правый край отрисованных цифр",
    };

    private IEnumerator AssertTypedMatchesProgrammatic(string initial, string typed)
    {
        var edited = NewNumberField("Typed_" + typed.Length, initial, 40f);
        var reference = NewNumberField("Ref_" + typed.Length, initial, -40f);
        yield return null;

        yield return TypeInto(edited, typed);
        reference.SetTextWithoutNotify(typed);
        yield return null;

        var got = Measure(edited);
        var want = Measure(reference);
        for (int i = 0; i < got.Count; i++)
            Assert.AreEqual(want[i], got[i], Tolerance,
                $"«{typed}» (было «{initial}»): {MeasureNames[i]} — после набора и ухода из поля "
                + "должно быть как у того же значения, выставленного из кода");
    }

    [UnityTest]
    public IEnumerator ShortNumber_TypedOverALongOne_SitsWhereTheProgrammaticOneSits()
    {
        yield return AssertTypedMatchesProgrammatic("2000", "50");
    }

    [UnityTest]
    public IEnumerator ThreeDigits_TypedOverAnother_SitsWhereTheProgrammaticOneSits()
    {
        yield return AssertTypedMatchesProgrammatic("2000", "360");
    }

    [UnityTest]
    public IEnumerator NumberLongerThanTheField_Typed_SitsWhereTheProgrammaticOneSits()
    {
        yield return AssertTypedMatchesProgrammatic("2000", "123456789012345678901234");
    }

    private const float ScrolledBy = -30f;

    private static float TextOffset(TMP_InputField field) =>
        field.textComponent!.rectTransform.anchoredPosition.x;

    private IEnumerator FocusTypeAndScroll(TMP_InputField field, string value)
    {
        field.ActivateInputField();
        yield return null;
        Assert.IsTrue(field.isFocused,
            "поле обязано быть в фокусе перед набором — иначе тест ничего не доказывает");
        foreach (char c in value)
        {
            field.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            field.ForceLabelUpdate();
            yield return null;
        }
        field.textComponent!.rectTransform.anchoredPosition = new Vector2(ScrolledBy, 0f);
        Assert.AreEqual(ScrolledBy, TextOffset(field), Tolerance,
            "посылка: текст сдвинут так, как его двигает TMP за кареткой, — иначе покой нечем доказать");
    }

    private static void RunUpdateSelectedLikeTheInputModuleDoes()
    {
        var events = EventSystem.current;
        ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events),
            ExecuteEvents.updateSelectedHandler);
    }

    /// <summary>Enter не снимает выделение с поля в EventSystem: TMP по Return зовёт
    /// DeactivateInputField, а OnDeselect не приходит, пока пользователь не кликнет в другое
    /// место. Поле остаётся выбранным и расфокусированным, и каждый кадр EventSystem зовёт его
    /// OnUpdateSelected. Это единственная дорога, на которой ветка «выбрано, но не в фокусе»
    /// успокаивает текст. Модуль ввода в batch-прогоне события из очереди не отдаёт, поэтому
    /// Return шлётся в ProcessEvent, а то, что сделал бы OnUpdateSelected при Finish
    /// (DeactivateInputField), и сам вызов обновления выбранного тест выполняет руками — тем же
    /// ExecuteEvents.updateSelectedHandler, которым пользуется StandaloneInputModule.</summary>
    [UnityTest]
    public IEnumerator Enter_LeavesTheFieldSelected_ButRestsTheText()
    {
        var field = NewNumberField("Entered", "2000", 0f);
        yield return null;
        yield return FocusTypeAndScroll(field, "50");

        field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
        field.DeactivateInputField();

        Assert.IsFalse(field.isFocused, "посылка: Enter снимает фокус ввода с поля");
        Assert.AreSame(field.gameObject, EventSystem.current.currentSelectedGameObject,
            "посылка: Enter не снимает выделение в EventSystem — OnDeselect не придёт, "
            + "и покой может дать только OnUpdateSelected");
        Assert.AreEqual(ScrolledBy, TextOffset(field), Tolerance,
            "посылка: до обновления выбранного ничто, кроме проверяемой ветки, текст не двигало");

        RunUpdateSelectedLikeTheInputModuleDoes();
        Assert.AreEqual(0f, TextOffset(field), Tolerance,
            "после Enter текст остался сдвинутым: ветка «выбрано, не в фокусе» в "
            + "RestingInputField.OnUpdateSelected не вернула его в покой, а поле осталось выбранным");

        yield return null;
        yield return null;
        Assert.AreEqual(0f, TextOffset(field), Tolerance,
            "покой после Enter обязан держаться и на следующих кадрах, пока поле выбрано");
    }

    /// <summary>Парная к Enter_... проверка с обратной стороны условия: пока поле в фокусе,
    /// OnUpdateSelected обязан оставить текст там, куда его вёл TMP. Снятая проверка фокуса
    /// дёргала бы текст из-под каретки на каждом кадре набора; Enter_... без этой пары такую
    /// поломку не видит, а эта без той — не видит удалённой ветки.</summary>
    [UnityTest]
    public IEnumerator UpdateSelected_WhileTheFieldIsFocused_DoesNotMoveTheTextUnderTheCaret()
    {
        var field = NewNumberField("Focused", "2000", 0f);
        yield return null;
        yield return FocusTypeAndScroll(field, "50");

        Assert.IsTrue(field.isFocused, "посылка: поле всё ещё в фокусе");
        Assert.AreSame(field.gameObject, EventSystem.current.currentSelectedGameObject,
            "посылка: поле выбрано, значит модуль ввода зовёт его OnUpdateSelected");

        RunUpdateSelectedLikeTheInputModuleDoes();

        Assert.AreEqual(ScrolledBy, TextOffset(field), Tolerance,
            "OnUpdateSelected вернул текст в покой, пока поле в фокусе: при наборе текст дёргало бы "
            + "из-под каретки на каждом кадре");
    }

    [UnityTest]
    public IEnumerator Retyping_TwiceInARow_DoesNotAccumulateAShift()
    {
        var edited = NewNumberField("Typed", "2000", 40f);
        var reference = NewNumberField("Ref", "2000", -40f);
        yield return null;

        yield return TypeInto(edited, "50");
        yield return TypeInto(edited, "360");
        reference.SetTextWithoutNotify("360");
        yield return null;

        var got = Measure(edited);
        var want = Measure(reference);
        for (int i = 0; i < got.Count; i++)
            Assert.AreEqual(want[i], got[i], Tolerance,
                $"второй набор подряд: {MeasureNames[i]} — сдвиг не должен копиться");
    }
}

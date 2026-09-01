using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class MultiSelectDropdownTests
{
    private GameObject _canvasGo = null!;
    private MultiSelectDropdown _filter = null!;
    private int _changes;

    private const float FieldWidth = 200f;
    private const float RowInset = 16f;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("Canvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        _changes = 0;
        _filter = MultiSelectDropdown.Create("Flt", _canvasGo.transform, "Все",
            new Vector2(20, -20), new Vector2(FieldWidth, 28), () => _changes++);
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private RectTransform Field => _filter.GetComponent<RectTransform>();

    private void OpenPopup() => _filter.GetComponent<Button>().onClick.Invoke();

    private Transform? Overlay => _canvasGo.transform.Find("MultiSelectOverlay");

    private List<Toggle> Rows()
    {
        var rows = new List<Toggle>();
        var overlay = Overlay;
        if (overlay != null) rows.AddRange(overlay.GetComponentsInChildren<Toggle>(true));
        return rows;
    }

    private static Vector3 BottomLeft(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return corners[0];
    }

    private static Vector3 TopLeft(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return corners[1];
    }

    [Test]
    public void IsAllowed_NothingChecked_PassesEverything()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });

        Assert.IsTrue(_filter.IsAllowed("COL-01"));
        Assert.IsTrue(_filter.IsAllowed("SOMETHING-NEW"),
            "пустой набор — это «нет ограничения», а не «не проходит ничего»: "
            + "иначе фильтр с пустым выбором прятал бы весь список ошибок");
    }

    [Test]
    public void IsAllowed_SomethingChecked_PassesOnlyChecked()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();
        Rows()[1].isOn = true;

        Assert.IsFalse(_filter.IsAllowed("COL-01"));
        Assert.IsTrue(_filter.IsAllowed("GAP-02"));
        Assert.AreEqual(1, _changes,
            "отметка применяется сразу: кнопки «Применить» у окна ошибок нет");
    }

    [Test]
    public void SetOptions_KeepsCheckedOptionsThatSurvive_AndForgetsGoneOnes()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();
        Rows()[0].isOn = true;
        Rows()[1].isOn = true;

        _filter.SetOptions(new[] { "GAP-02", "OVR-03" });

        Assert.IsTrue(_filter.IsAllowed("GAP-02"), "выживший пункт остаётся отмеченным");
        Assert.IsFalse(_filter.IsAllowed("OVR-03"),
            "новый код не проходит молча: ограничение задано и оно его не включает");
        Assert.IsFalse(_filter.IsAllowed("COL-01"),
            "исчезнувший код выкидывается из выбора, но выбор не становится пустым");
    }

    [Test]
    public void Caption_ShowsPlaceholder_WhenNothingOrEverythingChecked()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        var caption = _filter.GetComponentInChildren<TMPro.TMP_Text>();
        Assert.AreEqual("Все", caption.text,
            "виджет показывает только плейсхолдер и счётчик: описательная подпись над "
            + "полем — забота вызывающего (правило 5 UI-GUIDELINES)");

        OpenPopup();
        Rows()[0].isOn = true;
        Assert.AreEqual("Выбрано: 1", caption.text);

        Rows()[1].isOn = true;
        Assert.AreEqual("Все", caption.text,
            "отмечено всё — ограничения фактически нет, подпись это и показывает");
    }

    [Test]
    public void Popup_OpensDirectlyBelowField()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();

        var popup = Overlay!.Find("Popup") as RectTransform;
        Assert.IsNotNull(popup);
        Assert.AreEqual(BottomLeft(Field).x, TopLeft(popup!).x, 0.01f);
        Assert.AreEqual(BottomLeft(Field).y, TopLeft(popup!).y, 0.01f,
            "поповер вешается верхним левым углом на нижний левый угол поля — "
            + "иначе он накрывает само поле и первый пункт не нажать");
    }

    [Test]
    public void PopupRows_HaveFixedWidth_NotStretched()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();

        var row = Rows()[0].GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(0, 1), row.anchorMin);
        Assert.AreEqual(new Vector2(0, 1), row.anchorMax);
        Assert.AreEqual(FieldWidth - RowInset, row.sizeDelta.x, 0.01f,
            "UIFactory.CreateToggle раскладывает бокс и подпись от ПЕРЕДАННОЙ size.x: "
            + "растянутая по якорям строка получила бы бокс не на своём месте");
    }

    [UnityTest]
    public IEnumerator Popup_ScrollsOnlyWhenListIsTallerThanPopup()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02", "OVR-03" });
        OpenPopup();
        Assert.IsFalse(Overlay!.GetComponentInChildren<ScrollRect>().vertical,
            "короткий список не должен прокручиваться — колесо мыши остаётся сцене");

        var many = new List<string>();
        for (int i = 0; i < 20; i++) many.Add("CODE-" + i);
        _filter.SetOptions(many);
        yield return null;
        OpenPopup();
        Assert.IsTrue(Overlay!.GetComponentInChildren<ScrollRect>().vertical,
            "длинный список обязан прокручиваться: поповер выше PopupMaxH не растёт");
    }

    [Test]
    public void Catcher_IsInvisible_ButStillTakesClicks()
    {
        _filter.SetOptions(new[] { "COL-01" });
        OpenPopup();

        var catcher = Overlay!.Find("Catcher");
        var image = catcher.GetComponent<Image>();
        Assert.Less(image.color.a, 0.05f, "ловец кликов не должен затемнять экран");
        Assert.IsTrue(image.raycastTarget,
            "полностью прозрачная картинка кликов не ловит — поэтому альфа не ноль");
    }

    [UnityTest]
    public IEnumerator ClickOutside_ClosesPopup()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();
        Assume.That(Overlay, Is.Not.Null);

        Overlay!.Find("Catcher").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.IsNull(Overlay,
            "клик мимо закрывает список: без ловца поповер оставался бы висеть "
            + "поверх окна ошибок");
    }

    [UnityTest]
    public IEnumerator SetOptions_ClosesOpenPopup()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        OpenPopup();
        Assume.That(Overlay, Is.Not.Null);

        _filter.SetOptions(new[] { "GAP-02" });
        yield return null;

        Assert.IsNull(Overlay,
            "строки поповера построены по старому списку опций: оставить его открытым "
            + "значит показывать пункты, которых уже нет");
    }
}

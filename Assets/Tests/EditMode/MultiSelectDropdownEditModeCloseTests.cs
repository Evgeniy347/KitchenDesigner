using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

/// <summary>Закрытие поповера фильтра ВНЕ play mode — второй путь, открывшийся сведением
/// голого <c>Destroy(</c> к <c>DestroyNow.The</c>.
///
/// <para><c>ClosePopup</c> звал голый <c>Destroy</c>, который вне play mode бросает
/// <c>InvalidOperationException</c>: значит ни повторный клик по полю, ни клик мимо, ни
/// <c>SetOptions</c> поверх открытого списка в EditMode не проверялись вовсе — всё это
/// жило в <c>MultiSelectDropdownTests</c> под PlayMode, где каждому закрытию нужен
/// <c>yield return null</c>, потому что отложенный <c>Destroy</c> снимает оверлей только
/// в конце кадра.</para>
///
/// <para>В EditMode кадра нет, поэтому здесь утверждается более сильное: оверлей исчезает
/// в ТОЙ ЖЕ строке. Это же и есть защита от удвоения — отложенный снос оставлял бы старый
/// оверлей висеть поверх окна, пока строится новый.</para></summary>
public class MultiSelectDropdownEditModeCloseTests
{
    private const string OverlayName = "MultiSelectOverlay";

    private GameObject _canvasGo = null!;
    private MultiSelectDropdown _filter = null!;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        _filter = MultiSelectDropdown.Create("Flt", _canvasGo.transform, "Все",
            new Vector2(20, -20), new Vector2(200, 28), null);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo);
    }

    private Transform? Overlay => _canvasGo.transform.Find(OverlayName);

    private void ClickField() => _filter.GetComponent<Button>().onClick.Invoke();

    private List<Toggle> Rows()
    {
        var rows = new List<Toggle>();
        var overlay = Overlay;
        if (overlay != null) rows.AddRange(overlay.GetComponentsInChildren<Toggle>(true));
        return rows;
    }

    private int OverlayCount()
    {
        int count = 0;
        foreach (Transform child in _canvasGo.transform)
            if (child.name == OverlayName) count++;
        return count;
    }

    [Test]
    public void MultiSelectDropdown_SecondClickOnTheField_ClosesThePopupWithoutAFrame()
    {
        Assume.That(Application.isPlaying, Is.False,
            "тест про путь ВНЕ play mode: под PlayMode голый Destroy не падал и "
            + "доказывать было бы нечего");
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        ClickField();
        Assume.That(Overlay, Is.Not.Null, "первый клик открыл поповер");
        var overlay = Overlay!.gameObject;

        ClickField();

        Assert.IsTrue(overlay == null,
            "оверлей уничтожается немедленно: в EditMode кадра нет, и отложенный Destroy "
            + "оставил бы список висеть навсегда — сравнение через == пользуется "
            + "перегрузкой Unity, Assert.IsNull уничтоженный объект нулём не считает");
        Assert.IsNull(Overlay, "и он отцеплен от канвы, а не просто помечен на снос");
    }

    [Test]
    public void MultiSelectDropdown_ClickOutside_ClosesThePopupWithoutAFrame()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        ClickField();
        Assume.That(Overlay, Is.Not.Null, "поповер открыт");
        var overlay = Overlay!.gameObject;

        Overlay!.Find("Catcher").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(overlay == null,
            "клик мимо закрывает список тем же путём, что и клик по полю: ловец кликов "
            + "подписан прямо на ClosePopup, и до сведения к DestroyNow.The эта подписка "
            + "вне play mode кончалась исключением");
    }

    [Test]
    public void MultiSelectDropdown_ReopenedThreeTimes_LeavesExactlyOnePopup()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        ClickField();
        ClickField();
        ClickField();

        Assert.AreEqual(1, OverlayCount(),
            "открыть — закрыть — открыть обязано оставить ОДИН оверлей: отложенный снос "
            + "держит старый до конца кадра, и второй список строится поверх живого "
            + "первого, перехватывая клики невидимым ловцом");
    }

    [Test]
    public void MultiSelectDropdown_ClosingThePopup_KeepsWhatWasChecked()
    {
        _filter.SetOptions(new[] { "COL-01", "GAP-02" });
        ClickField();
        Rows()[1].isOn = true;
        Assume.That(_filter.IsAllowed("COL-01"), Is.False, "ограничение задано");

        ClickField();
        ClickField();

        Assert.IsTrue(Rows()[1].isOn,
            "выбор живёт в самом виджете, а не в строках поповера: снос строк не имеет "
            + "права его стереть, иначе закрытие списка молча снимало бы фильтр");
        Assert.IsFalse(_filter.IsAllowed("COL-01"),
            "и фильтр продолжает отсекать невыбранный код после закрытия");
    }
}

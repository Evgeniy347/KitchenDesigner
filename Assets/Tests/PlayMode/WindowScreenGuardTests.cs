using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core.UI;

public class WindowScreenGuardTests
{
    private RectTransform _parent = null!;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("Parent", typeof(RectTransform));
        _parent = (RectTransform)go.transform;
        _parent.sizeDelta = new Vector2(1920, 1080);
    }

    [TearDown]
    public void TearDown()
    {
        if (_parent != null) Object.DestroyImmediate(_parent.gameObject);
    }

    private RectTransform MakeWindow(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        rt.sizeDelta = new Vector2(300, 200);
        return rt;
    }

    private bool InsideParent(RectTransform window)
    {
        var corners = new Vector3[4];
        window.GetWorldCorners(corners);
        Vector2 bottomLeft = _parent.InverseTransformPoint(corners[0]);
        Vector2 topRight = _parent.InverseTransformPoint(corners[2]);
        Rect pr = _parent.rect;
        return bottomLeft.x >= pr.xMin - 0.01f && topRight.x <= pr.xMax + 0.01f
            && bottomLeft.y >= pr.yMin - 0.01f && topRight.y <= pr.yMax + 0.01f;
    }

    [UnityTest]
    public IEnumerator OpeningAWindow_PutsItInFrontOfTheOthers()
    {
        var first = MakeWindow("First");
        var second = MakeWindow("Second");
        WindowDrag.Attach(first, 30f);
        WindowDrag.BringToFront(second);
        first.gameObject.SetActive(false);
        yield return null;

        first.gameObject.SetActive(true);
        yield return null;

        Assert.AreEqual(_parent.childCount - 1, first.GetSiblingIndex(),
            "открытое окно обязано оказаться поверх остальных: иначе оно откроется "
            + "под уже лежащей панелью, и пользователь решит, что кнопка не сработала. "
            + "Проверка живёт в PlayMode: вне Play mode Unity не зовёт OnEnable");
    }

    [UnityTest]
    public IEnumerator ShrinkingTheScreen_PullsTheWindowBackIntoIt()
    {
        var window = MakeWindow("Win");
        WindowDrag.Attach(window, 30f);
        window.anchoredPosition = new Vector2(700, -400);
        yield return null;
        Assume.That(InsideParent(window), Is.True, "исходно окно внутри экрана");

        _parent.sizeDelta = new Vector2(600, 400);
        yield return null;

        Assert.IsTrue(InsideParent(window),
            "при уменьшении экрана окно обязано подтянуться внутрь: сама панель "
            + "OnRectTransformDimensionsChange при этом не получает, поэтому сторож "
            + "следит за rect родителя в LateUpdate");
    }

    [UnityTest]
    public IEnumerator GrowingTheWindow_KeepsItInsideTheScreen()
    {
        var window = MakeWindow("Win");
        WindowDrag.Attach(window, 30f);
        window.anchoredPosition = new Vector2(0, -400);
        yield return null;

        window.sizeDelta = new Vector2(300, 900);
        yield return null;

        Assert.IsTrue(InsideParent(window),
            "окно, выросшее в высоту (так меняет размер контекстное меню в Layout), "
            + "не должно свеситься за нижний край экрана");
    }
}

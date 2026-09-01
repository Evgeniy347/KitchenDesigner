using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class MeasureLabelsUITests
{
    private GameObject? _canvasGo;
    private GameObject? _camGo;
    private Canvas? _canvas;
    private Camera? _cam;
    private MeasureLabelsUI? _labels;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvas = _canvasGo!.AddComponent<Canvas>();
        _canvas!.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo!.AddComponent<CanvasScaler>();

        _camGo = new GameObject("Cam");
        _cam = _camGo!.AddComponent<Camera>();
        _cam!.transform.position = new Vector3(0f, 0f, -5f);
        _cam!.transform.rotation = Quaternion.identity;

        var host = new GameObject("MeasureLabels");
        host.transform.SetParent(_canvasGo!.transform);
        _labels = host.AddComponent<MeasureLabelsUI>();
        _labels!.Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        if (_camGo != null) Object.DestroyImmediate(_camGo);
    }

    private TextMeshProUGUI FirstLabel()
    {
        var label = _canvasGo!.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        Assert.IsNotNull(label, "подпись не создана — тест бы зеленел впустую");
        return label!;
    }

    private static readonly Vector3 FarLeft = new Vector3(-100f, 0f, 0f);
    private static readonly Vector3 FarRight = new Vector3(100f, 0f, 0f);

    private float ScreenLength(Vector3 a, Vector3 b)
    {
        Vector3 sa = _cam!.WorldToScreenPoint(a);
        Vector3 sb = _cam!.WorldToScreenPoint(b);
        return (new Vector2(sb.x, sb.y) - new Vector2(sa.x, sa.y)).magnitude;
    }

    [Test]
    public void MeasureLabels_HideTheNumber_WhenTheSegmentIsShorterThanTheText()
    {
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, FarLeft, FarRight);
        Assert.IsTrue(FirstLabel().gameObject.activeSelf,
            "на длинном отрезке число видно — иначе следующая проверка ничего не значит");

        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, new Vector3(0f, 0f, 0f), new Vector3(0.0001f, 0f, 0f));
        Assert.IsFalse(FirstLabel().gameObject.activeSelf,
            "Число прячется, когда экранная проекция отрезка короче самой надписи: иначе оно "
            + "вылезает за концы отрезка и читается как подпись соседнего замера");
    }

    [Test]
    public void MeasureLabels_MeasureTheTextInScreenPixels_NotInCanvasUnits()
    {
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, FarLeft, FarRight);
        var label = FirstLabel();
        Assume.That(label.preferredWidth, Is.GreaterThan(0f),
            "без метрик текста этой проверке не на что опереться");
        Assume.That(label.gameObject.activeSelf, Is.True);

        _canvas!.scaleFactor = 2f * ScreenLength(FarLeft, FarRight) / label.preferredWidth;
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, FarLeft, FarRight);

        Assert.IsFalse(FirstLabel().gameObject.activeSelf,
            "Ширина текста считается в единицах канвы, а длина отрезка — в пикселях экрана. "
            + "Без приведения через scaleFactor надпись на растянутой канве вылезала бы за "
            + "концы отрезка вдвое, а сравнение этого не замечало");
    }

    [Test]
    public void MeasureLabels_SitAboveTheSegment_NotOnTheDottedLine()
    {
        var a = FarLeft;
        var b = FarRight;
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, a, b);

        float midScreenY = (_cam!.WorldToScreenPoint(a).y + _cam!.WorldToScreenPoint(b).y) * 0.5f;

        Assert.AreEqual(midScreenY + MeasureLabelsUI.LiftAboveTheDottedLinePx,
            FirstLabel().rectTransform.position.y, 0.01f,
            "Подпись приподнята над отрезком, чтобы не лежать на его пунктире");
    }

    [Test]
    public void MeasureLabels_KeepAConstantSize_AndStayUpright_WhenTheCameraPullsBack()
    {
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, FarLeft, FarRight);
        var near = FirstLabel();
        float sizeNear = near.fontSize;
        var rotationNear = near.rectTransform.rotation;

        _cam!.transform.position = new Vector3(0f, 0f, -40f);
        _labels!.ReuseThePoolFromTheStart();
        _labels!.Place(_cam!, FarLeft, FarRight);
        var far = FirstLabel();

        Assert.AreEqual(sizeNear, far.fontSize,
            "Подписи — обычный экранный текст, а не world-space: при отдалении камеры размер "
            + "надписи не меняется");
        Assert.AreEqual(rotationNear, far.rectTransform.rotation,
            "и она не разворачивается ребром к зрителю вслед за камерой");
        Assert.AreEqual(Quaternion.identity, far.rectTransform.rotation);
    }
}

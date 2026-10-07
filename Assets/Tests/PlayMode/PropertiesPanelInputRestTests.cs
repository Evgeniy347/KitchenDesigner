using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Настоящая панель свойств дивана, настоящие поля: набранное в любое числовое поле
/// обязано стоять там же, где стоит нетронутое. Правый край цифр относительно правого края
/// поля не зависит от самих цифр (выравнивание вправо), поэтому у всех числовых полей панели
/// он один и тот же — до набора и после. Скриншот пользователя: «Скругление» и «Высота основания»
/// (строки со значком «i») после ввода стояли левее «Ширины» и «Глубины» (строки без значка); тест
/// проверяет и гипотезу про значок: до набора поля со значком и без него обязаны совпасть.</summary>
public class PropertiesPanelInputRestTests
{
    private const float Tolerance = 0.01f;

    private static GameObject? _bootstrap;
    private static GameObject? _mainCamera;
    private static Canvas? _uiCanvas;

    [OneTimeTearDown]
    public void OneTimeTearDownOnce()
    {
        if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
        if (_mainCamera != null) Object.DestroyImmediate(_mainCamera);
        var basePlate = Object.FindAnyObjectByType<BasePlate>();
        if (basePlate != null) Object.DestroyImmediate(basePlate.gameObject);
        _bootstrap = null;
        _mainCamera = null;
        _uiCanvas = null;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        if (_bootstrap == null)
        {
            _mainCamera = new GameObject("Main Camera");
            _mainCamera.tag = "MainCamera";
            _mainCamera.AddComponent<Camera>();
            _mainCamera.transform.position = new Vector3(0f, 3f, -5f);
            _mainCamera.transform.LookAt(Vector3.zero);

            SaveLoadManager.LastPath = "";
            var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
            if (File.Exists(autoPath)) File.Delete(autoPath);

            _bootstrap = new GameObject("Bootstrap");
            _bootstrap.AddComponent<Bootstrap>();

            yield return null;
            yield return null;

            _uiCanvas = UIManager.Instance!.Canvas;
            Assert.IsNotNull(_uiCanvas, "Canvas should be created by Bootstrap");
        }

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        ContextMenuUI.Instance?.Close();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null && e.GetComponent<BasePlate>() == null) Object.Destroy(e.gameObject);
        yield return null;
    }

    private static TMP_InputField Field(string node)
    {
        var panel = UiTestTree.FindDeep(_uiCanvas!.transform, "ContextMenu");
        Assert.IsNotNull(panel, "панель свойств не найдена под канвой");
        foreach (var field in panel!.GetComponentsInChildren<TMP_InputField>(true))
            if (field.name == "F_" + node) return field;
        Assert.Fail($"в панели свойств нет поля «F_{node}»");
        return null!;
    }

    private static float RightGap(TMP_InputField field)
    {
        var text = field.textComponent!;
        text.ForceMeshUpdate();
        float inkRight = field.transform.InverseTransformPoint(
            text.transform.TransformPoint(text.textBounds.max)).x;
        return ((RectTransform)field.transform).rect.xMax - inkRight;
    }

    private static List<(string node, string hint)> NumberNodes() => new()
    {
        (Loc.T("element.common.width"), "без значка"),
        (Loc.T("element.common.depth"), "без значка"),
        (SofaFieldsEditor.CornerRadiusNode, "со значком «i»"),
        (SofaFieldsEditor.SeatHeightNode, "со значком «i»"),
    };

    private static void AssertAllGapsEqual(float baseline, string moment)
    {
        foreach (var (node, hint) in NumberNodes())
            Assert.AreEqual(baseline, RightGap(Field(node)), Tolerance,
                $"{moment}: правый край цифр в поле «{node}» ({hint}) отличается от поля «Ширина» — "
                + "цифры выровнены вправо, зазор до края поля обязан быть одинаковым");
    }

    private static KitchenElement NewSofa(float xUnits)
    {
        var dims = new Vector3Int(SofaElement.DefaultWidthMM, SofaElement.DefaultHeightMM,
            SofaElement.DefaultDepthMM);
        var pos = new Vector3(xUnits, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateSofa(dims, SofaElement.DefaultCornerRadiusMM,
            SofaElement.DefaultSeatHeightMM, "Диван", pos);
        return go.GetComponent<KitchenElement>();
    }

    /// <summary>Панель закрывается скрытием корня (ContextMenuUI.Close → SetActive(false)), а
    /// поля и строки живут дальше и показывают следующий элемент. Если закрыть панель, пока в поле
    /// идёт правка, TMP успевает сдвинуть текст за кареткой, а OnDeselect не приходит вовсе: поле
    /// просто гаснет. Единственная ветка, которая вернёт текст в покой до следующего открытия, —
    /// OnDisable в RestingInputField. Без неё открытая на другом элементе панель показывала бы
    /// число левее нетронутых.</summary>
    [UnityTest]
    public IEnumerator PanelClosedMidEdit_ReopensWithTheTextAtRest()
    {
        var first = NewSofa(0f);
        var second = NewSofa(4f);
        ContextMenuUI.Instance!.Open(first);
        yield return null;
        yield return null;

        var width = Field(Loc.T("element.common.width"));
        var textRect = width.textComponent!.rectTransform;
        float baseline = RightGap(width);
        Assert.AreEqual(0f, textRect.anchoredPosition.x, Tolerance,
            "посылка: нетронутое поле стоит в покое");

        width.ActivateInputField();
        yield return null;
        Assert.IsTrue(width.isFocused, "посылка: правка идёт, поле в фокусе");
        const float scrolledBy = -30f;
        textRect.anchoredPosition = new Vector2(scrolledBy, 0f);
        Assert.AreEqual(scrolledBy, textRect.anchoredPosition.x, Tolerance,
            "посылка: текст сдвинут так, как его двигает TMP за кареткой");

        ContextMenuUI.Instance!.Close();

        Assert.IsFalse(width.isActiveAndEnabled,
            "посылка: панель закрылась скрытием, поля не уничтожены — их ждёт повторное использование");
        Assert.AreEqual(0f, textRect.anchoredPosition.x, Tolerance,
            "панель закрыта посреди правки, а текст остался сдвинутым: OnDisable в "
            + "RestingInputField не вернул его в покой, и сдвиг переживёт закрытие");

        ContextMenuUI.Instance!.Open(second);
        yield return null;
        yield return null;

        Assert.AreSame(width, Field(Loc.T("element.common.width")),
            "посылка: на другом элементе панель переиспользует то же поле, а не строит новое");
        Assert.AreEqual(0f, textRect.anchoredPosition.x, Tolerance,
            "на другом элементе переиспользованное поле открылось со сдвинутым текстом");
        Assert.AreEqual(baseline, RightGap(width), Tolerance,
            "цифры в переиспользованном поле стоят не там, где в нетронутом: зазор до правого края "
            + "должен совпасть с зазором до правки");
    }

    [UnityTest]
    public IEnumerator SofaPanel_TypedNumbers_StandWhereUntouchedOnesStand()
    {
        var dims = new Vector3Int(SofaElement.DefaultWidthMM, SofaElement.DefaultHeightMM,
            SofaElement.DefaultDepthMM);
        var pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
        var go = ElementFactory.CreateSofa(dims, SofaElement.DefaultCornerRadiusMM,
            SofaElement.DefaultSeatHeightMM, "Диван", pos);
        ContextMenuUI.Instance!.Open(go.GetComponent<KitchenElement>());
        yield return null;
        yield return null;

        float baseline = RightGap(Field(Loc.T("element.common.width")));
        AssertAllGapsEqual(baseline, "до набора");

        yield return InputFieldRestTests.TypeInto(Field(SofaFieldsEditor.CornerRadiusNode), "50", false);
        yield return InputFieldRestTests.TypeInto(Field(SofaFieldsEditor.SeatHeightNode), "360", false);
        yield return null;
        yield return null;
        AssertAllGapsEqual(baseline, "после набора в «Скругление» и «Высота основания»");

        yield return InputFieldRestTests.TypeInto(Field(Loc.T("element.common.width")), "1800", false);
        yield return InputFieldRestTests.TypeInto(Field(Loc.T("element.common.depth")), "950", false);
        yield return null;
        yield return null;
        AssertAllGapsEqual(baseline, "после набора в «Ширина» и «Глубина»");
    }
}

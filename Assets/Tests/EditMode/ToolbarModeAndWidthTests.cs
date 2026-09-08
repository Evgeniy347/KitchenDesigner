using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>D9 + D10: режим правки и вид «Фото» больше не делят одну кнопку с
/// трёхшаговым циклом, а панели тулбара приведены к одному правилу — иконка с
/// tooltip, текстом остаются только режимы. Ширина проверяется прямым замером
/// построенного UI, а не константой на глаз.</summary>
public class ToolbarModeAndWidthTests
{
    private sealed class FakeToolbarHost : IToolbarHost
    {
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => false;
        public void SaveCurrent() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private GameObject? _canvasGo;
    private Transform _bar = null!;
    private ResizeHandleManager.HandleMode _handleModeBefore;

    [SetUp]
    public void SetUp()
    {
        _handleModeBefore = ResizeHandleManager.Mode;
        EditModeManager.SetMode(EditMode.Normal);

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        var toolbar = new ToolbarUI();
        toolbar.Build(_canvasGo.transform, new FakeToolbarHost());
        _bar = _canvasGo.transform.Find("Toolbar")!;
    }

    [TearDown]
    public void TearDown()
    {
        if (ResizeHandleManager.Mode != _handleModeBefore) ResizeHandleManager.ToggleMode();
        EditModeManager.SetMode(EditMode.Normal);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    // ── D9: режим и вид разведены ───────────────────────────

    [Test]
    public void Build_CreatesSeparateNormalRoomAndPhotoButtons_NotASingleCycleButton()
    {
        Assert.IsNotNull(_bar.Find("ModeNormal"), "«Обычный» — отдельная кнопка сегментированного переключателя");
        Assert.IsNotNull(_bar.Find("ModeRoom"), "«Помещение» — тоже отдельная кнопка");
        Assert.IsNotNull(_bar.Find("ModePhoto"), "«Фото» — отдельный тоггл, а не третий шаг общего цикла");
    }

    [Test]
    public void NormalModeButton_FromPhoto_ReturnsToNormalInOneClick()
    {
        EditModeManager.SetMode(EditMode.Room);
        EditModeManager.SetMode(EditMode.Photo);
        Assume.That(PhotoMode.Active, Is.True, "фоторежим обязан быть включён перед проверкой");

        _bar.Find("ModeNormal")!.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode,
            "раньше выход из фото требовал сначала попасть в «Помещение» — один клик обязан "
            + "вернуть сразу в «Обычный»");
        Assert.IsFalse(PhotoMode.Active);
    }

    [Test]
    public void RoomModeButton_Click_NeverTogglesPhoto()
    {
        _bar.Find("ModeRoom")!.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(EditMode.Room, EditModeManager.Mode);
        Assert.IsFalse(PhotoMode.Active, "переключение обычный/помещение не имеет права включать фото");
    }

    [Test]
    public void PhotoModeButton_Click_DrivesThePhotoModeEntryPoint()
    {
        _bar.Find("ModePhoto")!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(PhotoMode.Active,
            "кнопка «Фото» обязана звать существующую точку входа PhotoMode.Toggle");
        Assert.AreEqual(EditMode.Photo, EditModeManager.Mode);
    }

    // ── D10: панели — иконки с tooltip, режимы — текст ──────

    [Test]
    public void ErrorsButton_IsAnIconWithTooltip_NotAWordyLabel()
    {
        var errors = _bar.Find("Errors")!;

        Assert.IsNotNull(errors.Find("Errors_Icon"), "«Ошибки» переведена на иконку (правило D10)");
        Assert.IsNotNull(errors.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");
    }

    [Test]
    public void ModeButtons_StayText_AsTheOneNamedExceptionToTheIconRule()
    {
        Assert.IsNotNull(_bar.Find("ModeNormal")!.GetComponentInChildren<TMP_Text>(),
            "режимы — единственное, что остаётся текстом по правилу D10");
        Assert.IsNotNull(_bar.Find("ModeRoom")!.GetComponentInChildren<TMP_Text>());
        Assert.IsNotNull(_bar.Find("ModePhoto")!.GetComponentInChildren<TMP_Text>());
    }

    [Test]
    public void TextButtons_WidthTracksItsOwnLabel_NotASharedMagicNumber()
    {
        var spec = (RectTransform)_bar.Find("Spec")!;
        var hierarchy = (RectTransform)_bar.Find("Hierarchy")!;

        Assert.Greater(spec.sizeDelta.x, hierarchy.sizeDelta.x,
            "«Спецификация» длиннее «Сцены» — авторазмер обязан это отразить, а не тащить "
            + "общую фиксированную ширину, как было раньше");
    }

    [Test]
    public void HandleModeButton_WidthFitsBothCaptions_AndNeverResizesOnToggle()
    {
        var handleBtn = (RectTransform)_bar.Find("HandleMode")!;
        float widthBefore = handleBtn.sizeDelta.x;

        handleBtn.GetComponent<Button>().onClick.Invoke();
        float widthAfterToggle = handleBtn.sizeDelta.x;
        handleBtn.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(widthBefore, widthAfterToggle, 0.01f,
            "ширина зафиксирована по ХУДШЕЙ из двух подписей заранее — иначе переключение "
            + "«Ручки: растяжение» ↔ «Ручки: перенос» сдвигало бы все кнопки правее");
    }

    // ── D10: помещается на 1366px ноутбука, и не разъезжается на 1920 ──

    private static float RightEdgeOfFlow(Transform bar)
    {
        float maxRight = 0f;
        foreach (Transform child in bar)
        {
            if (child.name == "Music") continue;
            var rt = (RectTransform)child;
            float right = rt.anchoredPosition.x + rt.sizeDelta.x;
            if (right > maxRight) maxRight = right;
        }
        return maxRight;
    }

    [TestCase(1920f)]
    [TestCase(1366f)]
    public void Toolbar_FitsWithoutOverlappingTheRightAnchoredMusicButton(float screenWidth)
    {
        var music = (RectTransform)_bar.Find("Music")!;
        float reservedFromRightEdge = -music.anchoredPosition.x + music.sizeDelta.x;
        float budget = screenWidth - reservedFromRightEdge;

        float flowRight = RightEdgeOfFlow(_bar);

        Assert.Less(flowRight, budget,
            $"при ширине экрана {screenWidth}px поток кнопок доходит до {flowRight:0}px, "
            + $"а свободно только {budget:0}px до кнопки «Музыка»");
    }
}

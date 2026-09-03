using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Что происходит с окном настроек ПОСЛЕ его уничтожения.
///
/// Одна незамеченная ссылка стоила 91 красного теста в трёх разных классах, и
/// ни один из них не про настройки. Цепочка была такая: SettingsViewTab
/// подписан на статическое EditModeManager.Changed и отписывается только из
/// OnDestroy; тест, который зовёт Build дважды, оставляет первую вкладку
/// подписанной навсегда. Панель уничтожают — делегат остаётся. Следующая смена
/// режима зовёт RefreshDependentStates на мёртвых RectTransform.
///
/// Раньше это молчало: все обращения там шли через Unity-проверку != null и
/// уходили в никуда. Подгонка высоты области прокрутки такой проверки не имела
/// и бросала MissingReferenceException — причём ИЗ TearDown, из-за чего
/// DestroyImmediate не выполнялся, живое зарегистрированное окно утекало в
/// следующие классы, и в снимках сцены появлялся блок windows с настройками.
/// Отсюда и разброс: VisibilityModeIntegrationTests, WallCutoutTests,
/// WallManagerTests и SnapshotTests падали от чужой утечки.
///
/// Поэтому проверок три, и каждая рвёт цепочку в своём звене. Любая из них,
/// покрасневшая в одиночку, означает, что звено вернулось.</summary>
public class SettingsPanelLifetimeTests
{
    private GameObject? _canvasGo;

    private SettingsPanelUI BuildPanel()
    {
        _canvasGo = new GameObject("LifetimeCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        var ui = _canvasGo.AddComponent<SettingsPanelUI>();
        ui.Build(_canvasGo.transform);
        return ui;
    }

    private void DestroyPanel()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        _canvasGo = null;
    }

    [SetUp]
    public void SetUp()
    {
        EditModeManager.Reset();
        ProjectWindows.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        DestroyPanel();
        ProjectWindows.Clear();
    }

    [Test]
    public void EditModeChange_AfterThePanelWasDestroyed_DoesNotTouchItsDeadRects()
    {
        BuildPanel();
        DestroyPanel();

        Assert.DoesNotThrow(() => EditModeManager.SetMode(EditMode.Room),
            "смена режима после уничтожения панели не должна ничего трогать: делегат "
            + "статического события переживает свой объект, и именно так 37 тестов в "
            + "трёх чужих классах падали с MissingReferenceException на RectTransform");
    }

    [Test]
    public void BuildTwice_LeavesNoTabSubscribedToTheEditMode()
    {
        var ui = BuildPanel();
        ui.Build(_canvasGo!.transform);
        DestroyPanel();

        Assert.DoesNotThrow(() => EditModeManager.SetMode(EditMode.Room),
            "второй Build обязан отписать вкладки, которые заменяет. Это и был исходный "
            + "источник: одна осиротевшая SettingsViewTab, подписанная на EditModeManager, "
            + "роняла TearDown у тестов, которые про настройки даже не знают");
    }

    [Test]
    public void DestroyedPanel_IsNotCapturedIntoTheProjectFile()
    {
        BuildPanel();
        EditModeManager.SetMode(EditMode.Room);
        DestroyPanel();

        var ids = ProjectWindows.Capture().Select(w => w.id).ToList();

        CollectionAssert.DoesNotContain(ids, "settings",
            "уничтоженное окно не попадает в состояние проекта. Когда исключение "
            + "прерывало TearDown, панель оставалась живой и всплывала блоком windows "
            + "в 25 снимках сцены, к UI не имеющих отношения");
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

/// <summary>Подсказка у иконной кнопки: одна панель на весь UI, показ с
/// задержкой, снятие при уходе курсора и при исчезновении самой кнопки.
/// PlayMode — потому что показ идёт из Update по Time.unscaledTime.</summary>
public class TooltipUITests
{
    private GameObject _canvasGo = null!;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("Canvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();
    }

    [TearDown]
    public void TearDown()
    {
        TooltipUI.Hide();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private GameObject MakeButton(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_canvasGo.transform, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(40, 40);
        return go;
    }

    private static void Hover(GameObject button) =>
        Fire(button, EventTriggerType.PointerEnter);

    private static void Unhover(GameObject button) =>
        Fire(button, EventTriggerType.PointerExit);

    private static void Fire(GameObject button, EventTriggerType type)
    {
        var trigger = button.GetComponent<EventTrigger>();
        Assert.IsNotNull(trigger, "на кнопке нет EventTrigger — подсказку не повесили");
        foreach (var entry in trigger!.triggers)
            if (entry.eventID == type)
                entry.callback.Invoke(new PointerEventData(EventSystem.current));
    }

    private Transform? TooltipRoot => _canvasGo.transform.Find("Tooltip");

    private static bool PanelShown(Transform root)
    {
        var panel = root.Find("TooltipPanel");
        return panel != null && panel.gameObject.activeSelf;
    }

    [Test]
    public void Attach_EmptyText_LeavesTheButtonAlone()
    {
        var button = MakeButton("Btn");

        TooltipUI.Attach(button, "");

        Assert.IsNull(button.GetComponent<EventTrigger>(),
            "пустой текст — не подсказка: вызывающему не приходится проверять это "
            + "самому, и кнопка не обрастает лишним обработчиком");
    }

    [UnityTest]
    public IEnumerator Hover_ShowsNothingImmediately_AndTheTextAfterTheDelay()
    {
        var button = MakeButton("Btn");
        TooltipUI.Attach(button, "Сохранить проект");

        Hover(button);
        yield return null;

        Assert.IsTrue(TooltipRoot == null || !PanelShown(TooltipRoot!),
            "подсказка не должна вспыхивать в тот же кадр: курсор, просто "
            + "проехавший по тулбару, зажигал бы её на каждой кнопке");

        yield return new WaitForSecondsRealtime(1f);

        Assert.IsNotNull(TooltipRoot, "после задержки подсказка обязана появиться");
        Assert.IsTrue(PanelShown(TooltipRoot!));
        Assert.AreEqual("Сохранить проект",
            TooltipRoot!.GetComponentInChildren<TMPro.TMP_Text>().text);
    }

    [UnityTest]
    public IEnumerator Unhover_BeforeDelayPasses_ShowsNothingAtAll()
    {
        var button = MakeButton("Btn");
        TooltipUI.Attach(button, "Сохранить проект");

        Hover(button);
        Unhover(button);
        yield return new WaitForSecondsRealtime(1f);

        Assert.IsTrue(TooltipRoot == null || !PanelShown(TooltipRoot!),
            "курсор ушёл раньше срока — отложенный показ обязан сняться");
    }

    [UnityTest]
    public IEnumerator ButtonDisabledWhileWaiting_ShowsNothing()
    {
        var button = MakeButton("Btn");
        TooltipUI.Attach(button, "Сохранить проект");

        Hover(button);
        button.SetActive(false);
        yield return new WaitForSecondsRealtime(1f);

        Assert.IsTrue(TooltipRoot == null || !PanelShown(TooltipRoot!),
            "кнопку выключили, пока подсказка ждала очереди: показывать нечего, "
            + "иначе подпись повиснет над пустым местом");
    }

    [UnityTest]
    public IEnumerator Attach_WithFuncProvider_ReadsTheTextFreshOnEveryHover()
    {
        var button = MakeButton("Btn");
        string current = "Первое значение";
        TooltipUI.Attach(button, () => current);

        Hover(button);
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual("Первое значение",
            TooltipRoot!.GetComponentInChildren<TMPro.TMP_Text>().text);

        Unhover(button);
        current = "Второе значение";
        Hover(button);
        yield return new WaitForSecondsRealtime(1f);

        Assert.AreEqual("Второе значение",
            TooltipRoot!.GetComponentInChildren<TMPro.TMP_Text>().text,
            "провайдер обязан вызываться заново при каждом наведении — иначе подсказка, "
            + "привязанная один раз, замораживает текст на момент сборки");
    }

    [UnityTest]
    public IEnumerator Tooltip_IsOneObjectOnTheCanvasOfTheButton_DrawnAboveEverything()
    {
        var first = MakeButton("First");
        var second = MakeButton("Second");
        TooltipUI.Attach(first, "Первая");
        TooltipUI.Attach(second, "Вторая");

        Hover(first);
        yield return new WaitForSecondsRealtime(1f);
        TooltipUI.Hide();
        Hover(second);
        yield return new WaitForSecondsRealtime(1f);

        int roots = 0;
        foreach (Transform child in _canvasGo.transform)
            if (child.name == "Tooltip") roots++;
        Assert.AreEqual(1, roots,
            "подсказка в интерфейсе ровно одна — плодить объект на кнопку незачем");

        var root = (RectTransform)TooltipRoot!;
        Assert.AreEqual(Vector2.zero, root.anchorMin);
        Assert.AreEqual(Vector2.one, root.anchorMax,
            "корень подсказки растянут во всю канву: у Transform-родителя без "
            + "растяжки якоря вложенной панели считаются не от экрана");
        Assert.AreEqual(_canvasGo.transform.childCount - 1, root.GetSiblingIndex(),
            "порядок отрисовки uGUI — это порядок в иерархии: подсказка обязана "
            + "быть последней, иначе её накроет соседняя панель");
    }
}
